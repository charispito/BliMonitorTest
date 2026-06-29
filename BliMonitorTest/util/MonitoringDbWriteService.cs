using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BliMonitorTest.data;
using Microsoft.Data.Sqlite;

namespace BliMonitorTest.util.MonitoringDb
{
    internal sealed class MonitoringDbWriteService : IDisposable
    {
        public static MonitoringDbWriteService Instance { get; } = new MonitoringDbWriteService();

        private readonly BlockingCollection<InsertItem> _queue =
            new BlockingCollection<InsertItem>(boundedCapacity: 20000);

        private readonly BlockingCollection<ErrorHistoryItem> _errorQueue =
            new BlockingCollection<ErrorHistoryItem>(boundedCapacity: 5000);

        private readonly object _startLock = new object();

        private SqliteConnection _db;
        private bool _dbReady;
        private string _dbPath;

        private CancellationTokenSource _cts;
        private Task _worker;

        private int _batchSize = 200;
        private int _flushIntervalMs = 300;

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
                try { _errorQueue.CompleteAdding(); } catch { }

                try { _worker.Wait(3000); } catch { }

                _worker = null;

                try { _db?.Dispose(); } catch { }
                _db = null;

                try { _cts?.Dispose(); } catch { }
                _cts = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void Enqueue(Duo8StatusPacket packet, int channelNo, int sourceType)
        {
            if (packet == null) return;

            if (_worker == null)
                Start(_dbPath ?? BliMonitorTest.util.StoragePathUtil.StoragePathUtil.GetDbPath());

            _queue.TryAdd(new InsertItem(packet, channelNo, sourceType));
        }

        public void EnqueueErrorHistory(Duo8ErrorResponse response, int sourceType, int channelNo)
        {
            if (response == null) return;

            if (_worker == null)
                Start(_dbPath ?? BliMonitorTest.util.StoragePathUtil.StoragePathUtil.GetDbPath());

            _errorQueue.TryAdd(new ErrorHistoryItem(response, sourceType, channelNo));
        }

        private void EnsureOpen()
        {
            if (_db != null && _db.State == System.Data.ConnectionState.Open)
                return;

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
            var errorBuffer = new System.Collections.Generic.List<ErrorHistoryItem>(_batchSize);
            DateTime lastFlush = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_queue.TryTake(out var item, millisecondsTimeout: _flushIntervalMs, cancellationToken: token))
                        stateBuffer.Add(item);

                    while (_errorQueue.TryTake(out var ev))
                        errorBuffer.Add(ev);

                    bool timeToFlush = (DateTime.UtcNow - lastFlush).TotalMilliseconds >= _flushIntervalMs;

                    if (stateBuffer.Count >= _batchSize ||
                        errorBuffer.Count >= _batchSize ||
                        timeToFlush)
                    {
                        using (var tx = _db.BeginTransaction())
                        {
                            foreach (var it in stateBuffer)
                            {
                                MonitoringDb.InsertDb(
                                    ref _db,
                                    _dbPath,
                                    ref _dbReady,
                                    it.SourceType,
                                    it.ChannelNo,
                                    it.Packet);
                            }

                            foreach (var ev in errorBuffer)
                            {
                                MonitoringDb.InsertErrorHistory(
                                    ref _db,
                                    _dbPath,
                                    ref _dbReady,
                                    ev.SourceType,
                                    ev.ChannelNo,
                                    ev.Response);
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

            try
            {
                if (stateBuffer.Count > 0 || errorBuffer.Count > 0)
                {
                    using (var tx = _db.BeginTransaction())
                    {
                        foreach (var it in stateBuffer)
                        {
                            MonitoringDb.InsertDb(
                                ref _db,
                                _dbPath,
                                ref _dbReady,
                                it.SourceType,
                                it.ChannelNo,
                                it.Packet);
                        }

                        foreach (var ev in errorBuffer)
                        {
                            MonitoringDb.InsertErrorHistory(
                                ref _db,
                                _dbPath,
                                ref _dbReady,
                                ev.SourceType,
                                ev.ChannelNo,
                                ev.Response);
                        }

                        tx.Commit();
                    }
                }
            }
            catch
            {
            }
        }

        private readonly struct InsertItem
        {
            public readonly Duo8StatusPacket Packet;
            public readonly int ChannelNo;
            public readonly int SourceType;

            public InsertItem(Duo8StatusPacket packet, int channelNo, int sourceType)
            {
                Packet = packet;
                ChannelNo = channelNo;
                SourceType = sourceType;
            }
        }

        private readonly struct ErrorHistoryItem
        {
            public readonly Duo8ErrorResponse Response;
            public readonly int SourceType;
            public readonly int ChannelNo;

            public ErrorHistoryItem(Duo8ErrorResponse response, int sourceType, int channelNo)
            {
                Response = response;
                SourceType = sourceType;
                ChannelNo = channelNo;
            }
        }
    }
}
