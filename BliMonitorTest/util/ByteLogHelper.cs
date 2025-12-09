using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.util
{
    public static class ByteLogHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ByteLogHelper));

        // 기존: 공백 구분 X2 헥사(예: "12 01 AA ...")
        public static string ToHex(byte[] data)
        {
            if (data == null) return "<null>";
            var sb = new System.Text.StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("X2"));
                if (i < data.Length - 1) sb.Append(' ');
            }
            log.Debug(sb.ToString());
            return sb.ToString();
        }

        // 기존: ASCII 표시(0x20~0x7E만 문자, 그 외 '.')
        public static string ToAscii(byte[] data)
        {
            if (data == null) return "<null>";
            var sb = new System.Text.StringBuilder(data.Length);
            foreach (var b in data)
            {
                char c = (b >= 32 && b <= 126) ? (char)b : '.';
                sb.Append(c);
            }
            log.Debug(sb.ToString());
            return sb.ToString();
        }

        // 기존: 줄바꿈 단위 덤프(기본 16바이트)
        public static string DumpLines(byte[] data, int bytesPerLine = 16)
        {
            if (data == null) return "<null>";
            var sb = new System.Text.StringBuilder();
            for (int offset = 0; offset < data.Length; offset += bytesPerLine)
            {
                int count = Math.Min(bytesPerLine, data.Length - offset);
                var slice = new byte[count];
                Buffer.BlockCopy(data, offset, slice, 0, count);

                string hex = ToHex(slice).PadRight(bytesPerLine * 3 - 1);
                string ascii = ToAscii(slice);
                sb.AppendFormat("{0,-4}  {1}  |{2}|\n", offset, hex, ascii);
            }
            log.Debug(sb.ToString());
            return sb.ToString();
        }

        // 기존: 콘솔/로그로 한 줄 요약 + 줄바꿈 덤프
        public static void LogPacket(byte[] data, string source)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{ts}] [{source}] len={data?.Length ?? 0}");
            Console.WriteLine(DumpLines(data));
            log.Debug($"[{ts}] [{source}] len={data?.Length ?? 0}");
            log.Debug(DumpLines(data));
        }

        // ===================== 여기서부터 신규 0x 접두어 함수 추가 =====================

        // 신규: 0x 접두어 한 줄 출력(쉼표 구분) → "0x12, 0x01, 0xAA, ..."
        public static string ToHexWith0x(byte[] data)
        {
            if (data == null || data.Length == 0) return "<null>";
            var sb = new System.Text.StringBuilder(data.Length * 6);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append("0x");
                sb.Append(data[i].ToString("X2"));
                if (i < data.Length - 1) sb.Append(", ");
            }
            log.Debug(sb.ToString());
            return sb.ToString();
        }

        // 신규: 0x 접두어 줄바꿈 덤프(기본 16바이트마다 줄바꿈)
        public static string DumpLinesWith0x(byte[] data, int bytesPerLine = 16)
        {
            if (data == null || data.Length == 0) return "<null>";
            var sb = new System.Text.StringBuilder();
            for (int offset = 0; offset < data.Length; offset += bytesPerLine)
            {
                int count = Math.Min(bytesPerLine, data.Length - offset);
                for (int i = 0; i < count; i++)
                {
                    sb.Append("0x");
                    sb.Append(data[offset + i].ToString("X2"));
                    if (i < count - 1) sb.Append(", ");
                }
                if (offset + count < data.Length) sb.AppendLine();
            }
            log.Debug(sb.ToString());
            return sb.ToString();
        }

        // 신규: C# 배열 리터럴로 바로 복붙 → "new byte[] { 0x12, 0x01, ... }"
        public static string ToCSharpByteArrayLiteral(byte[] data, int bytesPerLine = 16)
        {
            if (data == null || data.Length == 0) return "new byte[] { }";
            var sb = new System.Text.StringBuilder();
            sb.Append("new byte[] {\n    ");

            int col = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append("0x");
                sb.Append(data[i].ToString("X2"));
                if (i < data.Length - 1) sb.Append(", ");

                col++;
                if (bytesPerLine > 0 && col >= bytesPerLine && i < data.Length - 1)
                {
                    sb.Append("\n    ");
                    col = 0;
                }
            }
            sb.Append("\n}");
            log.Debug(sb.ToString());
            return sb.ToString();
        }
    }


}
