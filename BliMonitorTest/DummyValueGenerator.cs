using System;

namespace BliMonitorTest.dummy
{
    public sealed class DummySample
    {
        public byte ModelCode { get; private set; }      // index 5
        public byte SwVersion { get; private set; }      // index 6

        public byte HeaterTemp { get; private set; }     // index 7
        public byte ColdTemp { get; private set; }       // index 8
        public bool LowWaterSensor { get; private set; } // index 9
        public bool FloorSensor { get; private set; }    // index 10

        public ushort ButtonsRaw { get; private set; }   // index 15,16

        public bool PumpOn { get; private set; }         // index 17
        public bool ColdSolOn { get; private set; }      // index 18
        public bool NormalSolOn { get; private set; }    // index 19
        public bool HotSol1On { get; private set; }      // index 20

        public byte NeedleState { get; private set; }    // index 21
        public bool CompressorOn { get; private set; }   // index 22 (0/1)

        public DummySample(
            byte modelCode,
            byte swVersion,
            byte heaterTemp,
            byte coldTemp,
            bool lowWaterSensor,
            bool floorSensor,
            ushort buttonsRaw,
            bool pumpOn,
            bool coldSolOn,
            bool normalSolOn,
            bool hotSol1On,
            byte needleState,
            bool compressorOn)
        {
            ModelCode = modelCode;
            SwVersion = swVersion;
            HeaterTemp = heaterTemp;
            ColdTemp = coldTemp;
            LowWaterSensor = lowWaterSensor;
            FloorSensor = floorSensor;
            ButtonsRaw = buttonsRaw;
            PumpOn = pumpOn;
            ColdSolOn = coldSolOn;
            NormalSolOn = normalSolOn;
            HotSol1On = hotSol1On;
            NeedleState = needleState;
            CompressorOn = compressorOn;
        }
    }

    public sealed class DummyValueGenerator
    {
        private readonly DateTime _t0 = DateTime.UtcNow;
        private readonly Random _rng = new Random();

        public DummySample Next()
        {
            double t = (DateTime.UtcNow - _t0).TotalSeconds;

            byte heaterTemp = ClampToByte(65 + 15 * Math.Sin(t / 7.0));
            byte coldTemp = ClampToByte(12 + 5 * Math.Sin(t / 9.0));

            bool lowWaterSensor = (Math.Sin(t / 11.0) > 0.3);
            bool floorSensor = (Math.Sin(t / 13.0) > -0.2);

            ushort buttons = 0;
            buttons |= (ushort)((Math.Sin(t / 5.0) > 0.2) ? (1 << 0) : 0);
            buttons |= (ushort)((Math.Sin(t / 6.0) > 0.1) ? (1 << 1) : 0);
            buttons |= (ushort)((Math.Sin(t / 7.0) > 0.0) ? (1 << 2) : 0);
            buttons |= (ushort)((Math.Sin(t / 8.0) > 0.3) ? (1 << 3) : 0);
            buttons |= (ushort)((Math.Sin(t / 9.0) > 0.2) ? (1 << 4) : 0);
            buttons |= (ushort)((Math.Sin(t / 10.0) > 0.1) ? (1 << 5) : 0);
            buttons |= (ushort)((Math.Sin(t / 11.0) > 0.0) ? (1 << 6) : 0);
            buttons |= (ushort)((Math.Sin(t / 12.0) > 0.2) ? (1 << 7) : 0);
            buttons |= (ushort)((Math.Sin(t / 13.0) > 0.1) ? (1 << 8) : 0);
            buttons |= (ushort)((Math.Sin(t / 14.0) > 0.0) ? (1 << 9) : 0);

            bool pumpOn = (Math.Sin(t / 15.0) > 0.0);
            bool coldSolOn = (Math.Sin(t / 16.0) > 0.3);
            bool normalSolOn = (Math.Sin(t / 17.0) > 0.2);
            bool hotSol1On = (Math.Sin(t / 18.0) > 0.1);

            byte needle = (byte)_rng.Next(0, 3);
            bool compOn = (Math.Sin(t / 19.0) > 0.15);

            byte modelCode = 0x02;
            byte swVersion = 0x03;

            return new DummySample(
                modelCode,
                swVersion,
                heaterTemp,
                coldTemp,
                lowWaterSensor,
                floorSensor,
                buttons,
                pumpOn,
                coldSolOn,
                normalSolOn,
                hotSol1On,
                needle,
                compOn
            );
        }

        private static byte ClampToByte(double v)
        {
            int i = (int)Math.Round(v);
            if (i < 0) i = 0;
            if (i > 255) i = 255;
            return (byte)i;
        }
    }

    public static class DummyFramePatcher
    {
        public static void PatchStatusResponse57(byte[] rsp, DummySample s)
        {
            if (rsp == null || rsp.Length != 57) throw new ArgumentException("rsp must be 57 bytes");
            if (s == null) throw new ArgumentNullException("s");

            rsp[0] = 0x12;
            rsp[1] = 0x01;
            rsp[2] = 0xA0;
            rsp[3] = 57;
            rsp[4] = 0x01;

            rsp[5] = s.ModelCode;
            rsp[6] = s.SwVersion;

            rsp[7] = s.HeaterTemp;
            rsp[8] = s.ColdTemp;
            rsp[9] = s.LowWaterSensor ? (byte)1 : (byte)0;
            rsp[10] = s.FloorSensor ? (byte)1 : (byte)0;

            rsp[11] = 0;
            rsp[12] = 0;
            rsp[13] = 0;
            rsp[14] = 0;

            rsp[15] = (byte)(s.ButtonsRaw & 0xFF);
            rsp[16] = (byte)((s.ButtonsRaw >> 8) & 0xFF);

            rsp[17] = s.PumpOn ? (byte)1 : (byte)0;
            rsp[18] = s.ColdSolOn ? (byte)1 : (byte)0;
            rsp[19] = s.NormalSolOn ? (byte)1 : (byte)0;
            rsp[20] = s.HotSol1On ? (byte)1 : (byte)0;

            rsp[21] = s.NeedleState;
            rsp[22] = s.CompressorOn ? (byte)1 : (byte)0; // ON/OFF

            for (int i = 23; i <= 54; i++) rsp[i] = 0;

            byte chk = CalcChecksumRange(rsp, 1, 54);
            rsp[55] = chk;
            rsp[56] = 0x34;
        }

        private static byte CalcChecksumRange(byte[] buf, int start, int endInclusive)
        {
            byte x = 0x00;
            for (int i = start; i <= endInclusive; i++)
                x ^= buf[i];
            x ^= 0xFF;
            return x;
        }
    }
}
