using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public sealed class BliResponse57Packet
    {
        public byte[] Raw;
        public int StartPacket;   // [0]
        public int CmdByte;       // [2]
        public int PayloadSize;   // [3]
        public int ModelNo;       // [5]
        public int SwVer;         // [6]
        public int HeaterTempB;   // [7]
        public int ColdTempB;     // [8]
        public int LowWater;      // [9] 0/1
        public int FloorSensor;   // [10] 0/1
        public int UvLedByte;     // [11] 비트 집합
        public int Sol3Way1;      // [12] bit0
        public int Sol3Way2;      // [12] bit1
        public int Sol3Way3;      // [12] bit2
        public int AirVentSol;    // [13]
        public int CvSol;         // [14]
        public int ButtonFlags;   // [15..16] UInt16
        public int Pump;          // [17]
        public int ColdSol;       // [18]
        public int NormalSol;     // [19]
        public int HotSol1;       // [20]
        public int NeedlePos;     // [21]
        public int PelVoltageB;   // [22]
        public int Checksum;      // [55]
        public int EndPacket;     // [56]
    }

    public static class ResponsePacket57
    {
        public static BliResponse57Packet Parse(byte[] buf)
        {
            if (buf == null || buf.Length < 57) return null;
            int B(int i) => buf[i] & 0xFF;
            int Bit(byte v, int pos) => v >> pos & 0x1;

            return new BliResponse57Packet
            {
                Raw = buf,
                StartPacket = B(0),
                CmdByte = B(2),
                PayloadSize = B(3),
                ModelNo = B(5),
                SwVer = B(6),
                HeaterTempB = B(7),
                ColdTempB = B(8),
                LowWater = B(9),
                FloorSensor = B(10),
                UvLedByte = B(11),
                Sol3Way1 = Bit((byte)B(12), 0),
                Sol3Way2 = Bit((byte)B(12), 1),
                Sol3Way3 = Bit((byte)B(12), 2),
                AirVentSol = B(13),
                CvSol = B(14),
                ButtonFlags = B(15) << 8 | B(16),
                Pump = B(17),
                ColdSol = B(18),
                NormalSol = B(19),
                HotSol1 = B(20),
                NeedlePos = B(21),
                PelVoltageB = B(22),
                Checksum = B(55),
                EndPacket = B(56),
            };
        }
    }
}