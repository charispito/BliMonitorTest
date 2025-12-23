using BliMonitorTest;
using BliMonitorTest.util;
using System;
using System.Threading;

namespace DummySerialPortNs
{
    public class DummyServer
    {
        private Timer _timer;
        private int _currentChannel = 1;
        private readonly Random _rand = new Random();
        public bool IsRunning { get; private set; }

        public void Start()
        {

        }
    }
}
