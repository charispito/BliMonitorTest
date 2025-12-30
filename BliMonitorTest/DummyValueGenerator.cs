using System;

namespace BliMonitorTest.dummy
{
    public sealed class DummyValueGenerator
    {
        private readonly DateTime _t0 = DateTime.UtcNow;
        private readonly Random _rng = new Random();

        public DummySample Next()
        {
            double t = (DateTime.UtcNow - _t0).TotalSeconds;

                     // 0~255 범위 1바이트 값들(정의서 기준)
            byte heaterTemp = ClampToByte(180 + 10 * Math.Sin(t / 7.0));
            byte heaterOffTime = ClampToByte(30 + 10 * Math.Sin(t / 9.0 + 0.5));
            byte exhaustTemp = ClampToByte(160 + 8 * Math.Sin(t / 5.0 + 0.7));
            byte exhaustTempAvg = ClampToByte(158 + 5 * Math.Sin(t / 6.5 + 0.3));
            byte airHeaterTemp = ClampToByte(140 + 12 * Math.Sin(t / 8.0 + 1.2));
            byte airHeaterDuty = ClampToByte(50 + 20 * Math.Sin(t / 4.0));

                     // 메인모터 전류 (2바이트: 13 상위, 14 하위)
            int current = (int)(1200 + 300 * Math.Sin(t / 3.0) + _rng.Next(-20, 21));
            ushort mainMotorCurrent = (ushort)ClampInt(current, 0, 5000);

                     // 메인모터운전 비트필드 (5번 바이트)
            bool running = Math.Sin(t / 12.0) > 0;
            bool forward = running && (Math.Sin(t / 20.0) > 0);
            bool reverse = running && !forward;
            bool stop = !running;

            byte mainMotorRunBits = 0;
            if (running) mainMotorRunBits |= 0x01; // BIT0 동작
            if (forward) mainMotorRunBits |= 0x02; // BIT1 순방향
            if (reverse) mainMotorRunBits |= 0x04; // BIT2 역방향
            if (stop) mainMotorRunBits |= 0x08; // BIT3 정지

            return new DummySample(
                mainMotorRunBits,
                heaterTemp,
                heaterOffTime,
                exhaustTemp,
                exhaustTempAvg,
                airHeaterTemp,
                airHeaterDuty,
                mainMotorCurrent
            );
        }

        private static byte ClampToByte(double v)
        {
            int i = (int)Math.Round(v);
            i = ClampInt(i, 0, 255);
            return (byte)i;
        }

        private static int ClampInt(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }

    public sealed class DummySample
    {
        public byte MainMotorRunBits { get; private set; }   // index 5
        public byte HeaterTemp { get; private set; }         // index 6
        public byte HeaterOffTime { get; private set; }      // index 7
        public byte ExhaustTemp { get; private set; }        // index 8
        public byte ExhaustTempAvg { get; private set; }     // index 9
        public byte AirHeaterTemp { get; private set; }      // index 10
        public byte AirHeaterDuty { get; private set; }      // index 11
        public ushort MainMotorCurrent { get; private set; } // index 13..14 (BE)

        public DummySample(
            byte mainMotorRunBits,
            byte heaterTemp,
            byte heaterOffTime,
            byte exhaustTemp,
            byte exhaustTempAvg,
            byte airHeaterTemp,
            byte airHeaterDuty,
            ushort mainMotorCurrent)
        {
            MainMotorRunBits = mainMotorRunBits;
            HeaterTemp = heaterTemp;
            HeaterOffTime = heaterOffTime;
            ExhaustTemp = exhaustTemp;
            ExhaustTempAvg = exhaustTempAvg;
            AirHeaterTemp = airHeaterTemp;
            AirHeaterDuty = airHeaterDuty;
            MainMotorCurrent = mainMotorCurrent;
        }
    }

    public static class DummyFramePatcher
    {
        /// <summary>
        /// 엑셀 정의서(From PCS-400, SIZE=57) 기준으로 상태 응답 프레임에 값 심기 + 체크섬 재계산
        /// </summary>
        public static void PatchStatusResponse57(byte[] rsp, DummySample s)
        {
            if (rsp == null || rsp.Length < 57) return;
            if (s == null) return;

                     // 프레임 기본(Sanity)
            rsp[0] = 0x12;  // START
            rsp[1] = 0x01;
            rsp[2] = 0xA0;   // ✅ 핵심: 상태 응답으로 고정 (차트 로직이 이 CMD를 볼 확률 큼)
            rsp[3] = 57;    // SIZE
            rsp[4] = 0x01;  // 문서에 1로 표기

                     // 값 매핑 (엑셀 표 그대로)
            rsp[5] = s.MainMotorRunBits;
            rsp[6] = s.HeaterTemp;
            rsp[7] = s.HeaterOffTime;
            rsp[8] = s.ExhaustTemp;
            rsp[9] = s.ExhaustTempAvg;
            rsp[10] = s.AirHeaterTemp;
            rsp[11] = s.AirHeaterDuty;

                     // 메인모터 전류: 상위/하위 (Big-Endian)
            rsp[13] = (byte)((s.MainMotorCurrent >> 8) & 0xFF);
            rsp[14] = (byte)(s.MainMotorCurrent & 0xFF);

            rsp[56] = 0x34;     // END
            rsp[55] = CalcChecksum(rsp); // CHECKSUM
        }

        /// <summary>
              /// BYTE 1~54 XOR 후 마지막에 0xFF XOR
        /// </summary>
        private static byte CalcChecksum(byte[] buf)
        {
            byte x = 0x00;
            for (int i = 1; i <= 54; i++)
                x ^= buf[i];
            x ^= 0xFF;
            return x;
        }
    }
}
