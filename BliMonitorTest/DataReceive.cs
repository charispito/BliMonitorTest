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
                frames = ExtractFramesFromBuffer(_rxBuffer, channel.IsNewVersion);
            }

            // 프레임 단위로 처리 (UI 업데이트는 CheckCommand 내부에서 Dispatcher 사용)
            foreach (var frame in frames)
            {
                CheckCommand(frame);
            }
        }


        /*
        private void receiveData(byte[] data, int Length)
        {
            log.Debug("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
            log.Debug($"[receiveData] len={Length} start={data[0]:X2} cmd={data[2]:X2} size={data[3]}");
            log.Debug($"[receiveData] len={data.Length} start={data[0]:X2} v={data[1]:X2} cmd={data[2]:X2} size={data[3]} end={data[data.Length - 1]:X2}");
            log.Debug($"[receiveData] IsNewVersion={channel.IsNewVersion} parameterCnt={parameterCnt} paramBufCount={parameterReceived.Count} rxBufCount={receivedData.Count}");
            log.Debug("$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");

            if (channel.IsNewVersion)
            {
                if (Length > 0)
                {
                    byte[] buffer = data.Slice(Length);
                    //buffer.PrintHex(1);
                    if (parameterCnt > 0)
                    {
                        parameterReceived.AddRange(buffer);

                        if (parameterReceived.Count >= 70)
                        {
                            byte[] receive = parameterReceived.ToArray();
                            int s_idx = getNewStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if (receive.Length > 70)
                                {
                                    byte[] cmd = receive.Slice(70);
                                    byte[] etc = receive.Slice(70, parameterReceived.Count - 70);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    parameterReceived.Clear();
                                    CheckCommand(receive);
                                }
                            }
                            else
                            {
                                if (receive[s_idx - 1] == 0x34)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if (command.Length == 70)
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    else
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(command);
                                }
                            }
                        }
                    }
                    else
                    {
                        receivedData.AddRange(buffer);
                        if (receivedData.Count >= 57)
                        {
                            byte[] receive = receivedData.ToArray();
                            int s_idx = getNewStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if (receive.Length > 57)
                                {
                                    byte[] cmd = receive.Slice(57);
                                    byte[] etc = receive.Slice(57, receivedData.Count - 57);
                                    receivedData.Clear();
                                    receivedData.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    receivedData.Clear();
                                    CheckCommand(receive);
                                }
                            }
                            else
                            {
                                if (receive[s_idx - 1] == 0x34)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if (command.Length == 57)
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    else
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    receivedData.Clear();
                                    receivedData.AddRange(command);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (Length > 0)
                {
                    byte[] buffer = data.Slice(Length);
                    if (parameterCnt > 0)
                    {
                        parameterReceived.AddRange(buffer);

                        if (parameterReceived.Count >= 70)
                        {
                            byte[] receive = parameterReceived.ToArray();
                            int s_idx = getStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if (receive.Length > 70)
                                {
                                    byte[] cmd = receive.Slice(70);
                                    byte[] etc = receive.Slice(70, parameterReceived.Count - 70);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    parameterReceived.Clear();
                                    CheckCommand(receive);
                                }
                            }
                            else
                            {
                                if (receive[s_idx - 1] == 0xEF)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if (command.Length == 70)
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    else
                                    {
                                        parameterReceived.Clear();
                                        parameterReceived.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    parameterReceived.Clear();
                                    parameterReceived.AddRange(command);
                                }
                            }
                        }
                    }
                    else
                    {
                        receivedData.AddRange(buffer);
                        if (receivedData.Count >= 57)
                        {
                            byte[] receive = receivedData.ToArray();
                            int s_idx = getStxIndex(receive);
                            if (s_idx == 0)
                            {
                                if (receive.Length > 57)
                                {
                                    byte[] cmd = receive.Slice(57);
                                    byte[] etc = receive.Slice(57, receivedData.Count - 57);
                                    receivedData.Clear();
                                    receivedData.AddRange(etc);
                                    CheckCommand(cmd);
                                }
                                else
                                {
                                    receivedData.Clear();
                                    CheckCommand(receive);
                                }
                            }
                            else
                            {
                                if (receive[s_idx - 1] == 0xEF)
                                {
                                    byte[] command = receive.Slice(s_idx);
                                    byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);
                                    if (command.Length == 57)
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    else
                                    {
                                        receivedData.Clear();
                                        receivedData.AddRange(etc);
                                    }
                                    CheckCommand(command);
                                }
                                else
                                {
                                    byte[] command = receive.Slice(s_idx, receive.Length - s_idx);
                                    receivedData.Clear();
                                    receivedData.AddRange(command);
                                }
                            }
                        }
                    }
                }
            }
        }
        */

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
