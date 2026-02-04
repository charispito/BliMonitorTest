using System;

namespace BliMonitorTest.ProtocolDuo
{
    public static class DuoV1_2
    {
        // 공통
        public const byte STX = 0x12;
        public const byte FIX_01 = 0x01;
        public const byte ETX = 0x34;

        // CMD
        public const byte CMD_ERROR_REQ = 0xB9;
        public const byte CMD_ERROR_RESET = 0xB6;
        public const byte CMD_STATUS_REQ = 0xA0;
        public const byte CMD_PARAM_REQ = 0x99;
        public const byte CMD_PARAM_SET = 0x96;

        // SIZE
        public const byte SIZE_REQ_07 = 0x07;

        // REQ Builders
        public static byte[] BuildErrorRequest(byte mode)
        {
            var b = new byte[7];
            b[0] = STX; b[1] = FIX_01; b[2] = CMD_ERROR_REQ; b[3] = SIZE_REQ_07; b[4] = mode; // 0x00
            b[5] = CalcChecksum(b[1], b[2], b[3], b[4]); b[6] = ETX;
            return b;
        }

        public static byte[] BuildErrorReset()
        {
            var b = new byte[7];
            b[0] = STX; b[1] = FIX_01; b[2] = CMD_ERROR_RESET; b[3] = SIZE_REQ_07; b[4] = 0x00;
            b[5] = CalcChecksum(b[1], b[2], b[3], b[4]); b[6] = ETX;
            return b;
        }

        public static byte[] BuildStatusRequest()
        {
            var b = new byte[7];
            b[0] = STX; b[1] = FIX_01; b[2] = CMD_STATUS_REQ; b[3] = SIZE_REQ_07; b[4] = 0x00;
            b[5] = CalcChecksum(b[1], b[2], b[3], b[4]); b[6] = ETX;
            return b;
        }

        public static byte[] BuildParamRequest()
        {
            var b = new byte[7];
            b[0] = STX; b[1] = FIX_01; b[2] = CMD_PARAM_REQ; b[3] = SIZE_REQ_07; b[4] = 0x00;
            b[5] = CalcChecksum(b[1], b[2], b[3], b[4]); b[6] = ETX;
            return b;
        }

        // PARAMETER 설정 프레임: 전체 70바이트. UI에서 4..54 영역을 채워 전달.
        public static byte[] BuildParamSet(byte[] full70)
        {
            if (full70 == null || full70.Length != 70) throw new ArgumentException("full70 must be 70 bytes");
            var b = new byte[70];
            b[0] = STX; b[1] = FIX_01; b[2] = CMD_PARAM_SET; b[3] = 70;
            // 4..67 복사(전달된 full70을 그대로 사용)
            for (int i = 4; i <= 67; i++) b[i] = full70[i];
            b[68] = CalcChecksumRange(b, 1, 67);
            b[69] = ETX;
            return b;
        }

        // 체크섬
        public static byte CalcChecksum(byte b1, byte b2, byte b3, byte b4)
        {
            byte x = 0x00;
            x ^= b1; x ^= b2; x ^= b3; x ^= b4;
            x ^= 0xFF;
            return x;
        }

        public static byte CalcChecksumRange(byte[] frame, int start, int endInclusive)
        {
            byte x = 0x00;
            for (int i = start; i <= endInclusive; i++) x ^= frame[i];
            x ^= 0xFF;
            return x;
        }

        public static bool ValidateHeader(byte[] frame)
        {
            return frame != null && frame.Length > 3 && frame[0] == STX && frame[1] == FIX_01;
        }

        public static bool ValidateFooter(byte[] frame)
        {
            return frame != null && frame.Length >= 2 && frame[frame.Length - 1] == ETX;
        }
    }
}
