using System;

namespace BliMonitorTest.data
{
    public class Protocol
    {
        public const byte STX = 0x12;
        public const byte ETX = 0x34;
        public const byte VERSION = 0x01;

        public const byte STATUS = 0xA0;
        public const byte ERROR_READ = 0xB9;
        public const byte ERROR_RESET = 0xB6;

        public static byte[] GetStatusRequest()
        {
            return BuildCommand(STATUS, 0x00);
        }

        public static byte[] GetError()
        {
            return BuildCommand(ERROR_READ, 0x00);
        }

        public static byte[] GetErrorReset()
        {
            return BuildCommand(ERROR_RESET, 0x00);
        }

        // 구형 코드 호환용: 현재 제품에서는 파라미터 요청 미사용
        public static byte[] GetParameter()
        {
            return BuildCommand(STATUS, 0x00);
        }

        public static byte[] BuildCommand(byte cmd, byte option)
        {
            byte[] command = new byte[7];
            command[0] = STX;
            command[1] = VERSION;
            command[2] = cmd;
            command[3] = 0x07;
            command[4] = option;
            command[5] = CalcChecksum(command, 1, 4);
            command[6] = ETX;
            return command;
        }

        public static byte CalcChecksum(byte[] array, int start, int endInclusive)
        {
            byte res = 0x00;
            for (int i = start; i <= endInclusive; i++)
            {
                res ^= array[i];
            }
            res ^= 0xFF;
            return res;
        }

        // 구형 코드 호환용
        public static byte GetCheckSum(byte[] array, int start, int endInclusive)
        {
            return CalcChecksum(array, start, endInclusive);
        }
    }
}
