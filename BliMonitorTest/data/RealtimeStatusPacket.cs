namespace BliMonitorTest.data
{
    public sealed class RealtimeStatusPacket
    {
        public byte ModelCode { get; set; }
        public byte ErrorCode { get; set; }
        public byte WaterInitDone { get; set; }
        public byte WaterInitGo { get; set; }
        public byte EmptyDetect { get; set; }
        public byte BufferLow { get; set; }

        public byte PcbHwVersion { get; set; }
        public byte PcbSwVersion { get; set; }

        public byte HeaterPwm { get; set; }
        public byte Night { get; set; }
        public byte TestMode { get; set; }
        public byte ModeSelected { get; set; }
        public byte QtySelected { get; set; }
        public byte DispensePhase { get; set; }
        public byte DispenseSubPhase { get; set; }

        public ushort HotTemp { get; set; }
        public ushort ColdTemp { get; set; }

        public byte FloatLowStable { get; set; }
        public byte BallTopFullStable { get; set; }
        public byte WaterBufFullStable { get; set; }

        public byte HeaterOutput { get; set; }
        public byte CompressorOutput { get; set; }
        public byte HotValveOutput { get; set; }
        public byte ColdSelectOutput { get; set; }
        public byte OutletValveOutput { get; set; }

        public ushort ButtonInfo { get; set; }

        public byte StatusA { get; set; }
        public byte StatusB { get; set; }

        public byte ReheatRunning
        {
            get { return (byte)(((StatusB & 0x20) != 0) ? 1 : 0); }
        }

        public byte HotIng
        {
            get { return (byte)(((StatusB & 0x40) != 0) ? 1 : 0); }
        }
    }
}
