using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCaraTest.util
{
    public static class Extension
    {
        public static void AppendLine(this StringBuilder builder, string str, bool app)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine(str);
            }
            else
            {
                builder.Append(str);
            }
        }
        public static bool IsEmpty(this string str)
        {
            if (str == null || str.Length == 0)
                return true;
            else
                return false;
        }

        public static void addValue(this KeyValuePair<double, int>?[] arr, KeyValuePair<double, int>? value)
        {
            int index = arr.Count(i => i.HasValue);
            Console.WriteLine("index = {0}, size = {1}", index, arr.Length);
            if(index < arr.Length)
            {
                arr[index] = value;
            }            
        }

        public static void PrintHex(this byte[] arr)
        {
            string hex = "";
            foreach (byte b in arr)
            {
                hex += " " + b.ToString("X2");
            }
            Console.WriteLine(hex);
        }
        public static void PrintInt(this int[] arr)
        {
            string hex = "";
            foreach (int i in arr)
            {
                hex += " " + i.ToString();
            }
            Console.WriteLine(hex);
        }

        public static void PrintHex(this byte[] arr, int type)
        {
            string hex = "";
            foreach (byte b in arr)
            {
                hex += " " + b.ToString("X2");
            }
            switch (type)
            {
                case 0:
                    Console.WriteLine(hex);
                    break;
                case 1:
                    Console.WriteLine("Length: {0}, Data: {1}",arr.Length, hex);
                    break;
                case 2:
                    Console.WriteLine("[{0}] Length: {1}, Data: {2}",DateTime.Now.ToString("HH:mm:ss:ff"), arr.Length, hex);
                    break;
            }
        }

        public static string byteToString(this byte[] arr)
        {
            string hex = "";
            foreach (byte b in arr)
            {
                hex += " " + b.ToString("X2");
            }
            return hex;
        }

        public static byte[] Slice(this byte[] arr, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(arr, 0, result, 0, length);
            return result;
        }
        public static byte[] Slice(this byte[] arr, int startIndex, int length)
        {
            byte[] result = new byte[length];
            if(startIndex >= arr.Length)
            {
                return null;
            }
            Buffer.BlockCopy(arr, startIndex, result, 0, length);
            return result;
        }
        public static string Clear(this string str)
        {
            if (str != null)
                str = str.Remove(str.Length - 1);
            else
                str = "";
            return str;
        }
    }
}
