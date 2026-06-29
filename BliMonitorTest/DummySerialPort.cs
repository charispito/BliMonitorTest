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
        private const byte CMD_PARAMETER_SET = 0x96;

        private const int STATUS_RESPONSE_SIZE = 37;

        public bool IsOpen { get; private set; }
        public event EventHandler DataReceived;

        private readonly ConcurrentQueue<byte> _rxQueue = new ConcurrentQueue<byte>();
        private readonly DummyValueGenerator _generator = new DummyValueGenerator();

        public void Open() { IsOpen = true; }
        public void Close() { IsOpen = false; ClearRx(); }
        public void Dispose() { Close(); }

        public static readonly byte[] REQ_ErrorData = new byte[] { 0x12, 0x01, 0xB9, 0x07, 0x00, 0x40, 0x34 };
        public static readonly byte[] REQ_ErrorReset = new byte[] { 0x12, 0x01, 0xB6, 0x07, 0x00, 0x4F, 0x34 };
        public static readonly byte[] REQ_StartStopStatus = new byte[] { 0x12, 0x01, 0xA0, 0x07, 0x00, 0x59, 0x34 };
        public static readonly byte[] REQ_ParameterRequest = new byte[] { 0x12, 0x01, 0xA0, 0x07, 0x00, 0x59, 0x34 };

        public static readonly byte[] REQ_ParameterSet = new byte[] {
            0x12, 0x01, 0x96, 0x46, 0x28, 0x1C, 0x28, 0x1C, 0x7D, 0x00,
            0x5F, 0x32, 0x28, 0x00, 0x32, 0x05, 0x32, 0x05, 0x7D, 0x41,
            0x5F, 0x78, 0x46, 0x00, 0x55, 0x05, 0x55, 0x05, 0x82, 0x41,
            0x5F, 0xA0, 0x00, 0x00, 0x46, 0x02, 0x46, 0x02, 0x82, 0x43,
            0x5F, 0xB4, 0x00, 0x00, 0x46, 0x02, 0x46, 0x02, 0x82, 0x00,
            0x5F, 0xC8, 0x00, 0x00, 0x14, 0x08, 0x14, 0x0C, 0x28, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9B, 0x34
        };

        public byte[] GetRequestPacket(ProtocolKind kind)
        {
            switch (kind)
            {
                case ProtocolKind.ErrorDataRequest: return (byte[])REQ_ErrorData.Clone();
                case ProtocolKind.ErrorReset: return (byte[])REQ_ErrorReset.Clone();
                case ProtocolKind.StartStopStatus: return (byte[])REQ_StartStopStatus.Clone();
                case ProtocolKind.ParameterRequest: return (byte[])REQ_ParameterRequest.Clone();
                case ProtocolKind.ParameterSet: return (byte[])REQ_ParameterSet.Clone();
                default: return new byte[0];
            }
        }

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

            if (cmd == CMD_ERROR_DATA ||
                cmd == CMD_STATUS ||
                cmd == CMD_ERROR_RESET ||
                cmd == CMD_PARAMETER_SET)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(5);
                    var handler = DataReceived;
                    if (handler != null) handler(this, EventArgs.Empty);
                });
            }
        }

        public void SimulateExchange(ProtocolKind kind)
        {
            if (!IsOpen) return;

            byte[] rsp = new byte[0];

            if (kind == ProtocolKind.StartStopStatus || kind == ProtocolKind.ParameterRequest)
            {
                var sample = _generator.Next();
                rsp = new byte[STATUS_RESPONSE_SIZE];
                DummyValueGenerator.PatchStatusResponse37(rsp, sample);
            }
            else if (kind == ProtocolKind.ErrorDataRequest)
            {
                rsp = _generator.BuildDummyErrorResponse();
            }
            else if (kind == ProtocolKind.ErrorReset)
            {
                rsp = new byte[0];
            }

            if (rsp.Length > 0)
            {
                EnqueueAndFire(rsp);
            }
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
