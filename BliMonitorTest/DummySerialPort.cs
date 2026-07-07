using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using BliMonitorTest.dummy;

namespace DummySerialPortNs
{
    public enum ProtocolKind
    {
        ErrorDataRequest = 1,
        ErrorReset = 2,
        StartStopStatus = 3,
        ParameterRequest = 4,
        ParameterSet = 5
    }

    public class DummySerialPort : IDisposable
    {
        public string PortName { get; set; } = "DUMMY";
        public int BaudRate { get; set; } = 9600;

        private const byte CMD_ERROR_DATA = 0xB9;
        private const byte CMD_ERROR_RESET = 0xB6;
        private const byte CMD_STATUS = 0xA0;
        private const byte CMD_PARAMETER_READ = 0xC1;
        private const byte CMD_PARAMETER_SET = 0xC2;

        public bool IsOpen { get; private set; }
        public event EventHandler DataReceived;

        private readonly ConcurrentQueue<byte> _rxQueue = new ConcurrentQueue<byte>();
        private readonly DummyValueGenerator _generator = new DummyValueGenerator();

        public void Open() { IsOpen = true; }
        public void Close() { IsOpen = false; ClearRx(); }
        public void Dispose() { Close(); }

        public void Write(byte[] buffer)
        {
            Write(buffer, 0, buffer.Length);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (!IsOpen) return;
            if (buffer == null || count <= 0) return;

            var req = buffer.Skip(offset).Take(count).ToArray();
            if (req.Length < 3) return;

            byte cmd = req[2];
            byte[] rsp = null;

            switch (cmd)
            {
                case CMD_STATUS:
                    {
                        var sample = _generator.Next();
                        rsp = new byte[37];
                        DummyValueGenerator.PatchStatusResponse37(rsp, sample);
                    }
                    break;

                case CMD_ERROR_DATA:
                    rsp = _generator.BuildDummyErrorResponse();
                    break;

                case CMD_PARAMETER_READ:
                    rsp = _generator.BuildDummyParameterResponse74();
                    break;

                case CMD_PARAMETER_SET:
                    rsp = _generator.BuildDummyParameterWriteAck(0x00);
                    break;

                case CMD_ERROR_RESET:
                    rsp = null;
                    break;
            }

            if (rsp != null && rsp.Length > 0)
                EnqueueAndFire(rsp);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (!IsOpen) return 0;
            if (buffer == null || count <= 0) return 0;

            int read = 0;
            while (read < count)
            {
                byte b;
                if (_rxQueue.TryDequeue(out b))
                {
                    buffer[offset + read] = b;
                    read++;
                }
                else
                {
                    break;
                }
            }
            return read;
        }

        private void EnqueueAndFire(byte[] data)
        {
            foreach (var b in data)
                _rxQueue.Enqueue(b);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(10);
                var handler = DataReceived;
                if (handler != null) handler(this, EventArgs.Empty);
            });
        }

        private void ClearRx()
        {
            byte tmp;
            while (_rxQueue.TryDequeue(out tmp)) { }
        }
    }
}
