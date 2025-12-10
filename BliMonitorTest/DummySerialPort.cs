using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

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
        public bool IsOpen { get; private set; }
        public event EventHandler DataReceived;

        private readonly ConcurrentQueue<byte> _rxQueue = new ConcurrentQueue<byte>();

        public void Open() { IsOpen = true; }
        public void Close() { IsOpen = false; ClearRx(); }
        public void Dispose() { Close(); }

        // 요청 패킷(PC→제품)
        public static readonly byte[] REQ_ErrorData = new byte[] { 0x12, 0x01, 0xB9, 0x07, 0x00, 0x40, 0x34 };
        public static readonly byte[] REQ_ErrorReset = new byte[] { 0x12, 0x01, 0XB6, 0x07, 0x00, 0x40, 0x34 };        // 자료 동일   // RES X
        public static readonly byte[] REQ_StartStopStatus = new byte[] { 0x12, 0x01, 0xA0, 0x07, 0x00, 0x59, 0x34 };   // [2] 0xA0: 상태 요청, 0xAA: START/STOP       ||    [4] 0x00:  상태 요청, 0x02:START, 0x04:STOP
        public static readonly byte[] REQ_ParameterRequest = new byte[] { 0x12, 0x01, 0xA0, 0x07, 0x00, 0x59, 0x34 };
        public static readonly byte[] REQ_ParameterSet = new byte[] {
            0x12, 0x01, 0x96, 0x46, 0x28, 0x1C, 0x28, 0x1C, 0x7D, 0x00,
            0x5F, 0x32, 0x28, 0x00, 0x32, 0x05, 0x32, 0x05, 0x7D, 0x41,
            0x5F, 0x78, 0x46, 0x00, 0x55, 0x05, 0x55, 0x05, 0x82, 0x41,
            0x5F, 0xA0, 0x00, 0x00, 0x46, 0x02, 0x46, 0x02, 0x82, 0x43,
            0x5F, 0xB4, 0x00, 0x00, 0x46, 0x02, 0x46, 0x02, 0x82, 0x00,
            0x5F, 0xC8, 0x00, 0x00, 0x14, 0x08, 0x14, 0x0C, 0x28, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9B, 0x34
        };      // RES X

        // 응답 패킷(제품→PC)
        public static readonly byte[] RSP_ErrorData = new byte[] {
            0x12, 0x01, 0xB9, 0x46, 0x50, 0x00, 0x00, 0xC7, 0x53, 0x00,
            0x00, 0x00, 0x00, 0x00, 0xD0, 0x00, 0x00, 0xC7, 0x53, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x53, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x50, 0x00, 0x00, 0xC7, 0x53, 0x00,
            0x00, 0x00, 0x00, 0x00, 0xD0, 0x00, 0x00, 0xC7, 0x53, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x42, 0x34
        };

        public static readonly byte[] RSP_StartStopStatus = new byte[] {
            0x12, 0x01, 0xAA, 0x39, 0x01, 0x08, 0xC7, 0x53, 0x00, 0x00,
            0x00, 0x32, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x73, 0x00, 0x00, 0x46,
            0x00, 0x14, 0x08, 0x14, 0x0C, 0x28, 0x5F, 0x28, 0x00, 0x00,
            0x1C, 0xAE, 0x00, 0x00, 0x01, 0x00, 0x09, 0x00, 0x08, 0xD0,
            0x00, 0x19, 0x09, 0x05, 0x01, 0xDD, 0x34
        };

        public static readonly byte[] RSP_ParameterRequest = new byte[] {
            0x12, 0x01, 0x99, 0x46, 0x28, 0x1C, 0x28, 0x1C, 0x7D, 0x00,
            0x5F, 0x32, 0x28, 0x00, 0x32, 0x05, 0x32, 0x05, 0x7D, 0x41,
            0x5F, 0x78, 0x46, 0x00, 0x55, 0x05, 0x55, 0x05, 0x82, 0x41,
            0x5F, 0xA0, 0x00, 0x00, 0x46, 0x02, 0x46, 0x02, 0x82, 0x43,
            0x5F, 0xB4, 0x00, 0x00, 0x46, 0x02, 0x46, 0x02, 0x82, 0x00,
            0x5F, 0xC8, 0x00, 0x00, 0x14, 0x08, 0x14, 0x0C, 0x28, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x6B, 0x34
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

        public void Write(byte[] buffer) { Write(buffer, 0, buffer.Length); }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (!IsOpen) return;
            var req = buffer.Skip(offset).Take(count).ToArray();
            byte cmd = req.Length >= 3 ? req[2] : (byte)0x00;

            byte[] rsp = new byte[0];

            if (cmd == 0xB9)
            {
                // ERROR_DATA 요청(응답 있음) vs ERROR 리셋(응답 없음) — 요청이 같다고 주셨으므로,
                // 상위에서 SimulateExchange로 구분하는 것을 권장. 기본은 응답 없음.
                if (SequenceEqual(req, REQ_ErrorData))
                {
                    // 여기서는 응답을 즉시 넣지 않고, 상위에서 SimulateExchange를 호출해 주입하는 방식을 사용.
                    rsp = new byte[0];
                }
            }
            else if (cmd == 0xA0)
            {
                // 상태요청/파라미터요청 — 동일 요청, 응답 선택은 SimulateExchange에서 처리
                rsp = new byte[0];
            }
            else if (cmd == 0x96)
            {
                // PARAMETER 설정 — 응답 없음
                rsp = new byte[0];
            }

            if (rsp.Length > 0)
            {
                EnqueueAndFire(rsp);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(5);
                    var handler = DataReceived;
                    if (handler != null) handler(this, EventArgs.Empty);
                });
            }
        }

        // A0, B9 등 요청 후 어떤 응답을 보낼지를 명시적으로 지정
        public void SimulateExchange(ProtocolKind kind)
        {
            if (!IsOpen) return;
            byte[] rsp = new byte[0];

            if (kind == ProtocolKind.StartStopStatus)
                rsp = RSP_StartStopStatus;
            else if (kind == ProtocolKind.ParameterRequest)
                rsp = RSP_ParameterRequest;
            else if (kind == ProtocolKind.ErrorDataRequest)
                rsp = RSP_ErrorData;
            else
                rsp = new byte[0];

            if (rsp.Length > 0) EnqueueAndFire(rsp);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (!IsOpen) return 0;
            int read = 0;
            while (read < count)
            {
                byte b;
                if (_rxQueue.TryDequeue(out b))
                {
                    buffer[offset + read] = b;
                    read++;
                }
                else break;
            }
            return read;
        }

        private void EnqueueAndFire(byte[] data)
        {
            foreach (var b in data) _rxQueue.Enqueue(b);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(10);
                var handler = DataReceived;
                if (handler != null) handler(this, EventArgs.Empty);
            });
        }

        private static bool SequenceEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private void ClearRx()
        {
            byte tmp;
            while (_rxQueue.TryDequeue(out tmp)) { }
        }
    }

}