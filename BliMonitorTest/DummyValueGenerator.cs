using System;

namespace BliMonitorTest.dummy
{
    public sealed class DummySample
    {
        public byte ModelCode { get; private set; }
        public byte ErrorCode { get; private set; }

        public byte WaterInitDone { get; private set; }
        public byte WaterInitGo { get; private set; }
        public byte EmptyDetect { get; private set; }
        public byte BufferLow { get; private set; }

        public byte ReheatRunning { get; private set; }
        public byte HotIng { get; private set; }
        public byte HeaterPwm { get; private set; }
        public byte Night { get; private set; }
        public byte TestMode { get; private set; }

        public byte ModeSelected { get; private set; }
        public byte QtySelected { get; private set; }
        public byte DispensePhase { get; private set; }
        public byte DispenseSubPhase { get; private set; }

        public ushort HotTempRaw { get; private set; }
        public ushort ColdTempRaw { get; private set; }

        public byte FloatLowStable { get; private set; }
        public byte BallTopFullStable { get; private set; }
        public byte WaterBufFullStable { get; private set; }

        public byte HeaterOutput { get; private set; }
        public byte CompressorOutput { get; private set; }
        public byte HotValveOutput { get; private set; }
        public byte ColdSelectOutput { get; private set; }
        public byte OutletValveOutput { get; private set; }
        public byte PumpOutletOutput { get; private set; }
        public byte PumpDiapOutput { get; private set; }
        public byte PumpAirventOutput { get; private set; }

        public byte HotSelected { get; private set; }
        public byte WarmSelected { get; private set; }
        public byte NormalSelected { get; private set; }
        public byte CoolSelected { get; private set; }
        public byte ColdSelected { get; private set; }

        public byte StatusA { get; private set; }
        public byte StatusB { get; private set; }
        public ushort ButtonInfo { get; private set; }

        public DummySample(
            byte modelCode,
            byte errorCode,
            byte waterInitDone,
            byte waterInitGo,
            byte emptyDetect,
            byte bufferLow,
            byte reheatRunning,
            byte hotIng,
            byte heaterPwm,
            byte night,
            byte testMode,
            byte modeSelected,
            byte qtySelected,
            byte dispensePhase,
            byte dispenseSubPhase,
            ushort hotTempRaw,
            ushort coldTempRaw,
            byte floatLowStable,
            byte ballTopFullStable,
            byte waterBufFullStable,
            byte heaterOutput,
            byte compressorOutput,
            byte hotValveOutput,
            byte coldSelectOutput,
            byte outletValveOutput,
            byte pumpOutletOutput,
            byte pumpDiapOutput,
            byte pumpAirventOutput,
            byte hotSelected,
            byte warmSelected,
            byte normalSelected,
            byte coolSelected,
            byte coldSelected,
            byte statusA,
            byte statusB,
            ushort buttonInfo)
        {
            ModelCode = modelCode;
            ErrorCode = errorCode;
            WaterInitDone = waterInitDone;
            WaterInitGo = waterInitGo;
            EmptyDetect = emptyDetect;
            BufferLow = bufferLow;
            ReheatRunning = reheatRunning;
            HotIng = hotIng;
            HeaterPwm = heaterPwm;
            Night = night;
            TestMode = testMode;
            ModeSelected = modeSelected;
            QtySelected = qtySelected;
            DispensePhase = dispensePhase;
            DispenseSubPhase = dispenseSubPhase;
            HotTempRaw = hotTempRaw;
            ColdTempRaw = coldTempRaw;
            FloatLowStable = floatLowStable;
            BallTopFullStable = ballTopFullStable;
            WaterBufFullStable = waterBufFullStable;
            HeaterOutput = heaterOutput;
            CompressorOutput = compressorOutput;
            HotValveOutput = hotValveOutput;
            ColdSelectOutput = coldSelectOutput;
            OutletValveOutput = outletValveOutput;
            PumpOutletOutput = pumpOutletOutput;
            PumpDiapOutput = pumpDiapOutput;
            PumpAirventOutput = pumpAirventOutput;
            HotSelected = hotSelected;
            WarmSelected = warmSelected;
            NormalSelected = normalSelected;
            CoolSelected = coolSelected;
            ColdSelected = coldSelected;
            StatusA = statusA;
            StatusB = statusB;
            ButtonInfo = buttonInfo;
        }
    }

    public sealed class DummyValueGenerator
    {
        private readonly DateTime _t0 = DateTime.UtcNow;
        private int _statusSeq = 0;
        private int _errorSeq = 0;

        private const byte MODEL_CODE_DEWO8 = 0x06;

        private const byte MODE_NONE = 0;
        private const byte MODE_HOT = 1;
        private const byte MODE_NORMAL = 2;
        private const byte MODE_COLD = 5;
        private const byte MODE_WARM = 6;
        private const byte MODE_COOL = 7;

        private const byte QTY_NONE = 0;
        private const byte QTY_150ML = 1;
        private const byte QTY_1000ML = 2;

        private const byte DISP_PHASE_STOP = 0;
        private const byte DISP_PHASE_CONT = 1;
        private const byte DISP_PHASE_QTY = 2;

        private const byte DISP_SUB_IDLE = 0;
        private const byte DISP_SUB_PREFLOW = 1;
        private const byte DISP_SUB_RUNNING = 2;

        private const byte ERROR_NONE = 0x00;
        private const byte ERROR_COLD_ERR1 = 0x01;
        private const byte ERROR_COLD_ERR2 = 0x02;
        private const byte ERROR_HOT_ERR1 = 0x04;
        private const byte ERROR_HOT_ERR2 = 0x08;
        private const byte ERROR_HOT_ERR3 = 0x10;

        private const byte STATUSA_HEATER_ACTIVE = 0x01;
        private const byte STATUSA_COMP_ACTIVE = 0x02;
        private const byte STATUSA_HOT_VALVE = 0x04;
        private const byte STATUSA_COLD_SELECT_VALVE = 0x08;
        private const byte STATUSA_OUTLET_VALVE = 0x10;
        private const byte STATUSA_PUMP_OUTLET = 0x20;
        private const byte STATUSA_PUMP_DIAP = 0x40;
        private const byte STATUSA_PUMP_AIRVENT = 0x80;

        private const byte STATUSB_FLOAT_SENSOR = 0x01;
        private const byte STATUSB_BALL_TOP_SENSOR = 0x02;
        private const byte STATUSB_WATER_BUF_SENSOR = 0x04;
        private const byte STATUSB_EMPTY_DETECT = 0x08;
        private const byte STATUSB_BUFFER_LOW = 0x10;
        private const byte STATUSB_REHEAT_RUNNING = 0x20;
        private const byte STATUSB_HOT_ING = 0x40;
        private const byte STATUSB_DISPENSING = 0x80;

        private sealed class RealLikeRow
        {
            public ushort HotRaw;
            public ushort ColdRaw;
            public byte HeaterOn;
            public byte ReheatRunning;
            public byte HotIng;
        }

        // 실데이터 샘플 기반: STOP / IDLE / NONE 상태에서 히터가 간헐 동작하는 패턴
        private readonly RealLikeRow[] _statusRows = new[]
        {
            new RealLikeRow { HotRaw = 913, ColdRaw = 49, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 902, ColdRaw = 43, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 898, ColdRaw = 39, HeaterOn = 1, ReheatRunning = 1, HotIng = 1 },
            new RealLikeRow { HotRaw = 898, ColdRaw = 39, HeaterOn = 1, ReheatRunning = 1, HotIng = 1 },
            new RealLikeRow { HotRaw = 909, ColdRaw = 46, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 910, ColdRaw = 48, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 898, ColdRaw = 39, HeaterOn = 1, ReheatRunning = 1, HotIng = 1 },
            new RealLikeRow { HotRaw = 910, ColdRaw = 49, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 910, ColdRaw = 49, HeaterOn = 1, ReheatRunning = 1, HotIng = 1 },
            new RealLikeRow { HotRaw = 898, ColdRaw = 47, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 898, ColdRaw = 47, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 910, ColdRaw = 56, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 897, ColdRaw = 47, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 897, ColdRaw = 47, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 910, ColdRaw = 56, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 897, ColdRaw = 47, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 907, ColdRaw = 49, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 896, ColdRaw = 41, HeaterOn = 1, ReheatRunning = 1, HotIng = 1 },
            new RealLikeRow { HotRaw = 909, ColdRaw = 49, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 909, ColdRaw = 50, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 896, ColdRaw = 42, HeaterOn = 0, ReheatRunning = 0, HotIng = 0 },
            new RealLikeRow { HotRaw = 901, ColdRaw = 45, HeaterOn = 1, ReheatRunning = 1, HotIng = 1 },
        };

        public DummySample Next()
        {
            var row = _statusRows[_statusSeq % _statusRows.Length];
            _statusSeq++;

            byte errorCode = ERROR_NONE;
            byte waterInitDone = 1;
            byte waterInitGo = 0;
            byte emptyDetect = 0;
            byte bufferLow = 0;

            byte reheatRunning = To01(row.ReheatRunning);
            byte hotIng = To01(row.HotIng);
            byte heaterPwm = To01(row.HeaterOn);
            byte night = 0;
            byte testMode = 0;

            // 실데이터 샘플 기준
            byte modeSelected = MODE_NONE;
            byte qtySelected = QTY_NONE;
            byte dispensePhase = DISP_PHASE_STOP;
            byte dispenseSubPhase = DISP_SUB_IDLE;

            ushort hotTempRaw = row.HotRaw;
            ushort coldTempRaw = row.ColdRaw;

            byte floatLowStable = 0;
            byte ballTopFullStable = 0;
            byte waterBufFullStable = 1;

            byte heaterOutput = To01(row.HeaterOn);
            byte compressorOutput = 0;
            byte hotValveOutput = 0;
            byte coldSelectOutput = 0;
            byte outletValveOutput = 0;
            byte pumpOutletOutput = 0;
            byte pumpDiapOutput = 0;
            byte pumpAirventOutput = 0;

            byte hotSelected = 0;
            byte warmSelected = 0;
            byte normalSelected = 0;
            byte coolSelected = 0;
            byte coldSelected = 0;

            byte statusA = BuildStatusA(
                hotIng,
                heaterPwm,
                heaterOutput,
                compressorOutput,
                hotValveOutput,
                coldSelectOutput,
                outletValveOutput,
                pumpOutletOutput,
                pumpDiapOutput,
                pumpAirventOutput);

            byte statusB = BuildStatusB(
                floatLowStable,
                ballTopFullStable,
                waterBufFullStable,
                emptyDetect,
                bufferLow,
                reheatRunning,
                hotIng,
                dispensePhase);

            ushort buttonInfo = BuildButtonInfo(
                hotSelected,
                warmSelected,
                normalSelected,
                coolSelected,
                coldSelected,
                reheatRunning,
                qtySelected,
                dispensePhase);

            return new DummySample(
                MODEL_CODE_DEWO8,
                errorCode,
                waterInitDone,
                waterInitGo,
                emptyDetect,
                bufferLow,
                reheatRunning,
                hotIng,
                heaterPwm,
                night,
                testMode,
                modeSelected,
                qtySelected,
                dispensePhase,
                dispenseSubPhase,
                hotTempRaw,
                coldTempRaw,
                floatLowStable,
                ballTopFullStable,
                waterBufFullStable,
                heaterOutput,
                compressorOutput,
                hotValveOutput,
                coldSelectOutput,
                outletValveOutput,
                pumpOutletOutput,
                pumpDiapOutput,
                pumpAirventOutput,
                hotSelected,
                warmSelected,
                normalSelected,
                coolSelected,
                coldSelected,
                statusA,
                statusB,
                buttonInfo);
        }

        public byte[] BuildDummyErrorResponse()
        {
            byte[] tx = new byte[134];
            tx[0] = 0x12;
            tx[1] = 0x01;
            tx[2] = 0xB9;
            tx[3] = 134;

            for (int i = 4; i <= 131; i++)
                tx[i] = 0xFF;

            // 에러 이력도 동일하게 raw/10 기준 온도 사용
            // hot/cold는 실제 온도 raw
            // adcHot/adcCold는 현재 시스템에서 별도 의미가 없으면
            // 우선 동일 raw 값 또는 근사 raw 값을 넣어서 화면/로그 일관성 유지
            for (int i = 0; i < 5; i++)
            {
                int baseIdx = 4 + (i * 16);

                byte errorCode;
                ushort hot;
                ushort cold;
                ushort adcHot;
                ushort adcCold;
                byte waterInitDone = 1;
                byte statusA;
                byte statusB;
                byte bufferLow = 0;

                switch (i)
                {
                    case 0:
                        // 냉수 이상 1
                        errorCode = ERROR_COLD_ERR1;
                        hot = 907;   // 90.7°C
                        cold = 56;   // 5.6°C
                        adcHot = 907;
                        adcCold = 56;
                        statusA = STATUSA_COMP_ACTIVE;
                        statusB = STATUSB_WATER_BUF_SENSOR;
                        break;

                    case 1:
                        // 냉수 이상 2
                        errorCode = ERROR_COLD_ERR2;
                        hot = 901;   // 90.1°C
                        cold = 45;   // 4.5°C
                        adcHot = 901;
                        adcCold = 45;
                        statusA = STATUSA_COMP_ACTIVE;
                        statusB = STATUSB_WATER_BUF_SENSOR;
                        break;

                    case 2:
                        // 온수 이상 1
                        errorCode = ERROR_HOT_ERR1;
                        hot = 913;   // 91.3°C
                        cold = 49;   // 4.9°C
                        adcHot = 913;
                        adcCold = 49;
                        statusA = STATUSA_HEATER_ACTIVE;
                        statusB = (byte)(STATUSB_WATER_BUF_SENSOR | STATUSB_REHEAT_RUNNING | STATUSB_HOT_ING);
                        break;

                    case 3:
                        // 온수 이상 2
                        errorCode = ERROR_HOT_ERR2;
                        hot = 898;   // 89.8°C
                        cold = 39;   // 3.9°C
                        adcHot = 898;
                        adcCold = 39;
                        statusA = STATUSA_HEATER_ACTIVE;
                        statusB = (byte)(STATUSB_WATER_BUF_SENSOR | STATUSB_REHEAT_RUNNING | STATUSB_HOT_ING);
                        break;

                    default:
                        // 온수 이상 3
                        errorCode = ERROR_HOT_ERR3;
                        hot = 896;   // 89.6°C
                        cold = 42;   // 4.2°C
                        adcHot = 896;
                        adcCold = 42;
                        statusA = STATUSA_HEATER_ACTIVE;
                        statusB = (byte)(STATUSB_WATER_BUF_SENSOR | STATUSB_REHEAT_RUNNING | STATUSB_HOT_ING);
                        break;
                }

                tx[baseIdx + 0] = 0xA5;
                tx[baseIdx + 1] = (byte)((_errorSeq + i + 1) & 0xFF);
                tx[baseIdx + 2] = errorCode;
                tx[baseIdx + 3] = (byte)(hot & 0xFF);
                tx[baseIdx + 4] = (byte)((hot >> 8) & 0xFF);
                tx[baseIdx + 5] = (byte)(cold & 0xFF);
                tx[baseIdx + 6] = (byte)((cold >> 8) & 0xFF);
                tx[baseIdx + 7] = (byte)(adcHot & 0xFF);
                tx[baseIdx + 8] = (byte)((adcHot >> 8) & 0xFF);
                tx[baseIdx + 9] = (byte)(adcCold & 0xFF);
                tx[baseIdx + 10] = (byte)((adcCold >> 8) & 0xFF);
                tx[baseIdx + 11] = waterInitDone;
                tx[baseIdx + 12] = statusA;
                tx[baseIdx + 13] = statusB;
                tx[baseIdx + 14] = bufferLow;

                byte crc = 0x00;
                for (int j = 0; j <= 14; j++)
                    crc = (byte)(crc + tx[baseIdx + j]);
                tx[baseIdx + 15] = crc;
            }

            tx[132] = CalcChecksumRange(tx, 1, 131);
            tx[133] = 0x34;

            _errorSeq += 8;
            return tx;
        }

        public static void PatchStatusResponse37(byte[] rsp, DummySample s)
        {
            if (rsp == null || rsp.Length != 37) throw new ArgumentException("rsp must be 37 bytes");
            if (s == null) throw new ArgumentNullException(nameof(s));

            rsp[0] = 0x12;
            rsp[1] = 0x01;
            rsp[2] = 0xA0;
            rsp[3] = 37;

            rsp[4] = s.ModelCode;
            rsp[5] = s.ErrorCode;
            rsp[6] = To01(s.WaterInitDone);
            rsp[7] = To01(s.WaterInitGo);
            rsp[8] = To01(s.EmptyDetect);
            rsp[9] = To01(s.BufferLow);
            rsp[10] = To01(s.ReheatRunning);
            rsp[11] = To01(s.HotIng);
            rsp[12] = To01(s.HeaterPwm);
            rsp[13] = To01(s.Night);
            rsp[14] = To01(s.TestMode);
            rsp[15] = s.ModeSelected;
            rsp[16] = s.QtySelected;
            rsp[17] = s.DispensePhase;
            rsp[18] = s.DispenseSubPhase;

            rsp[19] = (byte)(s.HotTempRaw & 0xFF);
            rsp[20] = (byte)((s.HotTempRaw >> 8) & 0xFF);
            rsp[21] = (byte)(s.ColdTempRaw & 0xFF);
            rsp[22] = (byte)((s.ColdTempRaw >> 8) & 0xFF);

            rsp[23] = To01(s.FloatLowStable);
            rsp[24] = To01(s.BallTopFullStable);
            rsp[25] = To01(s.WaterBufFullStable);
            rsp[26] = To01(s.HeaterOutput);
            rsp[27] = To01(s.CompressorOutput);
            rsp[28] = To01(s.HotValveOutput);
            rsp[29] = To01(s.ColdSelectOutput);
            rsp[30] = To01(s.OutletValveOutput);

            rsp[31] = (byte)(s.ButtonInfo & 0xFF);
            rsp[32] = (byte)((s.ButtonInfo >> 8) & 0xFF);
            rsp[33] = s.StatusA;
            rsp[34] = s.StatusB;
            rsp[35] = CalcChecksumRange(rsp, 1, 34);
            rsp[36] = 0x34;
        }

        private static ushort BuildButtonInfo(
            byte hotSelected,
            byte warmSelected,
            byte normalSelected,
            byte coolSelected,
            byte coldSelected,
            byte reheatRunning,
            byte qtySelected,
            byte dispensePhase)
        {
            ushort v = 0;

            if (hotSelected != 0) v |= 0x0001;
            if (warmSelected != 0) v |= 0x0002;
            if (normalSelected != 0) v |= 0x0004;
            if (coolSelected != 0) v |= 0x0008;
            if (coldSelected != 0) v |= 0x0010;
            if (reheatRunning != 0) v |= 0x0020;
            if (qtySelected == QTY_150ML) v |= 0x0040;
            if (qtySelected == QTY_1000ML) v |= 0x0080;
            if (dispensePhase != 0) v |= 0x0100;

            return v;
        }

        private static byte BuildStatusA(
            byte hotIng,
            byte heaterPwm,
            byte heaterOutput,
            byte compressorOutput,
            byte hotValveOutput,
            byte coldSelectOutput,
            byte outletValveOutput,
            byte pumpOutletOutput,
            byte pumpDiapOutput,
            byte pumpAirventOutput)
        {
            byte v = 0x00;

            if (heaterOutput != 0 || hotIng != 0 || heaterPwm != 0) v |= STATUSA_HEATER_ACTIVE;
            if (compressorOutput != 0) v |= STATUSA_COMP_ACTIVE;
            if (hotValveOutput != 0) v |= STATUSA_HOT_VALVE;
            if (coldSelectOutput != 0) v |= STATUSA_COLD_SELECT_VALVE;
            if (outletValveOutput != 0) v |= STATUSA_OUTLET_VALVE;
            if (pumpOutletOutput != 0) v |= STATUSA_PUMP_OUTLET;
            if (pumpDiapOutput != 0) v |= STATUSA_PUMP_DIAP;
            if (pumpAirventOutput != 0) v |= STATUSA_PUMP_AIRVENT;

            return v;
        }

        private static byte BuildStatusB(
            byte floatLowStable,
            byte ballTopFullStable,
            byte waterBufFullStable,
            byte emptyDetect,
            byte bufferLow,
            byte reheatRunning,
            byte hotIng,
            byte dispensePhase)
        {
            byte v = 0x00;

            if (floatLowStable != 0) v |= STATUSB_FLOAT_SENSOR;
            if (ballTopFullStable != 0) v |= STATUSB_BALL_TOP_SENSOR;
            if (waterBufFullStable != 0) v |= STATUSB_WATER_BUF_SENSOR;
            if (emptyDetect != 0) v |= STATUSB_EMPTY_DETECT;
            if (bufferLow != 0) v |= STATUSB_BUFFER_LOW;
            if (reheatRunning != 0) v |= STATUSB_REHEAT_RUNNING;
            if (hotIng != 0) v |= STATUSB_HOT_ING;
            if (dispensePhase != 0) v |= STATUSB_DISPENSING;

            return v;
        }

        private static byte CalcChecksumRange(byte[] buf, int start, int endInclusive)
        {
            byte x = 0x00;
            for (int i = start; i <= endInclusive; i++)
                x ^= buf[i];
            x ^= 0xFF;
            return x;
        }

        private static byte To01(byte v)
        {
            return (byte)(v != 0 ? 1 : 0);
        }

        public byte[] BuildDummyParameterResponse74()
        {
            ushort[] vals = new ushort[]
            {
                850, 880, 820,          // hot_target_reheat_x10, hot_target_normal_x10, hot_target_eco_x10
                80, 40, 120, 60, 140, 80, // cold_target_th/tl_a/b/c
                30, 35, 40, 45,         // disp_delay_*
                150, 900,               // disp_time_cold_150/1000
                160, 920,               // disp_time_cool_150/1000
                170, 940,               // disp_time_normal_150/1000
                180, 960,               // disp_time_hot_150/1000
                190, 980,               // disp_time_warm_150/1000
                155, 905,               // disp_time_reuse_cold_150/1000
                165, 925,               // disp_time_reuse_cool_150/1000
                175, 945,               // disp_time_reuse_normal_150/1000
                185, 965,               // disp_time_reuse_hot_150/1000
                195, 985,               // disp_time_reuse_warm_150/1000
                300                     // auto_refill_delay_10ms
            };

            byte[] tx = new byte[74];
            tx[0] = 0x12;
            tx[1] = 0x01;
            tx[2] = 0xC1;
            tx[3] = 74;

            int idx = 4;
            for (int i = 0; i < vals.Length; i++)
            {
                tx[idx++] = (byte)(vals[i] & 0xFF);
                tx[idx++] = (byte)((vals[i] >> 8) & 0xFF);
            }

            tx[72] = CalcChecksumRange(tx, 1, 71);
            tx[73] = 0x34;
            return tx;
        }

        public byte[] BuildDummyParameterWriteAck(byte result)
        {
            byte[] tx = new byte[8];
            tx[0] = 0x12;
            tx[1] = 0x01;
            tx[2] = 0xC2;
            tx[3] = 0x08;
            tx[4] = result;
            tx[5] = 0x00;
            tx[6] = CalcChecksumRange(tx, 1, 5);
            tx[7] = 0x34;
            return tx;
        }

    }
}
