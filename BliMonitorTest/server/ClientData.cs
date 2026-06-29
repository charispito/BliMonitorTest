using BliMonitorTest.controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.util
{
    public class ClientData
    {
        public int ResponseCount { get; set; }
        public bool Start { get; set; } = false;
        public DateTime StartTime { get; set; }

        public int initCount = 2;
        public TcpClient client { get; set; }
        public byte[] readByteParameterData { get; set; }
        public byte[] readByteData { get; set; }
        public int clientNumber { get; set; }
        public long TimeMills { get; set; }
        public int parameterCnt = 0;
        public int errorCnt = 0;
        public List<byte> readCompleteData { get; set; }
        public List<byte> readParameterData { get; set; }
        public bool Run = true;
        public ClientData(TcpClient client)
        {
            this.client = client;
            this.readByteData = new byte[57];                   // START STOP 상태 Packet 레이아웃형태
            this.readByteParameterData = new byte[70];          // ERROR DATGA요청, ERROR 리셋, PARAMETER 요청, PARAMTER 설정의 Packet 레이아웃형태
            ResponseCount = 0;
            Start = false;
            string clientEndPoint = client.Client.RemoteEndPoint.ToString();
            char[] point = { '.', ':' };
            string[] splitedData = clientEndPoint.Split(point);
            this.clientNumber = int.Parse(splitedData[3]);
            TimeMills = DateTimeOffset.Now.Ticks + clientNumber;
            Console.WriteLine($"Connected [{clientNumber}]");
            readCompleteData = new List<byte>();
            readParameterData = new List<byte>();
            StartTime = new DateTime();
        }
    }
}
