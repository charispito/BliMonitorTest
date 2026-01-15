using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BliMonitorTest.util.MonitoringDb
{
    internal sealed class MonitoringDbWriteService : IDisposable
    {
        // 싱글톤
        public static MonitoringDbWriteService Instance { get; } = new MonitoringDbWriteService();

        private readonly BlockingCollection<InsertItem> _queue =
            new BlockingCollection<InsertItem>(boundedCapacity: 20000);

        private readonly object _startLock = new object();

        private SqliteConnection _db;
        private bool _dbReady;
        private string _dbPath;

        private CancellationTokenSource _cts;
        private Task _worker;

        // 튜닝 포인트
        private int _batchSize = 200;             // 한 번에 커밋할 레코드 수
        private int _flushIntervalMs = 300;       // batch가 덜 차도 일정 주기로 커밋

        private MonitoringDbWriteService() { }

        public void Start(string dbPath, int batchSize = 200, int flushIntervalMs = 300)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is null/empty", nameof(dbPath));

            lock (_startLock)
            {
                if (_worker != null) return;

                _dbPath = dbPath;
                _batchSize = Math.Max(1, batchSize);
                _flushIntervalMs = Math.Max(50, flushIntervalMs);

                _cts = new CancellationTokenSource();
                _worker = Task.Run(() => WorkerLoop(_cts.Token));
            }
        }

        public void Stop()
        {
            lock (_startLock)
            {
                if (_worker == null) return;

                try { _cts.Cancel(); } catch { }
                try { _queue.CompleteAdding(); } catch { }

                try { _worker.Wait(3000); } catch { }

                _worker = null;

                try { _db?.Dispose(); } catch { }
                _db = null;

                try { _cts?.Dispose(); } catch { }
                _cts = null;
            }
        }

        public void Dispose() => Stop();

        /// <summary>
        /// 호출 측(다채널/단일채널)에서는 이것만 호출하면 됩니다.
        /// DB 쓰기는 백그라운드에서 배치 처리됩니다.
        /// </summary>
        public void Enqueue( bool isNewVersion, BliMonitorTest.data.ReadData data, int number, float off_sum, int air_sum, int channelNo, int sourceType )
        {
            if (data == null) return;

            // Start 안 했으면 즉시 시작(방어)
            if (_worker == null)
                Start(_dbPath ?? BliMonitorTest.util.StoragePathUtil.StoragePathUtil.GetDbPath());

            // 큐가 꽉 차면 Drop(혹은 Block) 정책 선택 가능
            // 지금은 “Block 대신 Drop + 로그”가 안전한 편
            if (!_queue.TryAdd(new InsertItem(isNewVersion, data, number, off_sum, air_sum, channelNo, sourceType)))
            {
                System.Diagnostics.Debug.WriteLine("DB queue full -> drop");
            }
        }

        // -------------------------
        // 내부 워커
        // -------------------------

        private void EnsureOpen()
        {
            if (_db != null) return;

            _db = new SqliteConnection($"Data Source={_dbPath};");
            _db.Open();

            // 동시성/안정성 PRAGMA
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA journal_mode=WAL;
                    PRAGMA synchronous=NORMAL;
                    PRAGMA busy_timeout=5000;
                    ";
                cmd.ExecuteNonQuery();
            }

            // 테이블 생성
            MonitoringDb.EnsureDb(ref _db, _dbPath, ref _dbReady);
        }

        private void WorkerLoop(CancellationToken token)
        {
            EnsureOpen();

            var buffer = new System.Collections.Generic.List<InsertItem>(_batchSize);
            DateTime lastFlush = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 1) 하나를 기다림(타임아웃으로 주기 flush)
                    if (_queue.TryTake(out var item, millisecondsTimeout: _flushIntervalMs, cancellationToken: token))
                    {
                        buffer.Add(item);
                    }

                    // 2) batchSize 채웠거나, 시간 지나면 flush
                    bool timeToFlush = (DateTime.UtcNow - lastFlush).TotalMilliseconds >= _flushIntervalMs;
                    if (buffer.Count >= _batchSize || (buffer.Count > 0 && timeToFlush))
                    {
                        Flush(buffer);
                        buffer.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // DB 오류가 나면 잠깐 쉬고 재시도(필요하면 로그)
                    Thread.Sleep(200);
                }
            }

            // 종료 전 잔여 flush
            try
            {
                if (buffer.Count > 0)
                {
                    Flush(buffer);
                    buffer.Clear();
                }
            }
            catch { }
        }

        private void Flush(System.Collections.Generic.List<InsertItem> items)
        {
            // 하나의 트랜잭션으로 묶어서 성능 + 안정성 확보
            using (var tx = _db.BeginTransaction())
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];

                    // IMPORTANT:
                    // 아래는 기존 MonitoringDb.InsertDb를 그대로 재사용(하지만 EnsureDb를 또 타지 않게 해야 더 좋음)
                    // 지금 단계에서는 “동작 우선”으로 ref _db를 넘겨도 OK.
                    MonitoringDb.InsertDb( ref _db, _dbPath, ref _dbReady, it.IsNewVersion, it.Data, it.Number, it.OffSum, it.AirSum, it.ChannelNo, it.SourceType );
                }

                tx.Commit();
            }
        }

        private readonly struct InsertItem
        {
            public readonly bool IsNewVersion;
            public readonly BliMonitorTest.data.ReadData Data;
            public readonly int Number;
            public readonly float OffSum;
            public readonly int AirSum;
            public readonly int ChannelNo;
            public readonly int SourceType;

            public InsertItem(bool isNewVersion, BliMonitorTest.data.ReadData data, int number, float offSum, int airSum, int channelNo, int sourceType)
            {
                IsNewVersion = isNewVersion;
                Data = data;
                Number = number;
                OffSum = offSum;
                AirSum = airSum;
                ChannelNo = channelNo;
                SourceType = sourceType;
            }
        }
    }
}
