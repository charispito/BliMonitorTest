using System;
using System.Collections.Generic;
using BliMonitorTest.data;

namespace BliMonitorTest
{
    public partial class OneChannelWindow
    {
        private readonly object _rxLock = new object();
        private readonly List<byte> _rxBuffer = new List<byte>(4096);

        private const int MIN_FRAME_LEN = 7;
        private const int MAX_FRAME_LEN = 200;

        private static readonly HashSet<byte> _allowedCmd = new HashSet<byte>
        {
            Protocol.STATUS,
            Protocol.ERROR_READ,
            Protocol.ERROR_RESET,
            Protocol.PARAMETER_READ,
            Protocol.PARAMETER_WRITE
        };

        private void receiveData(byte[] data, int length)
        {
            if (data == null || length <= 0) return;

            byte[] chunk = new byte[length];
            Buffer.BlockCopy(data, 0, chunk, 0, length);

            List<byte[]> frames;

            lock (_rxLock)
            {
                _rxBuffer.AddRange(chunk);
                frames = ExtractFramesFromBuffer(_rxBuffer);
            }

            foreach (var frame in frames)
            {
                CheckCommand(frame);
            }
        }

        private List<byte[]> ExtractFramesFromBuffer(List<byte> buf)
        {
            byte stx = 0x12;
            byte ver = 0x01;
            byte etx = 0x34;

            var frames = new List<byte[]>();

            while (true)
            {
                int stxPos = buf.IndexOf(stx);
                if (stxPos < 0)
                {
                    buf.Clear();
                    break;
                }

                if (stxPos > 0)
                    buf.RemoveRange(0, stxPos);

                if (buf.Count < 4)
                    break;

                if (buf[1] != ver)
                {
                    buf.RemoveAt(0);
                    continue;
                }

                byte cmd = buf[2];
                System.Diagnostics.Debug.WriteLine($"RX CMD=0x{cmd:X2}, size={buf[3]}, allowed={_allowedCmd.Contains(cmd)}");

                if (!_allowedCmd.Contains(cmd))
                {
                    buf.RemoveAt(0);
                    continue;
                }

                int size = buf[3];
                if (size < MIN_FRAME_LEN || size > MAX_FRAME_LEN)
                {
                    buf.RemoveAt(0);
                    continue;
                }

                if (buf.Count < size)
                    break;

                if (buf[size - 1] != etx)
                {
                    buf.RemoveAt(0);
                    continue;
                }

                byte[] frame = buf.GetRange(0, size).ToArray();
                frames.Add(frame);
                buf.RemoveRange(0, size);
            }

            return frames;
        }
    }
}
