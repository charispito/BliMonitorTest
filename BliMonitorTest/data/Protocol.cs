using BliMonitorTest.util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace BliMonitorTest.data
{
    public class Protocol
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Protocol));

        public static byte STX = 0xCC;
        public static byte ETX = 0xEF;
        public static byte CONTROL = 0xAA;
        public static byte STATUS = 0xA0;
        public static byte START = 0x02;
        public static byte END = 0x04;
        public static byte STATE = 0x00;

        public static byte[] GetParameter()
        {
            byte[] command = new byte[7];

            command[0] = 0x12;
            command[1] = 0x01;

            
            command[2] = 0x99;
            command[3] = 0x07;
            command[4] = 0x00;
            command[5] = (byte)(command[1] ^ command[2] ^ command[3] ^ command[4]);
            command[6] = ETX;

            command[5] = (byte)(command[5] ^ 0xFF);
            command[6] = 0x34;

            return command;
        }

        public static byte[] GetError()
        {
            byte[] command = new byte[7];

            command[0] = 0x12;
            command[1] = 0x01;

            command[2] = 0xB9;
            command[3] = 0x07;
            command[4] = 0x00;
            command[5] = (byte)(command[1] ^ command[2] ^ command[3] ^ command[4]);
            command[6] = ETX;


            command[5] = (byte)(command[5] ^ 0xFF);
            command[6] = 0x34;

            return command;
        }

        public static byte[] GetErrorReset()
        {
            byte[] command = new byte[7];

            command[0] = 0x12;
            command[1] = 0x01;
            command[6] = 0x34;

            command[2] = 0xB6;
            command[3] = 0x07;
            command[4] = 0x00;
            command[5] = (byte)(command[1] ^ command[2] ^ command[3] ^ command[4]);
            command[6] = ETX;

            command[5] = (byte)(command[5] ^ 0xFF);
            command[6] = 0x34;


            return command;
        }


        public static byte[] GetCommand(int kind)
        {
            byte[] command = new byte[7];
            command[0] = STX;
            command[1] = 0;
            command[3] = 0x07;
            switch (kind)
            {
                case 1: //state
                    command[2] = STATUS;
                    command[4] = 0x00;
                    break;
                case 2: //start
                    command[2] = CONTROL;
                    command[4] = START;
                    break;
                case 3: //end
                    command[2] = CONTROL;
                    command[4] = END;
                    break;
            }
            command[5] = (byte)(command[1] ^ command[2] ^ command[3] ^ command[4]);
            command[6] = ETX;

            return command;
        }

        public static byte[] GetNewCommand(int kind)
        {
            byte[] command = new byte[7];
            command[0] = 0x12;
            command[1] = 0x01;
            command[3] = 0x07;
            switch (kind)
            {
                case 1: //state
                    command[2] = STATUS;
                    command[4] = 0x00;
                    break;
                case 2: //start
                    command[2] = CONTROL;
                    command[4] = START;
                    break;
                case 3: //end
                    command[2] = CONTROL;
                    command[4] = END;
                    break;
            }
            command[5] = (byte)(command[1] ^ command[2] ^ command[3] ^ command[4] ^ 0xFF);
            command[6] = 0x34;

            return command;
        }

        /*
         * Get CheckSum
         * ERROR DATA요청, PARAMETER요청, PARAMETER 설정 수신(Response)시 CheckSum계산용
         */
        public static byte GetCheckSum(byte[] array, int start, int end)
        {
            int res = 0;
            for (int i = start; i < end; i++)
            {
                if (i == start)
                {
                    res = array[i] ^ array[i + 1];
                }
                else
                {
                    res = res ^ array[i + 1];
                }
            }

            return (byte)res;
        }

        /*
         * 미사용 함수
         */
        public static byte GetNewCheckSum(byte[] array, int start, int end)
        {
            int res = 0;
            for (int i = start; i < end; i++)
            {
                res = array[i] ^ array[i + 1];
            }
            res = res ^ 0xFF;
            return (byte)res;
        }
    }
}
