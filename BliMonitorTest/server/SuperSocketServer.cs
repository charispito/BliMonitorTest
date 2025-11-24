using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BliMonitorTest.data;
using SuperSocket;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase.Logging;

namespace BliMonitorTest.server
{
    public class SuperSocketServer
    {
        public ServerConfig config;
        public AppServer server;

        public void init()
        {
            config = new ServerConfig()
            {
                Port = 7755,
                Ip = "Any",
                Mode = SocketMode.Tcp,
                Name = "BliMonitorTest",
                TextEncoding = "utf-8"
            };
            server = new AppServer();
            server.Setup(new RootConfig(), config, logFactory: new Log4NetLogFactory());
            server.Start();
            server.NewSessionConnected += Server_NewSessionConnected;
            server.NewRequestReceived += Server_NewRequestReceived;
        }

        public void Write(byte[] cmd)
        {
        }

        private void Server_NewRequestReceived(AppSession session, SuperSocket.SocketBase.Protocol.StringRequestInfo requestInfo)
        {
            Console.WriteLine(requestInfo.ToString());
        }

        private void Server_NewSessionConnected(AppSession session)
        {           
            Console.WriteLine(session.SocketSession.Client.RemoteEndPoint.ToString());
            Task result = Task.Delay(1000).ContinueWith(_ =>
            {
                byte[] cmd = Protocol.GetNewCommand(1);
                session.Send(cmd, 0, cmd.Length);
                cmd = Protocol.GetCommand(1);
                session.Send(cmd, 0, cmd.Length); ;
                Console.WriteLine("send");
            });
        }
    }
}
