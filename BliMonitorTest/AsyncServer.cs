using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using log4net;

namespace BliMonitorTest
{
    public delegate void MessageHandler(byte[] message, int index);

    public class AsyncServer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AsyncServer));

        ClientManager _clientManager = null;
        ConcurrentBag<byte[]> receiveLog = null;
        Thread CheckThread = null;
        public TcpListener listener = null;
        public bool run = false;
        MultiWindow1 window;
        ParameterWindow parameterWindow;

        public AsyncServer(MultiWindow1 window)
        {
            this.window = window;
        }
        
        public void Start()
        {
            run = true;
            _clientManager = new ClientManager();
            _clientManager.OnConnected += OnConnected;
            _clientManager.OnDisconnected += OnDisconnected;
            _clientManager.OnReceived += OnDataReceived;
            if (listener != null)
                listener = null;
            listener = new TcpListener(new IPEndPoint(IPAddress.Any, 7755));
            listener.Start();
            Console.WriteLine("start");
            Task serverStart = Task.Run(() =>
            {
                log.Debug("asyncReadAsync 61 run : ");
                log.Debug(run);

                AsycnServerStart();
            });
            //CheckThread = new Thread(StateCheckLoop);
            //CheckThread.Start();
        }        

        private void StateCheckLoop()
        {
            while (run)
            {
                foreach (var item in ClientManager.clientDic)
                {
                    if (item.Value.channel.ParameterMode)
                    {
                        continue;
                    }
                    try
                    {
                        byte[] sendByteData = Protocol.GetCommand(1);
                        item.Value.client.GetStream().Write(sendByteData, 0, sendByteData.Length);
                        if (!item.Value.channel.Response)
                        {
                            item.Value.channel.NonResponse++;
                            if (item.Value.channel.NonResponse > 20)
                            {
                                window.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    item.Value.channel.StateBox.cont.Content = "응답없음";
                                }));
                                //item.Value.channel
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        ClientData client = null;
                        bool remove = ClientManager.clientDic.TryRemove(item.Value.TimeMills, out client);
                        if (remove)
                        {
                            OnDisconnected(item.Value);
                        }
                    }
                }
                Thread.Sleep(1000);
            }
        }

        private void AsycnServerStart()
        {
            Console.WriteLine("Start");
            while (run)
            {
                try
                {
                    Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
                    acceptTask.Wait();
                    TcpClient newClient = acceptTask.Result;

                    _clientManager.AddClient(newClient);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"here : {e}");
                    if (!run)
                        break;
                }
            }
            Console.WriteLine("Server Closed");
        }

        private void OnConnected(ClientData data)
        {
            Console.WriteLine("connected");
            int index = window.GetChannelIndex();
            Console.WriteLine("Channel {0}", index);

            if (index > -1)
            {
                ChannelItem channel = window.GetChannelItem(index);
                if (channel != null)
                {
                    window.channelList[data.TimeMills] = index;
                    
                    channel.Dispatcher.BeginInvoke(new Action(() => {
                        channel.clearData();
                        ClearCheck(channel);
                    }));
                    channel.Channel = data.clientNumber - 10;
                    channel.TimeMills = data.TimeMills;
                    data.channel = channel;
                    channel.OnCheckChanged += () => {
                        data.readCompleteData.Clear();
                        data.readParameterData.Clear();
                        Array.Clear(data.readByteData, 0, data.readByteData.Length);
                        Array.Clear(data.readByteParameterData, 0, data.readByteParameterData.Length);
                        channel.Dispatcher.BeginInvoke(new Action(() => {
                            if (channel.Item3Check.IsChecked.Value)
                            {
                                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item3Check, new object[0]);
                                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item3Check, new object[0]);
                            }

                            channel.Item3.label.Content = "평균히터오프타임";
                        }));

                    };
                    channel.OnParameterLoadAction += (i) =>
                    {
                        if (data.readParameterData == null)
                            data.readParameterData = new List<byte>();
                        data.readParameterData.Clear();
                        Array.Clear(data.readByteParameterData, 0, data.readByteParameterData.Length);
                        data.parameterCnt = 2;
                        if(i == 1)
                            data.errorCnt = 2;
                    };
                    channel.client = data.client;
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        channel.StateBox.cont.Content = "Connected";
                    }));
                }
            }
        }

        private void ClearCheck(ChannelItem channel)
        {
            if (channel.Item1Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item1Check, new object[0]);
            }
            if (channel.Item2Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item2Check, new object[0]);
            }
            if (channel.Item3Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item3Check, new object[0]);
            }
            if (channel.Item4Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item4Check, new object[0]);
            }
            if (channel.Item5Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item5Check, new object[0]);
            }
            if (channel.Item6Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item6Check, new object[0]);
            }
            if (channel.Item7Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item7Check, new object[0]);
            }
            if (channel.Item8Check.IsChecked.Value)
            {
                typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(channel.Item8Check, new object[0]);
            }
        }

        private void OnDisconnected(ClientData data)
        {
            int index = window.channelList[data.TimeMills];
            ChannelItem channel = window.GetChannelItem(index);
            //channel.Dispatcher.BeginInvoke(new Action(() => { ClearCheck(channel); }));
            channel.client.Close();
            data.channel.Dispatcher.BeginInvoke(new Action(() =>
            {
                channel.StateBox.cont.Content = "Disconnected";
                channel.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                channel.StateBox.cont.Background = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                //channel.clearData();
            }));

            window.channelList.Remove(data.TimeMills);
        }


        private void OnDataReceived(ClientData data)
        {            
            data.ResponseCount++;
            data.channel.NonResponse = 0;
            
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (data.channel.ParameterMode && data.channel.ParameterWindow != null)
                {
                    if (data.readByteParameterData[2] == 0xB9)
                    {
                        data.channel.ParameterWindow.setError(data.readByteParameterData);
                    }
                    else if (data.readByteParameterData[2] == 0x99)
                    {
                        data.channel.ParameterWindow.setParameter(data.readByteParameterData);
                    }
                }
                else
                {
                    Console.WriteLine("setview");
                    //window.SetView(data.readCompleteData.ToArray(), data.channel);
                    data.readCompleteData.Clear();
                }
            }));
        }
    }
}
