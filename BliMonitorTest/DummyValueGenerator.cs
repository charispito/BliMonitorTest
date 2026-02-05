using System;

namespace BliMonitorTest.dummy
{
    // TO-BE 상태응답에서 사용하는 필드들을 모두 포함
    public sealed class DummySample
    {
        // 문서: index 4는 확장(1), index 5 모델, index 6 SW 버전
        public byte ModelCode { get; private set; }          // index 5
        public byte SwVersion { get; private set; }          // index 6

        public byte HeaterTemp { get; private set; }         // index 7
        public byte ColdTemp { get; private set; }           // index 8
        public bool WaterLevelLow { get; private set; }      // index 9
        public bool FloorSensor { get; private set; }        // index 10

        // 11~14(UV/3방/에어벤트/CV)는 이번 화면에서 미사용 → 0으로 패치

        // 버튼 2바이트(LSB=15, MSB=16)
        // bit0=연속출수, bit1=정량출수, bit2=자유출수, bit3=고온수, bit4=온수, bit5=약온수,
        // bit6=차일드락, bit7=상온수, bit8=약냉수, bit9=냉수
        public ushort ButtonsRaw { get; private set; }       // index 15,16

        public bool PumpOn { get; private set; }             // index 17
        public bool ColdSolOn { get; private set; }          // index 18
        public bool NormalSolOn { get; private set; }        // index 19
        public bool HotSol1On { get; private set; }          // index 20

        public byte NeedleState { get; private set; }        // index 21 (0:상승,1:동작중,2:하강 등 가정)
        public byte CompressorVoltage { get; private set; }  // index 22 (샘플 전압 값)

        public DummySample(
            byte modelCode,
            byte swVersion,
            byte heaterTemp,
            byte coldTemp,
            bool waterLevelLow,
            bool floorSensor,
            ushort buttonsRaw,
            bool pumpOn,
            bool coldSolOn,
            bool normalSolOn,
            bool hotSol1On,
            byte needleState,
            byte compressorVoltage)
        {
            ModelCode = modelCode;
            SwVersion = swVersion;
            HeaterTemp = heaterTemp;
            ColdTemp = coldTemp;
            WaterLevelLow = waterLevelLow;
            FloorSensor = floorSensor;
            ButtonsRaw = buttonsRaw;
            PumpOn = pumpOn;
            ColdSolOn = coldSolOn;
            NormalSolOn = normalSolOn;
            HotSol1On = hotSol1On;
            NeedleState = needleState;
            CompressorVoltage = compressorVoltage;
        }
    }

    public sealed class DummyValueGenerator
    {
        private readonly DateTime _t0 = DateTime.UtcNow;
        private readonly Random _rng = new Random();

        // TO-BE 상태 응답에 맞춘 더미 값 생성
        public DummySample Next()
        {
            double t = (DateTime.UtcNow - _t0).TotalSeconds;

            // 온도류 샘플(0~255 범위 byte)
            byte heaterTemp = ClampToByte(65 + 15 * Math.Sin(t / 7.0));   // 히터온도
            byte coldTemp = ClampToByte(12 + 5 * Math.Sin(t / 9.0));    // 냉수온도

            // 이진 센서값(토글)
            bool waterLevelLow = (Math.Sin(t / 11.0) > 0.3);
            bool floorSensor = (Math.Sin(t / 13.0) > -0.2);

            // 버튼 2바이트: 다양한 토글 조합(정의서 비트 순서에 맞춤)
            ushort buttons = 0;
            buttons |= (ushort)((Math.Sin(t / 5.0) > 0.2) ? (1 << 0) : 0); // 연속출수
            buttons |= (ushort)((Math.Sin(t / 6.0) > 0.1) ? (1 << 1) : 0); // 정량출수
            buttons |= (ushort)((Math.Sin(t / 7.0) > 0.0) ? (1 << 2) : 0); // 자유출수
            buttons |= (ushort)((Math.Sin(t / 8.0) > 0.3) ? (1 << 3) : 0); // 고온수
            buttons |= (ushort)((Math.Sin(t / 9.0) > 0.2) ? (1 << 4) : 0); // 온수
            buttons |= (ushort)((Math.Sin(t / 10.0) > 0.1) ? (1 << 5) : 0); // 약온수
            buttons |= (ushort)((Math.Sin(t / 11.0) > 0.0) ? (1 << 6) : 0); // 차일드락
            buttons |= (ushort)((Math.Sin(t / 12.0) > 0.2) ? (1 << 7) : 0); // 상온수
            buttons |= (ushort)((Math.Sin(t / 13.0) > 0.1) ? (1 << 8) : 0); // 약냉수
            buttons |= (ushort)((Math.Sin(t / 14.0) > 0.0) ? (1 << 9) : 0); // 냉수

            // 구동 요소 토글
            bool pumpOn = (Math.Sin(t / 15.0) > 0.0);
            bool coldSolOn = (Math.Sin(t / 16.0) > 0.3);
            bool normalSolOn = (Math.Sin(t / 17.0) > 0.2);
            bool hotSol1On = (Math.Sin(t / 18.0) > 0.1);

            // 니들/전압 샘플
            byte needle = (byte)(_rng.Next(0, 3));                // 0~2 랜덤
            byte compV = (byte)_rng.Next(100, 200);              // 샘플 전압 범위

            // 모델/버전 샘플
            byte modelCode = 0x02;
            byte swVersion = 0x03;

            return new DummySample(
                modelCode,
                swVersion,
                heaterTemp,
                coldTemp,
                waterLevelLow,
                floorSensor,
                buttons,
                pumpOn,
                coldSolOn,
                normalSolOn,
                hotSol1On,
                needle,
                compV
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
        /// <summary>
        /// TO-BE: 0xA0 STATUS 응답(총 57바이트) 프레임에 샘플 값 심고, 체크섬/ETX까지 세팅
        /// </summary>
        public static void PatchStatusResponse57(byte[] rsp, DummySample s)
        {
            if (rsp == null || rsp.Length != 57) throw new ArgumentException("rsp must be 57 bytes");
            if (s == null) throw new ArgumentNullException("s");

            // 헤더/사이즈 고정
            rsp[0] = 0x12;     // START PACKET
            rsp[1] = 0x01;     // FIX
            rsp[2] = 0xA0;     // COMMAND: STATUS
            rsp[3] = 57;       // SIZE
            rsp[4] = 0x01;     // 확장(문서 예시 값)

            // 필드 매핑
            rsp[5] = s.ModelCode;         // 모델 코드
            rsp[6] = s.SwVersion;         // SW 버전

            rsp[7] = s.HeaterTemp;        // 히터온도(℃)
            rsp[8] = s.ColdTemp;          // 냉수온도(℃)
            rsp[9] = s.WaterLevelLow ? (byte)1 : (byte)0;   // 수위센서
            rsp[10] = s.FloorSensor ? (byte)1 : (byte)0;    // 플로어 센서

            // UV/3방/에어벤트/CV 미사용 → 0
            rsp[11] = 0;
            rsp[12] = 0;
            rsp[13] = 0;
            rsp[14] = 0;

            // 버튼 2바이트(LSB/MSB)
            rsp[15] = (byte)(s.ButtonsRaw & 0xFF);
            rsp[16] = (byte)((s.ButtonsRaw >> 8) & 0xFF);

            rsp[17] = s.PumpOn ? (byte)1 : (byte)0;
            rsp[18] = s.ColdSolOn ? (byte)1 : (byte)0;
            rsp[19] = s.NormalSolOn ? (byte)1 : (byte)0;
            rsp[20] = s.HotSol1On ? (byte)1 : (byte)0;

            rsp[21] = s.NeedleState;
            rsp[22] = s.CompressorVoltage;

            // 23..54 공백 0
            for (int i = 23; i <= 54; i++) rsp[i] = 0;

            // 체크섬(1..54 XOR 후 0xFF)
            byte chk = CalcChecksumRange(rsp, 1, 54);
            rsp[55] = chk;

            // ETX
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