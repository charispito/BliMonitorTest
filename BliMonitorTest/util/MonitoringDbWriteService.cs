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

        // 상태 스트림 큐
        private readonly BlockingCollection<InsertItem> _queue = new BlockingCollection<InsertItem>(boundedCapacity: 20000);

        // 에러 이벤트 큐
        private readonly BlockingCollection<ErrorEventItem> _errorQueue = new BlockingCollection<ErrorEventItem>(boundedCapacity: 5000);

        private readonly object _startLock = new object();

        private SqliteConnection _db;
        private bool _dbReady;
        private string _dbPath;

        private CancellationTokenSource _cts;
        private Task _worker;

        // 튜닝 포인트
        private int _batchSize = 200;       // 한 번에 커밋할 레코드 수
        private int _flushIntervalMs = 300; // batch가 덜 차도 일정 주기로 커밋

        private MonitoringDbWriteService() { }

        // ---------------------------
        // 외부 공개 API
        // ---------------------------

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
                try { _errorQueue.CompleteAdding(); } catch { }

                try { _worker.Wait(3000); } catch { }

                _worker = null;

                try { _db?.Dispose(); } catch { }
                _db = null;

                try { _cts?.Dispose(); } catch { }
                _cts = null;
            }
        }

        public void Dispose() => Stop();

        // 상태 데이터(기존 ReadData) 적재
        public void Enqueue( BliMonitorTest.data.ReadData data, int number, float off_sum, int air_sum, int channelNo, int sourceType)
        {
            if (data == null) return;

            if (_worker == null)
                Start(_dbPath ?? BliMonitorTest.util.StoragePathUtil.StoragePathUtil.GetDbPath());

            _queue.TryAdd(new InsertItem(data, number, off_sum, air_sum, channelNo, sourceType));
        }

        // 에러 이벤트 적재(error_events 테이블)
        public void EnqueueErrorEvent(
            int sourceType, int channelNo,
            DateTime createdAt, string snapshotId, string fileName,
            int errorSlot, string errorText,
            int? runMode, double? heaterTemp, double? heaterOffTime,
            double? hotAirTemp, double? hotAirOnTime,
            int? runCount, double? exhaustTemp)
        {
            if (_worker == null)
                Start(_dbPath ?? BliMonitorTest.util.StoragePathUtil.StoragePathUtil.GetDbPath());

            _errorQueue.TryAdd(new ErrorEventItem(
                sourceType, channelNo, createdAt, snapshotId, fileName,
                errorSlot, errorText,
                runMode, heaterTemp, heaterOffTime,
                hotAirTemp, hotAirOnTime,
                runCount, exhaustTemp));
        }

        // ---------------------------
        // 내부 워커/플러시
        // ---------------------------

        private void EnsureOpen()
        {
            if (_db != null) return;

            _db = new SqliteConnection($"Data Source={_dbPath};");
            _db.Open();

            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA journal_mode=WAL;
                    PRAGMA synchronous=NORMAL;
                    PRAGMA busy_timeout=5000;
                ";
                cmd.ExecuteNonQuery();
            }

            MonitoringDb.EnsureDb(ref _db, _dbPath, ref _dbReady);
        }

        private void WorkerLoop(CancellationToken token)
        {
            EnsureOpen();

            var stateBuffer = new System.Collections.Generic.List<InsertItem>(_batchSize);
            var errorBuffer = new System.Collections.Generic.List<ErrorEventItem>(_batchSize);
            DateTime lastFlush = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // 상태 데이터: 타임아웃 대기
                    if (_queue.TryTake(out var item, millisecondsTimeout: _flushIntervalMs, cancellationToken: token))
                        stateBuffer.Add(item);

                    // 에러 데이터: 가능한 많이 수집
                    while (_errorQueue.TryTake(out var ev))
                        errorBuffer.Add(ev);

                    bool timeToFlush = (DateTime.UtcNow - lastFlush).TotalMilliseconds >= _flushIntervalMs;
                    if (stateBuffer.Count >= _batchSize || errorBuffer.Count >= _batchSize || timeToFlush)
                    {
                        using (var tx = _db.BeginTransaction())
                        {
                            // 상태 배치
                            foreach (var it in stateBuffer)
                            {
                                MonitoringDb.InsertDb(ref _db, _dbPath, ref _dbReady, it.Data, it.Number, it.OffSum, it.AirSum, it.ChannelNo, it.SourceType);
                            }

                            // 에러 배치
                            foreach (var ev in errorBuffer)
                            {
                                MonitoringDb.InsertErrorEvent(ref _db, _dbPath, ref _dbReady,
                                    ev.SourceType, ev.ChannelNo, ev.CreatedAt, ev.SnapshotId, ev.FileName,
                                    ev.ErrorSlot, ev.ErrorText, ev.RunMode, ev.HeaterTemp, ev.HeaterOffTime,
                                    ev.HotAirTemp, ev.HotAirOnTime, ev.RunCount, ev.ExhaustTemp);
                            }

                            tx.Commit();
                        }

                        stateBuffer.Clear();
                        errorBuffer.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }

            // 종료 전 잔여 플러시
            try
            {
                if (stateBuffer.Count > 0 || errorBuffer.Count > 0)
                {
                    using (var tx = _db.BeginTransaction())
                    {
                        foreach (var it in stateBuffer)
                        {
                            MonitoringDb.InsertDb(ref _db, _dbPath, ref _dbReady, it.Data, it.Number, it.OffSum, it.AirSum, it.ChannelNo, it.SourceType);
                        }

                        foreach (var ev in errorBuffer)
                        {
                            MonitoringDb.InsertErrorEvent(ref _db, _dbPath, ref _dbReady,
                                ev.SourceType, ev.ChannelNo, ev.CreatedAt, ev.SnapshotId, ev.FileName,
                                ev.ErrorSlot, ev.ErrorText, ev.RunMode, ev.HeaterTemp, ev.HeaterOffTime,
                                ev.HotAirTemp, ev.HotAirOnTime, ev.RunCount, ev.ExhaustTemp);
                        }

                        tx.Commit();
                    }
                }
            }
            catch { }
        }

        // ---------------------------
        // 내부 버퍼 타입(외부 비공개)
        // ---------------------------

        private readonly struct InsertItem
        {
            public readonly BliMonitorTest.data.ReadData Data;
            public readonly int Number;
            public readonly float OffSum;
            public readonly int AirSum;
            public readonly int ChannelNo;
            public readonly int SourceType;

            public InsertItem(BliMonitorTest.data.ReadData data, int number, float offSum, int airSum, int channelNo, int sourceType)
            {
                //IsNewVersion = isNewVersion;
                Data = data;
                Number = number;
                OffSum = offSum;
                AirSum = airSum;
                ChannelNo = channelNo;
                SourceType = sourceType;
            }
        }

        private readonly struct ErrorEventItem
        {
            public readonly int SourceType, ChannelNo;
            public readonly DateTime CreatedAt;
            public readonly string SnapshotId, FileName;
            public readonly int ErrorSlot;
            public readonly string ErrorText;
            public readonly int? RunMode;
            public readonly double? HeaterTemp, HeaterOffTime, HotAirTemp, HotAirOnTime;
            public readonly int? RunCount;
            public readonly double? ExhaustTemp;

            public ErrorEventItem(
                int sourceType, int channelNo, DateTime createdAt, string snapshotId, string fileName,
                int errorSlot, string errorText, int? runMode, double? heaterTemp, double? heaterOffTime,
                double? hotAirTemp, double? hotAirOnTime, int? runCount, double? exhaustTemp)
            {
                SourceType = sourceType; ChannelNo = channelNo;
                CreatedAt = createdAt; SnapshotId = snapshotId; FileName = fileName;
                ErrorSlot = errorSlot; ErrorText = errorText;
                RunMode = runMode; HeaterTemp = heaterTemp; HeaterOffTime = heaterOffTime;
                HotAirTemp = hotAirTemp; HotAirOnTime = hotAirOnTime;
                RunCount = runCount; ExhaustTemp = exhaustTemp;
            }
        }
    }
}
