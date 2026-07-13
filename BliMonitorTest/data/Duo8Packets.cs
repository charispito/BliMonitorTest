using System;
using System.Collections.Generic;
using System.Globalization;

namespace BliMonitorTest.data
{
    public sealed class Duo8StatusPacket
    {
        public byte[] Raw { get; set; }

        public byte StartPacket { get; set; }
        public byte Version { get; set; }
        public byte Command { get; set; }
        public byte Size { get; set; }

        public byte ModelCode { get; set; }
        public byte ErrorCode { get; set; }
        public byte WaterInitDone { get; set; }
        public byte WaterInitGo { get; set; }
        public byte EmptyDetect { get; set; }
        public byte BufferLow { get; set; }

        public byte PcbHwVersion { get; set; }
        public byte PcbSwVersion { get; set; }

        public byte HeaterPwm { get; set; }
        public byte Night { get; set; }
        public byte TestMode { get; set; }
        public byte ModeSelected { get; set; }
        public byte QtySelected { get; set; }
        public byte DispensePhase { get; set; }
        public byte DispenseSubPhase { get; set; }

        public ushort HotTempRaw { get; set; }
        public ushort ColdTempRaw { get; set; }

        public byte FloatLowStable { get; set; }
        public byte BallTopFullStable { get; set; }
        public byte WaterBufFullStable { get; set; }
        public byte HeaterOutput { get; set; }
        public byte CompressorOutput { get; set; }
        public byte HotValveOutput { get; set; }
        public byte ColdSelectOutput { get; set; }
        public byte OutletValveOutput { get; set; }

        public ushort ButtonInfo { get; set; }
        public byte StatusA { get; set; }
        public byte StatusB { get; set; }
        public byte Checksum { get; set; }
        public byte EndPacket { get; set; }

        public byte ReheatRunning => (byte)(((StatusB & 0x20) != 0) ? 1 : 0);
        public byte HotIng => (byte)(((StatusB & 0x40) != 0) ? 1 : 0);
    }

    public sealed class Duo8ErrorRecord
    {
        public byte ValidMark { get; set; }
        public byte Sequence { get; set; }
        public byte ErrorCode { get; set; }
        public ushort HotTempRaw { get; set; }
        public ushort ColdTempRaw { get; set; }
        public ushort AdcHotRaw { get; set; }
        public ushort AdcColdRaw { get; set; }
        public byte WaterInitDone { get; set; }
        public byte StatusA { get; set; }
        public byte StatusB { get; set; }
        public byte BufferLow { get; set; }
        public byte Crc { get; set; }

        public bool IsValid => ValidMark == 0xA5;
    }

    public sealed class Duo8ErrorResponse
    {
        public byte[] Raw { get; set; }
        public byte StartPacket { get; set; }
        public byte Version { get; set; }
        public byte Command { get; set; }
        public byte Size { get; set; }
        public List<Duo8ErrorRecord> Records { get; set; } = new List<Duo8ErrorRecord>();
        public byte Checksum { get; set; }
        public byte EndPacket { get; set; }
    }

    public sealed class Duo8ParameterPacket
    {
        public byte[] Raw { get; set; }
        public byte StartPacket { get; set; }
        public byte Version { get; set; }
        public byte Command { get; set; }
        public byte Size { get; set; }
        public List<ushort> Values { get; set; } = new List<ushort>();
        public byte Checksum { get; set; }
        public byte EndPacket { get; set; }
    }

    public sealed class Duo8ParameterWriteAck
    {
        public byte[] Raw { get; set; }
        public byte StartPacket { get; set; }
        public byte Version { get; set; }
        public byte Command { get; set; }
        public byte Size { get; set; }
        public byte Result { get; set; }
        public byte Reserved { get; set; }
        public byte Checksum { get; set; }
        public byte EndPacket { get; set; }
    }

    public static class Duo8PacketParser
    {
        public static Duo8StatusPacket ParseStatus(byte[] buf)
        {
            if (buf == null || buf.Length != 37) return null;
            if (buf[0] != 0x12 || buf[1] != 0x01 || buf[2] != 0xA0 || buf[36] != 0x34) return null;
            if (buf[3] != 37) return null;

            byte checksum = CalcXorChecksum(buf, 1, 34);
            if (checksum != buf[35]) return null;

            return new Duo8StatusPacket
            {
                Raw = buf,
                StartPacket = buf[0],
                Version = buf[1],
                Command = buf[2],
                Size = buf[3],

                ModelCode = buf[4],
                ErrorCode = buf[5],
                WaterInitDone = buf[6],
                WaterInitGo = buf[7],
                EmptyDetect = buf[8],
                BufferLow = buf[9],
                PcbHwVersion = buf[10],
                PcbSwVersion = buf[11],
                HeaterPwm = buf[12],
                Night = buf[13],
                TestMode = buf[14],
                ModeSelected = buf[15],
                QtySelected = buf[16],
                DispensePhase = buf[17],
                DispenseSubPhase = buf[18],

                HotTempRaw = (ushort)(buf[19] | (buf[20] << 8)),
                ColdTempRaw = (ushort)(buf[21] | (buf[22] << 8)),

                FloatLowStable = buf[23],
                BallTopFullStable = buf[24],
                WaterBufFullStable = buf[25],
                HeaterOutput = buf[26],
                CompressorOutput = buf[27],
                HotValveOutput = buf[28],
                ColdSelectOutput = buf[29],
                OutletValveOutput = buf[30],

                ButtonInfo = (ushort)(buf[31] | (buf[32] << 8)),
                StatusA = buf[33],
                StatusB = buf[34],
                Checksum = buf[35],
                EndPacket = buf[36]
            };
        }

        public static Duo8ErrorResponse ParseErrorResponse(byte[] buf)
        {
            if (buf == null || buf.Length != 134) return null;
            if (buf[0] != 0x12 || buf[1] != 0x01 || buf[2] != 0xB9 || buf[133] != 0x34) return null;
            if (buf[3] != 134) return null;

            byte checksum = CalcXorChecksum(buf, 1, 131);
            if (checksum != buf[132]) return null;

            var result = new Duo8ErrorResponse
            {
                Raw = buf,
                StartPacket = buf[0],
                Version = buf[1],
                Command = buf[2],
                Size = buf[3],
                Checksum = buf[132],
                EndPacket = buf[133]
            };

            for (int i = 0; i < 8; i++)
            {
                int baseIdx = 4 + (i * 16);

                result.Records.Add(new Duo8ErrorRecord
                {
                    ValidMark = buf[baseIdx + 0],
                    Sequence = buf[baseIdx + 1],
                    ErrorCode = buf[baseIdx + 2],
                    HotTempRaw = (ushort)(buf[baseIdx + 3] | (buf[baseIdx + 4] << 8)),
                    ColdTempRaw = (ushort)(buf[baseIdx + 5] | (buf[baseIdx + 6] << 8)),
                    AdcHotRaw = (ushort)(buf[baseIdx + 7] | (buf[baseIdx + 8] << 8)),
                    AdcColdRaw = (ushort)(buf[baseIdx + 9] | (buf[baseIdx + 10] << 8)),
                    WaterInitDone = buf[baseIdx + 11],
                    StatusA = buf[baseIdx + 12],
                    StatusB = buf[baseIdx + 13],
                    BufferLow = buf[baseIdx + 14],
                    Crc = buf[baseIdx + 15]
                });
            }

            return result;
        }

        public static Duo8ParameterPacket ParseParameterResponse(byte[] buf)
        {
            if (buf == null || buf.Length != 76) return null;
            if (buf[0] != 0x12 || buf[1] != 0x01 || buf[2] != 0xC1 || buf[75] != 0x34) return null;
            if (buf[3] != 76) return null;

            byte checksum = CalcXorChecksum(buf, 1, 73);
            if (checksum != buf[74]) return null;

            var result = new Duo8ParameterPacket
            {
                Raw = buf,
                StartPacket = buf[0],
                Version = buf[1],
                Command = buf[2],
                Size = buf[3],
                Checksum = buf[74],
                EndPacket = buf[75]
            };

            for (int i = 4; i <= 73; i += 2)
            {
                ushort v = (ushort)(buf[i] | (buf[i + 1] << 8));
                result.Values.Add(v);
            }

            return result;
        }

        public static Duo8ParameterWriteAck ParseParameterWriteAck(byte[] buf)
        {
            if (buf == null || buf.Length != 8) return null;
            if (buf[0] != 0x12 || buf[1] != 0x01 || buf[2] != 0xC2 || buf[7] != 0x34) return null;
            if (buf[3] != 8) return null;

            byte checksum = CalcXorChecksum(buf, 1, 5);
            if (checksum != buf[6]) return null;

            return new Duo8ParameterWriteAck
            {
                Raw = buf,
                StartPacket = buf[0],
                Version = buf[1],
                Command = buf[2],
                Size = buf[3],
                Result = buf[4],
                Reserved = buf[5],
                Checksum = buf[6],
                EndPacket = buf[7]
            };
        }

        private static byte CalcXorChecksum(byte[] buf, int start, int endInclusive)
        {
            byte value = 0x00;
            for (int i = start; i <= endInclusive; i++)
                value ^= buf[i];
            value ^= 0xFF;
            return value;
        }
    }


    public static class Duo8ValueText
    {
        public static string GetModelName(int model)
        {
            switch (model)
            {
                case 0: return "BSH-311";
                case 1: return "BSS-311";
                case 2: return "BSS-314";
                case 3: return "BSS-310";
                case 4: return "BSS-330";
                case 5: return "BSS-341";
                case 6: return "DEWO8";
                case 7: return "HYBRID";
                default: return "UNKNOWN(" + model + ")";
            }
        }

        public static string GetSourceTypeText(int sourceType)
        {
            switch (sourceType)
            {
                case 1: return "단일";
                case 2: return "다채널";
                default: return "전체/미정(" + sourceType + ")";
            }
        }

        public static string GetValidMarkText(int value)
        {
            if (value == 1 || value == 0xA5) return "유효(A5)";
            if (value == 0) return "무효(00)";
            return "UNKNOWN(" + value + ")";
        }

        public static string GetErrorText(byte errorCode)
        {
            if (errorCode == 0x00) return "NONE";

            List<string> list = new List<string>();
            if ((errorCode & 0x01) != 0) list.Add("COLD_ERR1");
            if ((errorCode & 0x02) != 0) list.Add("COLD_ERR2");
            if ((errorCode & 0x04) != 0) list.Add("HOT_ERR1");
            if ((errorCode & 0x08) != 0) list.Add("HOT_ERR2");
            if ((errorCode & 0x10) != 0) list.Add("HOT_ERR3");

            return list.Count == 0 ? "UNKNOWN(0x" + errorCode.ToString("X2") + ")" : string.Join(", ", list);
        }

        public static string GetModeText(byte mode)
        {
            switch (mode)
            {
                case 0: return "NONE";
                case 1: return "HOT";
                case 2: return "NORMAL";
                case 3: return "HOT_ECO(LEGACY)";
                case 4: return "COLD_ECO(LEGACY)";
                case 5: return "COLD";
                case 6: return "WARM";
                case 7: return "COOL";
                default: return "UNKNOWN(" + mode + ")";
            }
        }

        public static string GetQtyText(byte qty)
        {
            switch (qty)
            {
                case 0: return "NONE";
                case 1: return "150mL";
                case 2: return "1000mL";
                default: return "UNKNOWN(" + qty + ")";
            }
        }

        public static string GetDispensePhaseText(byte phase)
        {
            switch (phase)
            {
                case 0: return "STOP";
                case 1: return "CONT";
                case 2: return "QTY";
                default: return "UNKNOWN(" + phase + ")";
            }
        }

        public static string GetDispenseSubPhaseText(byte sub)
        {
            switch (sub)
            {
                case 0: return "IDLE";
                case 1: return "PREFLOW";
                case 2: return "RUNNING";
                default: return "UNKNOWN(" + sub + ")";
            }
        }

        public static string ToDoneText(byte value)
        {
            return value != 0 ? "완료" : "미완료";
        }

        public static string ToRunText(byte value)
        {
            return value != 0 ? "동작" : "정지";
        }

        public static string ToActiveInactive(byte value)
        {
            return value != 0 ? "활성" : "비활성";
        }

        public static string FormatRawUShort(ushort value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string DecodeButtonInfo(ushort value)
        {
            List<string> list = new List<string>();

            if ((value & 0x0001) != 0) list.Add("HOT 선택");
            if ((value & 0x0002) != 0) list.Add("WARM 선택");
            if ((value & 0x0004) != 0) list.Add("NORMAL 선택");
            if ((value & 0x0008) != 0) list.Add("COOL 선택");
            if ((value & 0x0010) != 0) list.Add("COLD 선택");
            if ((value & 0x0020) != 0) list.Add("REHEAT 상태");
            if ((value & 0x0040) != 0) list.Add("150mL 선택");
            if ((value & 0x0080) != 0) list.Add("1000mL 선택");
            if ((value & 0x0100) != 0) list.Add("출수 동작");

            return list.Count == 0 ? "-" : string.Join(", ", list);
        }

        public static string DecodeStatusA(byte value)
        {
            List<string> list = new List<string>();

            if ((value & 0x01) != 0) list.Add("히터 동작");
            if ((value & 0x02) != 0) list.Add("컴프레서 동작");
            if ((value & 0x04) != 0) list.Add("온수 밸브 사용");
            if ((value & 0x08) != 0) list.Add("냉수 선택 밸브 사용");
            if ((value & 0x10) != 0) list.Add("출수 밸브 사용");
            if ((value & 0x20) != 0) list.Add("출수 펌프 사용");
            if ((value & 0x40) != 0) list.Add("다이어프램 펌프 사용");
            if ((value & 0x80) != 0) list.Add("에어벤트 펌프 사용");

            return list.Count == 0 ? "-" : string.Join(", ", list);
        }

        public static string DecodeStatusB(byte value)
        {
            List<string> list = new List<string>();

            if ((value & 0x01) != 0) list.Add("플로트 센서 활성");
            if ((value & 0x02) != 0) list.Add("볼탑 센서 활성");
            if ((value & 0x04) != 0) list.Add("버퍼 수위 센서 활성");
            if ((value & 0x08) != 0) list.Add("물 부족 감지");
            if ((value & 0x10) != 0) list.Add("버퍼 수위 부족");
            if ((value & 0x20) != 0) list.Add("재가열 동작");
            if ((value & 0x40) != 0) list.Add("가열 진행");
            if ((value & 0x80) != 0) list.Add("출수 진행");

            return list.Count == 0 ? "-" : string.Join(", ", list);
        }

        public static string ToYesNo(byte value) => value != 0 ? "YES" : "NO";
        public static string ToOnOff(byte value) => value != 0 ? "ON" : "OFF";
        public static string GetBitState(bool state) => state ? "ON" : "OFF";

        public static string FormatTempX10(ushort value)
        {
            return (value / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + "°C";
        }
    }
}
