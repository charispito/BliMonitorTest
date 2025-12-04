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

        public static string ToHex(byte[] data)
        {
            if (data == null) return "<null>";
            var sb = new System.Text.StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("X2"));
                if (i < data.Length - 1) sb.Append(' ');
            }
            return sb.ToString();
        }

        public static string ToAscii(byte[] data)
        {
            if (data == null) return "<null>";
            var sb = new System.Text.StringBuilder(data.Length);
            foreach (var b in data)
            {
                char c = (b >= 32 && b <= 126) ? (char)b : '.';
                sb.Append(c);
            }
            return sb.ToString();
        }

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
                sb.AppendFormat("{0:X4}  {1}  |{2}|\n", offset, hex, ascii);
            }
            return sb.ToString();
        }

        public static void LogPacket(byte[] data, string source)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss.fff");

            Console.WriteLine($"[{ts}] [{source}] len={data?.Length ?? 0}");
            Console.WriteLine(DumpLines(data));
            log.Debug($"[{ts}] [{source}] len={data?.Length ?? 0}");
            log.Debug(DumpLines(data));
        }
    }

}
