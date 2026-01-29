using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.dummy;
using DummySerialPortNs;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BliMonitorTest.util
{
    public class ClientManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ClientManager));

        public static ConcurrentDictionary<long, ClientData> clientDic = new ConcurrentDictionary<long, ClientData>();
        public static ConcurrentDictionary<long, ClientData> dummyClientDic = new ConcurrentDictionary<long, ClientData>();

        public delegate void ConnectionHandler(ClientData clientSocket);
        public ConnectionHandler OnConnected;
        public ConnectionHandler OnDisconnected;
        public delegate void DataReceiveHandler(ClientData client);
        public DataReceiveHandler OnReceived;
        public bool parameter = false;
        private byte[] welcome = { 0x02, 0x4D, 0x59, 0x20, 0x41, 0x64, 0x64, 0x72, 0x65, 0x73, 0x73, 0x3A, 0x31, 0x03, 0x0D, 0x0A };
        private byte[] blank = { 0x0D, 0x0A };

        private byte[] _dummyLastBuffer;
        private readonly DummyValueGenerator _dummyGen = new DummyValueGenerator();

        public void AddClient(TcpClient newClient)
        {
            log.Debug("!!!!!!!!!!!  AddClient AddClient START 303030303030   !!!!!!!!!!!!!!!!");

            Console.WriteLine("connected");
            ClientData clientData = new ClientData(newClient);
            OnConnected(clientData);
            try
            {
                clientData.Run = true;
                clientData.initCount = 2;
                Thread check = new Thread(new ParameterizedThreadStart(CheckClient));
                check.Start(clientData);
                Thread read = new Thread(new ParameterizedThreadStart(asyncReadAsync));

                read.Start(clientData);

                //clientData.client.GetStream().BeginRead(clientData.readByteData, 0, clientData.readByteData.Length, new AsyncCallback(DataReceived), clientData);
                clientDic.TryAdd(clientData.TimeMills, clientData);

                log.Debug("!!!!!!!!!!!  AddClient START  !!!!!!!!!!!!!!!!");
                log.Debug("ClientManager AddClient.Click 50 check : " + check);
                log.Debug("ClientManager AddClient.Click 50 check : " + read);
                log.Debug("!!!!!!!!!!!  AddClient END  !!!!!!!!!!!!!!!!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void precess_receive(SocketAsyncEventArgs e)
        {
            if(e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                
            }
        }

        private async void asyncReadAsync(object obj)
        {
            ClientData clientdata = obj as ClientData;
            while (clientdata.Run)
            {
                try
                {
                    int result = 0;
                    if(clientdata.parameterCnt > 0)
                    {
                        //Thread.Sleep(110);
                        result = await clientdata.client.GetStream().ReadAsync(clientdata.readByteParameterData, 0, 70);
                        if (clientdata.initCount > 0)
                        {
                            Array.Clear(clientdata.readByteParameterData, 0, result);
                            clientdata.initCount--;
                            Console.WriteLine("init");
                        }
                    }
                    else
                    {
                        //Thread.Sleep(60);

                        //result = clientdata.client.GetStream().Read(clientdata.readByteData, 0, 57);
                        result = await clientdata.client.GetStream().ReadAsync(clientdata.readByteData, 0, 57);
                        if (clientdata.initCount > 0)
                        {
                            Array.Clear(clientdata.readByteData, 0, result);
                            clientdata.initCount--;
                            Console.WriteLine("init");
                        }
                    }

                    log.Debug("!!!!!!!!!!!!!!!!!!!        ClientManager ClientManager START      !!!!!!!!!!!!!!!!!!!");
                    log.Debug("ClientManager asyncReadAsync 99 command.result : " + result);
                    log.Debug("ClientManager asyncReadAsync 99 clientdata.channel.IsNewVersion : " + clientdata.channel.IsNewVersion);
                    log.Debug("!!!!!!!!!!!!!!!!!!!        ClientManager ClientManager END      !!!!!!!!!!!!!!!!!!!");

                    if (result > 0)
                    {
                        log.Debug("clientmanager 95 : " + result);
                        log.Debug("clientmanager 95 : " + clientdata.channel.IsNewVersion);

                        Console.WriteLine("-----------------");
                        Console.WriteLine("lenght: {0}",result);
                        Console.WriteLine("-----------------");

                        if (clientdata.channel.IsNewVersion)
                        {
                            if (clientdata.parameterCnt > 0)
                            {
                                byte[] slice = clientdata.readByteParameterData.Slice(result);
                                Array.Clear(clientdata.readByteParameterData, 0, result);

                                log.Debug("clientmanager 108 : " + slice);
                                log.Debug("clientmanager 108 : " + clientdata.readParameterData.Count);
                                log.Debug("clientmanager 108 : " + clientdata.readParameterData.ToArray());

                                Console.WriteLine("param read");

                                //slice.PrintHex(1);
                                clientdata.readParameterData.AddRange(slice);

                                log.Debug("!!!!!!!!!!!!!!!!!!!        11111111111111111111 START IsNewVersion     !!!!!!!!!!!!!!!!!!!");
                                log.Debug("ClientManager asyncReadAsync 127 slice : " + slice);
                                log.Debug("ClientManager asyncReadAsync 127 clientdata.readParameterData.Count : " + clientdata.readParameterData.Count);
                                log.Debug("ClientManager asyncReadAsync 127 clientdata.readParameterData.ToArray() : " + clientdata.readParameterData.ToArray());
                                log.Debug("!!!!!!!!!!!!!!!!!!!        11111111111111111111 END IsNewVersion     !!!!!!!!!!!!!!!!!!!");

                                if (clientdata.readParameterData.Count >= 70)
                                {
                                    byte[] receive = clientdata.readParameterData.ToArray();

                                    log.Debug("clientmanager 108 : " + receive);

                                    int s_idx = getNewStxIndex(receive);
                                    //receive.PrintHex(1);
                                    if (s_idx == 0)
                                    {
                                        if (receive.Length > 70)
                                        {
                                            byte[] cmd = receive.Slice(70);
                                            byte[] etc = receive.Slice(70, clientdata.readParameterData.Count - 70);
                                            clientdata.readParameterData.Clear();
                                            clientdata.readParameterData.AddRange(etc);
                                            CheckCommand(cmd, clientdata);
                                        }
                                        else
                                        {
                                            clientdata.readParameterData.Clear();
                                            CheckCommand(receive, clientdata);
                                        }
                                    }
                                    else if (s_idx == -1)
                                    {
                                        clientdata.readParameterData.Clear();
                                    }
                                    else
                                    {
                                        if (receive[s_idx - 1] == 0x34)
                                        {
                                            byte[] command = receive.Slice(s_idx);
                                            byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);

                                            if (command.Length == 70)
                                            {
                                                clientdata.readParameterData.Clear();
                                                clientdata.readParameterData.AddRange(etc);
                                            }
                                            else
                                            {
                                                clientdata.readParameterData.Clear();
                                                clientdata.readParameterData.AddRange(etc);
                                            }
                                            CheckCommand(command, clientdata);
                                        }
                                        else
                                        {
                                            clientdata.readParameterData.Clear();
                                        }
                                    }
                                }
                            }
                            else
                            {
                                byte[] slice = clientdata.readByteData.Slice(result);
                                Array.Clear(clientdata.readByteData, 0, clientdata.readByteData.Length);
                                clientdata.readCompleteData.AddRange(slice);
                                //slice.PrintHex(1);

                                log.Debug("!!!!!!!!!!!!!!!!!!!        2222222222222222 START      !!!!!!!!!!!!!!!!!!!");
                                log.Debug("ClientManager asyncReadAsync 127 clientdata.readCompleteData.Count : " + clientdata.readCompleteData.Count);
                                log.Debug("ClientManager asyncReadAsync 127 slice : " + slice);
                                log.Debug("ClientManager asyncReadAsync 127 clientdata.readByteData.Length : " + clientdata.readByteData.Length);
                                log.Debug("ClientManager asyncReadAsync 127 clientdata.readCompleteData : " + clientdata.readCompleteData);
                                log.Debug("!!!!!!!!!!!!!!!!!!!        2222222222222222 END      !!!!!!!!!!!!!!!!!!!");

                                if (clientdata.readCompleteData.Count >= 57)
                                {
                                    byte[] receive = clientdata.readCompleteData.ToArray();
                                    int s_idx = getNewStxIndex(receive);
                                    Console.WriteLine("s_idx={0} length={1}", s_idx, receive.Length);
                                    //receive.PrintHex(1);
                                    if (s_idx == 0)
                                    {
                                        if (receive.Length > 57)
                                        {
                                            byte[] cmd = receive.Slice(57);
                                            byte[] etc = receive.Slice(57, clientdata.readCompleteData.Count - 57);
                                            clientdata.readCompleteData.Clear();
                                            clientdata.readCompleteData.AddRange(etc);
                                            CheckCommand(cmd, clientdata);
                                        }
                                        else
                                        {
                                            clientdata.readCompleteData.Clear();
                                            CheckCommand(receive, clientdata);
                                        }
                                    }
                                    else if (s_idx == -1)
                                    {
                                        clientdata.readCompleteData.Clear();
                                    }
                                    else
                                    {
                                        if (receive[s_idx - 1] == 0x34)
                                        {
                                            byte[] command = receive.Slice(s_idx);
                                            byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);

                                            if (command.Length == 57)
                                            {
                                                clientdata.readCompleteData.Clear();
                                                clientdata.readCompleteData.AddRange(etc);
                                            }
                                            else
                                            {
                                                clientdata.readCompleteData.Clear();
                                                clientdata.readCompleteData.AddRange(etc);
                                            }
                                            CheckCommand(command, clientdata);
                                        }
                                        else
                                        {
                                            clientdata.readCompleteData.Clear();
                                        }
                                    }

                                }
                            }
                        }
                        else
                        {
                            if (clientdata.parameterCnt > 0)
                            {
                                byte[] slice = clientdata.readByteParameterData.Slice(result);
                                Array.Clear(clientdata.readByteParameterData, 0, result);
                                clientdata.readParameterData.AddRange(slice);

                                if (clientdata.readParameterData.Count >= 70)
                                {
                                    byte[] receive = clientdata.readParameterData.ToArray();
                                    int s_idx = getStxIndex(receive);

                                    //log.Debug("ClientManager asyncReadAsync 275 receive : " + receive);
                                    //log.Debug("ClientManager asyncReadAsync 275 s_idx : " + s_idx);
                                    ByteLogHelper.LogPacket(receive, "RX");

                                    Console.WriteLine("s_idx={0} length={1}", s_idx, receive.Length);
                                    //receive.PrintHex(1);
                                    if (s_idx == 0)
                                    {
                                        if (receive.Length > 70)
                                        {
                                            byte[] cmd = receive.Slice(70);
                                            byte[] etc = receive.Slice(70, clientdata.readParameterData.Count - 70);
                                            clientdata.readParameterData.Clear();
                                            clientdata.readParameterData.AddRange(etc);
                                            CheckCommand(cmd, clientdata);
                                        }
                                        else
                                        {
                                            clientdata.readParameterData.Clear();
                                            CheckCommand(receive, clientdata);
                                        }
                                    }
                                    else if (s_idx == -1)
                                    {
                                        clientdata.readParameterData.Clear();
                                    }
                                    else
                                    {
                                        if (receive[s_idx - 1] == 0xEF)
                                        {
                                            byte[] command = receive.Slice(s_idx);
                                            byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);

                                            if (command.Length == 70)
                                            {
                                                clientdata.readParameterData.Clear();
                                                clientdata.readParameterData.AddRange(etc);
                                            }
                                            else
                                            {
                                                clientdata.readParameterData.Clear();
                                                clientdata.readParameterData.AddRange(etc);
                                            }
                                            CheckCommand(command, clientdata);
                                        }
                                        else
                                        {
                                            clientdata.readParameterData.Clear();
                                        }
                                    }
                                }
                            }
                            else
                            {
                                byte[] slice = clientdata.readByteData.Slice(result);                                
                                Array.Clear(clientdata.readByteData, 0, result);
                                clientdata.readCompleteData.AddRange(slice);
                                if (clientdata.readCompleteData.Count >= 57)
                                {
                                    byte[] receive = clientdata.readCompleteData.ToArray();
                                    int s_idx = getStxIndex(receive);

                                    log.Debug("!!!!!!!!!!!!!!!!!!!        44444444444444 START      !!!!!!!!!!!!!!!!!!!");
                                    log.Debug("ClientManager asyncReadAsync 337 slice : " + slice);
                                    log.Debug("ClientManager asyncReadAsync 337 slice : " + s_idx);
                                    log.Debug("ClientManager asyncReadAsync 337 clientdata.readParameterData.Count : " + clientdata.readParameterData.Count);
                                    log.Debug("ClientManager asyncReadAsync 337 clientdata.readParameterData.ToArray() : " + clientdata.readParameterData.ToArray());
                                    ByteLogHelper.LogPacket(receive, "RX");
                                    log.Debug("!!!!!!!!!!!!!!!!!!!        44444444444444 END      !!!!!!!!!!!!!!!!!!!");
                                    Console.WriteLine("s_idx={0} length={1}", s_idx, receive.Length);
                                    //receive.PrintHex(1);
                                    if (s_idx == 0)
                                    {
                                        if (receive.Length > 57)
                                        {
                                            byte[] cmd = receive.Slice(57);
                                            byte[] etc = receive.Slice(57, clientdata.readCompleteData.Count - 57);
                                            clientdata.readCompleteData.Clear();
                                            clientdata.readCompleteData.AddRange(etc);
                                            CheckCommand(cmd, clientdata);                                            
                                        }
                                        else
                                        {
                                            clientdata.readCompleteData.Clear();
                                            CheckCommand(receive, clientdata);                                            
                                        }
                                    }
                                    else if(s_idx == -1)
                                    {
                                        clientdata.readCompleteData.Clear();
                                    }
                                    else
                                    {
                                        if (receive[s_idx - 1] == 0xEF)
                                        {
                                            byte[] command = receive.Slice(s_idx);
                                            byte[] etc = receive.Slice(s_idx, receive.Length - s_idx);                                          
                                            
                                            if (command.Length == 57)
                                            {
                                                clientdata.readCompleteData.Clear();
                                                clientdata.readCompleteData.AddRange(etc);
                                            }
                                            else
                                            {
                                                clientdata.readCompleteData.Clear();
                                                clientdata.readCompleteData.AddRange(etc);
                                            }
                                            CheckCommand(command, clientdata);
                                        }
                                        else
                                        {
                                            clientdata.readCompleteData.Clear();
                                        }
                                    }

                                }
                            }
                        }
                    }                    
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    RemoveClient(clientdata);
                }
                
            }
        }

        private void CheckClient(Object obj)
        {
            ClientData data = obj as ClientData;

            log.Debug("!!!!!!!!!!!  CheckClient CheckClient START  !!!!!!!!!!!!!!!!");
            log.Debug("ClientManager ReadErrorButton.Click 362 obj : " + obj);
            log.Debug("ClientManager ReadErrorButton.Click 362             log.Debug(\"ClientManager ReadErrorButton.Click 362 command : \" + data);\r\n : " + data);
            log.Debug("ClientManager ReadErrorButton.Click 362 data.parameterCnt : " + data.parameterCnt);
            log.Debug("!!!!!!!!!!!  CheckClient CheckClient END  !!!!!!!!!!!!!!!!");

            while (data.Run)
            {
                if(data.parameterCnt > 0)
                {
                    Console.WriteLine("ParameterLoad");
                    byte[] command = null;
                    if(data.errorCnt > 0)
                    {
                        command = Protocol.GetError(data.channel.IsNewVersion);
                    }
                    else
                    {
                        command = Protocol.GetParameter(data.channel.IsNewVersion);
                    }
                    if (data.client.Connected)
                    {
                        data.client.GetStream().Write(command, 0, command.Length);
                    }
                    else
                    {
                        RemoveClient(data);
                    }

                    log.Debug("!!!!!!!!!!!  CheckClient CheckClient START  !!!!!!!!!!!!!!!!");
                    log.Debug("ClientManager ReadErrorButton.Click 392 obj : " + obj);
                    log.Debug("ClientManager ReadErrorButton.Click 392");
                    log.Debug("ClientManager ReadErrorButton.Click 392 data.parameterCnt : " + command);
                    ByteLogHelper.LogPacket(command, "RX");
                    log.Debug("!!!!!!!!!!!  CheckClient CheckClient END  !!!!!!!!!!!!!!!!");

                    data.channel.TestTime += TimeSpan.FromSeconds(1);
                    data.readCompleteData.Clear();
                    data.parameterCnt--;
                }
                else
                {
                    try
                    {
                        byte[] command = null;
                        if (data.channel.IsNewVersion)
                        {
                            command = Protocol.GetNewCommand(1);
                        }
                        else
                        {
                            command = Protocol.GetCommand(1);
                        }

                        //Console.WriteLine("[{0}]write: " + command.byteToString(), DateTime.Now.ToString("HH:mm:ss"));
                        log.Debug("!!!!!!!!!!!  CheckClient CheckClient START  !!!!!!!!!!!!!!!!");
                        log.Debug("ClientManager ReadErrorButton.Click 418 obj : " + obj);
                        log.Debug("ClientManager ReadErrorButton.Click 418");
                        log.Debug("ClientManager ReadErrorButton.Click 418 data.parameterCnt : " + command);
                        ByteLogHelper.LogPacket(command, "RX");
                        log.Debug("!!!!!!!!!!!  CheckClient CheckClient END  !!!!!!!!!!!!!!!!");

                        if (data.client.Connected)
                        {
                            data.client.GetStream().Write(command, 0, command.Length);
                        }
                        else
                        {
                            RemoveClient(data);
                        }

                        log.Debug("ClientManager 403 : " + command);
                        log.Debug("ClientManager 403 : " + command.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        data.Run = false;
                        RemoveClient(data);
                    }
                }

                log.Debug("ClientManager 414 : " + data.errorCnt);

                if (data.errorCnt > 0)
                    data.errorCnt--;
                data.channel.TestTime += TimeSpan.FromSeconds(1);
                Thread.Sleep(1000);
            }
        }

        private void RemoveClient(ClientData targetClient)
        {
            ClientData client = null;
            targetClient.Run = false;
            bool remove = clientDic.TryRemove(targetClient.TimeMills, out client);

            log.Debug("!!!!!!!!!!!  RemoveClient RemoveClient START  !!!!!!!!!!!!!!!!");
            log.Debug("ClientManager ReadErrorButton.Click 413 obj : " + remove);
            log.Debug("!!!!!!!!!!!  RemoveClient RemoveClient END  !!!!!!!!!!!!!!!!");

            if (remove)
            {
                OnDisconnected(targetClient);
            }
        }

        private int getStxIndex(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0xCC)
                {
                    if (i + 2 < data.Length)
                    {
                        if (data[i + 1] == 0x00)
                        {
                            if (data[i + 2] == 0x99 || data[i + 2] == 0xB9 || data[i + 2] == 0xA0 || data[i + 2] == 0xAA)
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            return -1;
        }

        public void CheckCommand(byte[] array, ClientData data)
        {
            //array.PrintHex(1);
            byte check = Protocol.GetCheckSum(array, 1, array.Length - 3);
            log.Debug("!!!!!!!!!!!  CheckCommand CheckCommand START  !!!!!!!!!!!!!!!!");
            log.Debug("ClientManager ReadErrorButton.Click 548 array[2] : " + array[2]);
            ByteLogHelper.LogPacket(array, "RX");
            log.Debug("!!!!!!!!!!!  CheckCommand CheckCommand END  !!!!!!!!!!!!!!!!");
            
            try
            {
                switch (array[2])
                {
                    case 0xA0:
                        if (array.Length != 57)
                            return;
                        //Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[55].ToString("X2"));
                        data.channel.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            data.channel.SetView(array);
                        }));
                        break;
                    case 0xAA:
                        if (array.Length != 57)
                            return;
                        //Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[55].ToString("X2"));
                        data.channel.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            data.channel.SetView(array);
                        }));
                        break;
                    case 0x99:
                        if (array.Length != 70)
                            return;
                        //Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[68].ToString("X2"));
                        if (data.channel.ParameterMode && data.channel.ParameterWindow != null)
                        {
                            data.channel.ParameterWindow.setParameter(array);
                        }
                        break;
                    case 0xB9:
                        if (array.Length != 70)
                            return;
                        //Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[68].ToString("X2"));
                        if (data.channel.ParameterMode && data.channel.ParameterWindow != null)
                        {
                            data.channel.ParameterWindow.setError(array);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        private int getNewStxIndex(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x12)
                {
                    if (i + 2 < data.Length)
                    {
                        if (data[i + 1] == 0x01)
                        {
                            if (data[i + 2] == 0x99 || data[i + 2] == 0xB9 || data[i + 2] == 0xA0 || data[i + 2] == 0xAA)
                            {
                                return i;
                            }
                        }
                    }
                }
            }
            return -1;
        }

        public void StartDummyChannel(ClientData dummyClient)
        {
            if (!dummyClientDic.TryAdd(dummyClient.TimeMills, dummyClient))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    dummyClient.channel?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        dummyClient.channel.run = true;
                    }));

                    while (dummyClient.Run)
                    {
                        byte[] rsp = DummySerialPortNs.DummySerialPort.RSP_StartStopStatus.ToArray();
                        if (rsp == null || rsp.Length != 57)
                            throw new InvalidOperationException("RSP_StartStopStatus must be 57 bytes.");

                                          // ✅ SetView 통과 조건 강제 보정
                        NormalizeStatusPacketForSetView(dummyClient.channel, rsp);

                        var sample = _dummyGen.Next();

                                          // 엑셀 정의서 기준 오프셋에 값 세팅 + 체크섬 갱신
                        DummyFramePatcher.PatchStatusResponse57(rsp, sample);

                                          // ✅ UI 갱신은 SetView로 확정
                        dummyClient.channel?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            dummyClient.channel.IsNewVersion = dummyClient.channel.ApplyNewVersion?.IsChecked == true;
                            dummyClient.channel.IsNewVersion = true;
                            dummyClient.channel.SetView(rsp);
                        }));

                        await Task.Delay(200);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    dummyClientDic.TryRemove(dummyClient.TimeMills, out _);
                }
            });
        }

        public static void StopAllDummyChannels()
        {
            foreach (var kv in dummyClientDic.ToArray())
            {
                if (kv.Value != null)
                    StopDummyChannel(kv.Value);
            }

            dummyClientDic.Clear();
        }

        public static void StopDummyChannel(ClientData dummyClient)
        {
            if (dummyClient == null) return;
            dummyClient.Run = false; // ✅ 루프 종료 트리거
            dummyClient.channel?.Dispatcher.BeginInvoke(new Action(() =>
            {
                dummyClient.channel.run = false; // UI 상태도 같이 끔(선택)
            }));
        }

        private static void NormalizeStatusPacketForSetView(ChannelItem channel, byte[] pkt57)
        {
            if (pkt57 == null || pkt57.Length != 57) return;

            // SetView가 길이 체크로 보는 값
            pkt57[3] = 57;

            // 채널 버전에 맞춰 STX/ETX 강제
            bool isNew = channel?.IsNewVersion == true;
            if (isNew)
            {
                pkt57[0] = 0x12;
                pkt57[56] = 0x34;
            }
            else
            {
                pkt57[0] = 0xCC;
                pkt57[56] = 0xEF;
            }
        }


    }
}
