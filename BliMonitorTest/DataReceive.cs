using BliMonitorTest.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest
{
    public partial class OneChannelWindow
    {
        // ===== NEW: RX framing buffer (공통) =====
        private readonly object _rxLock = new object();
        private readonly List<byte> _rxBuffer = new List<byte>(4096);

        // SIZE sanity 범위 (너무 작거나 큰 값은 노이즈로 간주)
        private const int MIN_FRAME_LEN = 7;
        private const int MAX_FRAME_LEN = 200;

        // 허용 CMD (프로토콜에 맞게 필요 시 추가)
        private static readonly HashSet<byte> _allowedCmd = new HashSet<byte>
        {
            0x99, 0xB9, 0xA0, 0xAA
        };

        private void receiveData(byte[] data, int Length)
        {
            // Length가 "유효 길이(새로 읽힌 바이트 수)"라는 전제 그대로 유지
            if (data == null || Length <= 0) return;

            byte[] chunk = new byte[Length];
            Buffer.BlockCopy(data, 0, chunk, 0, Length);

            List<byte[]> frames;

            lock (_rxLock)
            {
                _rxBuffer.AddRange(chunk);
                frames = ExtractFramesFromBuffer(_rxBuffer);
            }

            // 프레임 단위로 처리 (UI 업데이트는 CheckCommand 내부에서 Dispatcher 사용)
            foreach (var frame in frames)
            {
                CheckCommand(frame);
            }
        }

        private int getStxIndex(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0xCC)
                {
                    if (i + 2 < data.Length)
                    {
                        if (data[i + 1] == 0x00)
                        {
                            if (data[i + 2] == 0x99 || data[i + 2] == 0xB9 || data[i + 2] == 0xA0 || data[i + 2] == 0xAA)
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            return -1;
        }

        private int getNewStxIndex(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x12)
                {
                    if (i + 2 < data.Length)
                    {
                        if (data[i + 1] == 0x01)
                        {
                            if (data[i + 2] == 0x99 || data[i + 2] == 0xB9 || data[i + 2] == 0xA0 || data[i + 2] == 0xAA)
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            return -1;
        }
    }
}
