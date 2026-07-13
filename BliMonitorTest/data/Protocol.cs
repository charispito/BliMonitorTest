using System;
using System.Collections.Generic;

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
        public const byte PARAMETER_READ = 0xC1;
        public const byte PARAMETER_WRITE = 0xC2;

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

        public static byte[] GetParameterReadRequest()
        {
            return BuildCommand(PARAMETER_READ, 0x00);
        }

        public static byte[] GetParameterWriteRequest(List<ushort> values)
        {
            if (values == null || values.Count != 35)
                throw new ArgumentException("PARAMETER WRITE requires 35 ushort values.");

            byte[] command = new byte[76];
            command[0] = STX;
            command[1] = VERSION;
            command[2] = PARAMETER_WRITE;
            command[3] = 76;

            int index = 4;
            for (int i = 0; i < values.Count; i++)
            {
                ushort v = values[i];
                command[index++] = (byte)(v & 0xFF);
                command[index++] = (byte)((v >> 8) & 0xFF);
            }

            command[74] = CalcChecksum(command, 1, 73);
            command[75] = ETX;

            System.Diagnostics.Debug.WriteLine("=============== PARAMETER WRITE PACKET ===============");
            for (int i = 0; i < values.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"values[{i}] = {values[i]} (0x{values[i]:X4})");
            }
            System.Diagnostics.Debug.WriteLine("command = " + BitConverter.ToString(command));
            System.Diagnostics.Debug.WriteLine("======================================================");

            return command;
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

        public static byte GetCheckSum(byte[] array, int start, int endInclusive)
        {
            return CalcChecksum(array, start, endInclusive);
        }
    }
}
