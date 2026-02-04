using OxyPlot;
using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BliMonitorTest
{
    /// <summary>
    /// MultiWindow1.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MultiWindow1 : Window
    {
        private MultiWindow2 multiWindow2;
        private int count = 0;
        public List<StreamWriter> files = new List<StreamWriter>();
        private Dictionary<int, int> seriesList = new Dictionary<int, int>();
        private AsyncServer server;
        public Dictionary<long, int> channelList = new Dictionary<long, int>();
        private List<OxyColor> colorList = new List<OxyColor>();

        public MultiWindow1()
        {
            InitializeComponent();

            Loaded += MultiWindow1_Loaded;

            setItems(Item1);
            setItems(Item2);
            setItems(Item3);
            setItems(Item4);
            Item1.ItemIndex = 1;
            Item2.ItemIndex = 2;
            Item3.ItemIndex = 3;
            Item4.ItemIndex = 4;
            Item1.chartView = Chart;
            Item2.chartView = Chart;
            Item3.chartView = Chart;
            Item4.chartView = Chart;
            Item1.seriesList = seriesList;
            Item2.seriesList = seriesList;
            Item3.seriesList = seriesList;
            Item4.seriesList = seriesList;
            colorList.Add(OxyColor.FromRgb(255, 0, 0));
            colorList.Add(OxyColor.FromRgb(0, 0, 255));
            colorList.Add(OxyColor.FromRgb(246, 190, 7));
            colorList.Add(OxyColor.FromRgb(7, 200, 246));
            colorList.Add(OxyColor.FromRgb(255, 0, 255));
            colorList.Add(OxyColor.FromRgb(1, 249, 125));
            colorList.Add(OxyColor.FromRgb(14, 128, 71));
            colorList.Add(OxyColor.FromRgb(0, 0, 0));
            server = new AsyncServer(this);
            Closed += MultiWindow1_Closed;
            Closing += MultiWindow1_Closing;
            SaveCheck.Checked += SaveCheck_Checked;
            SaveCheck.Unchecked += SaveCheck_Unchecked;
            StartButton.Click += (s, e) =>
            {
                if (server.run)
                {
                    //MessageBox.Show("이미 작동 중 입니다.");
                    ToastMessage.ToastService.AppToast.Show("이미 작동 중 입니다.");
                    return;
                }
                server.Start();

                StartDummyChannelsIfNeeded();       // 더미 패킷전송 실행
            };
            StopButton.Click += (s, e) =>
            {
                if (ClientManager.clientDic.Count > 0)
                {
                    Task.Run(new Action(() =>
                    {
                        foreach (var item in ClientManager.clientDic)
                        {
                            if (item.Value.client != null && item.Value.client.Connected)
                            {
                                item.Value.Run = false;
                                byte[] cmd = null;
                                cmd = Protocol.GetNewCommand(3);

                                item.Value.channel.CloseWriter();
                                item.Value.client.GetStream().Write(cmd, 0, cmd.Length);
                                item.Value.client.Close();
                            }
                        }
                    }));
                }
                if (server.listener != null)
                {
                    server.listener.Stop();
                }

                BliMonitorTest.util.ClientManager.StopAllDummyChannels();       // ✅ 더미 채널 정지
                server.run = false;
            };
        }

        private void FileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            Item1.FName = FileName.Text;
            Item1.Modify = true;
            Item2.FName = FileName.Text;
            Item2.Modify = true;
            Item3.FName = FileName.Text;
            Item3.Modify = true;
            Item4.FName = FileName.Text;
            Item4.Modify = true;
            //if(multiWindow2 != null)
            //{
            multiWindow2.Item1.FName = FileName.Text;
            multiWindow2.Item1.Modify = true;
            multiWindow2.Item2.FName = FileName.Text;
            multiWindow2.Item2.Modify = true;
            multiWindow2.Item3.FName = FileName.Text;
            multiWindow2.Item3.Modify = true;
            multiWindow2.Item4.FName = FileName.Text;
            multiWindow2.Item4.Modify = true;
            //}
        }

        private void MultiWindow1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ClientManager.clientDic != null)
            {
                foreach (var item in ClientManager.clientDic)
                {
                    if (item.Value != null)
                    {
                        item.Value.Run = false;
                    }
                }
            }
            if (server != null)
            {                
                if (server.listener != null)
                {
                    try
                    {
                        server.listener.Stop();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("catch : {0}", ex.ToString());
                    }
                }
                server.run = false;
            }
        }

        private void MultiWindow1_Closed(object sender, EventArgs e)
        {
            if (multiWindow2 != null)
            {
                multiWindow2.Close();
            }
            if (server != null)
            {
                if (server.listener != null)
                {
                    try
                    {
                        
                        foreach (var item in ClientManager.clientDic)
                        {
                            if(item.Value != null)
                            {
                                item.Value.Run = false;
                            }                            
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("catch : {0}", ex.ToString());
                    }
                }
                server.run = false;
            }
        }


        private void setItems(ChannelItem item)
        {
            item.Item1.label.Content = "히터 온도";
            item.Item2.label.Content = "배기 온도";
            item.Item3.label.Content = "열풍히터온도";
            item.Item4.label.Content = "메인모터운전";
            item.Item5.label.Content = "배기팬 풍량";
            item.Item6.label.Content = "운전 시간";

            item.Item11.label.Content = "히터오프타임";
            item.Item12.label.Content = "배기온도평균";
            item.Item13.label.Content = "열풍히터Duty";
            item.Item14.label.Content = "메인모터전류";
            item.Item15.label.Content = "열풍팬 풍량";
            item.Item16.label.Content = "만수 감지";

            item.VersionBox.label.Content = "Model";
            item.CompileBox.label.Content = "Compile";
            item.StateBox.label.Content = "상태";
            //item.Item24.label.Content = "남은시간";
            ////item.Item25.label.Content = "Error";
            //item.Item26.label.Content = "MODE";
        }

        private void MultiWindow1_Loaded(object sender, RoutedEventArgs e)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            VersionText.Content = "ver: " + version;

            Item1.setHandler(Item1Check_Click);
            Item2.setHandler(Item2Check_Click);
            Item3.setHandler(Item3Check_Click);
            Item4.setHandler(Item4Check_Click);
            multiWindow2 = new MultiWindow2();
            //multiWindow2.Owner = this;
            multiWindow2.Show();
            Task.Delay(100).ContinueWith(task =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    WindowState = WindowState.Maximized;
                }));
            });
            FileName.TextChanged += FileName_TextChanged;
            FileName.Text = DateTime.Now.ToString("TEST_yy년MM월dd일HH시mm분ss초");
            string now = DateTime.Now.ToString("yyyy_MM_dd_HH:mm:dd");
            FileNameBox.Text = now;
            SaveCheck.IsChecked = true;

        }

        private void setSaveLocation(bool save)
        {
            Item1.SaveInDesktop = save;
            Item2.SaveInDesktop = save;
            Item3.SaveInDesktop = save;
            Item4.SaveInDesktop = save;
            //if (multiWindow2 != null)
            //{
            multiWindow2.Item1.SaveInDesktop = save;
            multiWindow2.Item2.SaveInDesktop = save;
            multiWindow2.Item3.SaveInDesktop = save;
            multiWindow2.Item4.SaveInDesktop = save;
            //}
        }

        private void SaveCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            setSaveLocation(false);
        }

        private void SaveCheck_Checked(object sender, RoutedEventArgs e)
        {
            setSaveLocation(true);
        }

        private int getIndex()
        {
            for (int i = 0; i < 8; i++)
            {
                if (!seriesList.ContainsValue(i))
                {
                    return i;
                }
            }
            return -1;
        }

        public int GetChannelIndex()
        {
            for (int i = 0; i < 8; i++)
            {
                if (!channelList.ContainsValue(i))
                {
                    return i;
                }
            }
            return -1;
        }

        public ChannelItem GetChannelItem(int index)
        {
            switch (index)
            {
                case 0:
                    return Item1;
                case 1:
                    return Item2;
                case 2:
                    return Item3;
                case 3:
                    return Item4;
                case 4:
                    return multiWindow2.Item1;
                case 5:
                    return multiWindow2.Item2;
                case 6:
                    return multiWindow2.Item3;
                case 7:
                    return multiWindow2.Item4;
            }
            return null;
        }

        public void Item1Check_Click(object sender, RoutedEventArgs e)
        {

            /* 운영 배포시 주석 해제
            if (Item1.client == null || !Item1.client.Connected)
            {
                (sender as CheckBox).IsChecked = false;
                ToastMessage.ToastService.AppToast.Show("연결 되지 않았습니다.");
                return;
            }
            */

            switch ((sender as Control).Name)
            {
                case "Item1Check":
                    if (Item1.Item1Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[10] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터 온도", Item1.Channel));
                            //Chart.seriesList[index].ItemsSource = Item1.list1;
                            try
                            {
                                //Chart.setAxis(Chart.seriesList[index], 0);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }
                        else
                        {
                            Item1.Item1Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[10];
                        seriesList.Remove(10);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item2Check":
                    if (Item1.Item2Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[11] = index;
                            Chart.setLegend(index, String.Format("CH{0} 배기 온도", Item1.Channel));
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            //Chart.seriesList[index].ItemsSource = Item1.list2;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item1.Item2Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[11];
                        seriesList.Remove(11);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item3Check":
                    if (Item1.Item3Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[12] = index;

                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 평균히터오프타임", Item1.Channel));
                        }
                        else
                        {
                            Item1.Item3Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[12];
                        seriesList.Remove(12);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item4Check":
                    if (Item1.Item4Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[13] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터운전", Item1.Channel));
                            //Chart.seriesList[index].ItemsSource = Item1.list4;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item1.Item4Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[13];
                        seriesList.Remove(13);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item5Check":
                    if (Item1.Item5Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[14] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터오프타임", Item1.Channel));
                            //Chart.seriesList[index].ItemsSource = Item1.list5;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item1.Item5Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[14];
                        seriesList.Remove(14);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item6Check":
                    if (Item1.Item6Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[15] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 배기온도평균", Item1.Channel));
                            //Chart.seriesList[index].ItemsSource = Item1.list6;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item1.Item6Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[15];
                        seriesList.Remove(15);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item7Check":
                    if (Item1.Item7Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[16] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 열풍히터duty", Item1.Channel));
                            //Chart.seriesList[index].ItemsSource = Item1.list7;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item1.Item7Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[16];
                        seriesList.Remove(16);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item8Check":
                    if (Item1.Item8Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[17] = index;
                            Chart.ViewModel.setSeries(index, 2, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터전류", Item1.Channel));
                            //Chart.seriesList[index].ItemsSource = Item1.list8;
                            //Chart.setAxis(Chart.seriesList[index], 2);
                        }
                        else
                        {
                            Item1.Item8Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[17];
                        seriesList.Remove(17);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
            }
        }
        public void Item2Check_Click(object sender, RoutedEventArgs e)
        {
            //if (Item2.client == null || !Item2.client.Connected)
            //{
            //    (sender as CheckBox).IsChecked = false;
            //    MessageBox.Show("연결 되지 않았습니다.");
            //    return;
            //}
            switch ((sender as Control).Name)
            {
                case "Item1Check":
                    if (Item2.Item1Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[20] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터 온도", Item2.Channel));
                            //Chart.seriesList[index].ItemsSource = Item2.list1;
                            try
                            {
                                //Chart.setAxis(Chart.seriesList[index], 0);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }
                        else
                        {
                            Item2.Item1Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[20];
                        seriesList.Remove(20);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item2Check":
                    if (Item2.Item2Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[21] = index;
                            Chart.setLegend(index, String.Format("CH{0} 배기 온도", Item2.Channel));
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            //Chart.seriesList[index].ItemsSource = Item2.list2;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item2.Item2Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[21];
                        seriesList.Remove(21);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item3Check":
                    if (Item2.Item3Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[22] = index;

                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 평균히터오프타임", Item2.Channel));
                        }
                        else
                        {
                            Item2.Item3Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[22];
                        seriesList.Remove(22);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item4Check":
                    if (Item2.Item4Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[23] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터운전", Item2.Channel));
                            //Chart.seriesList[index].ItemsSource = Item2.list4;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item2.Item4Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[23];
                        seriesList.Remove(23);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item5Check":
                    if (Item2.Item5Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[24] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터오프타임", Item2.Channel));
                            //Chart.seriesList[index].ItemsSource = Item2.list5;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item2.Item5Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[24];
                        seriesList.Remove(24);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item6Check":
                    if (Item2.Item6Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[25] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 배기온도평균", Item2.Channel));
                            //Chart.seriesList[index].ItemsSource = Item2.list6;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item2.Item6Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[25];
                        seriesList.Remove(25);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item7Check":
                    if (Item2.Item7Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[26] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 열풍히터duty", Item2.Channel));
                            //Chart.seriesList[index].ItemsSource = Item2.list7;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item2.Item7Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[26];
                        seriesList.Remove(26);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item8Check":
                    if (Item2.Item8Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[27] = index;
                            Chart.ViewModel.setSeries(index, 2, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터전류", Item2.Channel));
                            //Chart.seriesList[index].ItemsSource = Item2.list8;
                            //Chart.setAxis(Chart.seriesList[index], 2);
                        }
                        else
                        {
                            Item2.Item8Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[27];
                        seriesList.Remove(27);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
            }
        }

        public void Item3Check_Click(object sender, RoutedEventArgs e)
        {
            //if (Item3.client == null || !Item3.client.Connected)
            //{
            //    (sender as CheckBox).IsChecked = false;
            //    MessageBox.Show("연결 되지 않았습니다.");
            //    return;
            //}
            switch ((sender as Control).Name)
            {
                case "Item1Check":
                    if (Item3.Item1Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[30] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터 온도", Item3.Channel));
                            //Chart.seriesList[index].ItemsSource = Item3.list1;
                            try
                            {
                                //Chart.setAxis(Chart.seriesList[index], 0);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }
                        else
                        {
                            Item3.Item1Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[30];
                        seriesList.Remove(30);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item2Check":
                    if (Item3.Item2Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[31] = index;
                            Chart.setLegend(index, String.Format("CH{0} 배기 온도", Item3.Channel));
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            //Chart.seriesList[index].ItemsSource = Item3.list2;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item3.Item2Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[31];
                        seriesList.Remove(31);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item3Check":
                    if (Item3.Item3Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[32] = index;

                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 평균히터오프타임", Item3.Channel));
                        }
                        else
                        {
                            Item3.Item3Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[32];
                        seriesList.Remove(32);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item4Check":
                    if (Item3.Item4Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[33] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터운전", Item3.Channel));
                            //Chart.seriesList[index].ItemsSource = Item3.list4;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item3.Item4Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[33];
                        seriesList.Remove(33);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item5Check":
                    if (Item3.Item5Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[34] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터오프타임", Item3.Channel));
                            //Chart.seriesList[index].ItemsSource = Item3.list5;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item3.Item5Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[34];
                        seriesList.Remove(34);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item6Check":
                    if (Item3.Item6Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[35] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 배기온도평균", Item3.Channel));
                            //Chart.seriesList[index].ItemsSource = Item3.list6;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item3.Item6Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[35];
                        seriesList.Remove(35);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item7Check":
                    if (Item3.Item7Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[36] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 열풍히터duty", Item3.Channel));
                            //Chart.seriesList[index].ItemsSource = Item3.list7;
                            //Chart.setAxis(Chart.seriesList[index], 1);
                        }
                        else
                        {
                            Item3.Item7Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[36];
                        seriesList.Remove(36);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
                case "Item8Check":
                    if (Item3.Item8Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[37] = index;
                            Chart.ViewModel.setSeries(index, 2, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터전류", Item3.Channel));
                            //Chart.seriesList[index].ItemsSource = Item3.list8;
                            //Chart.setAxis(Chart.seriesList[index], 2);
                        }
                        else
                        {
                            Item3.Item8Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[37];
                        seriesList.Remove(37);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                        //Chart.seriesList[index].ItemsSource = null;
                    }
                    break;
            }
        }
        public void Item4Check_Click(object sender, RoutedEventArgs e)
        {

            //if (Item4.client == null || !Item4.client.Connected)
            //{
            //    (sender as CheckBox).IsChecked = false;
            //    MessageBox.Show("연결 되지 않았습니다.");
            //    return;
            //}
            switch ((sender as Control).Name)
            {
                case "Item1Check":
                    if (Item4.Item1Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[40] = index;
                            Chart.ViewModel.setSeries(index, 0, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터 온도", Item4.Channel));
                        }
                        else
                        {
                            Item4.Item1Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[40];
                        seriesList.Remove(40);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
                case "Item2Check":
                    if (Item4.Item2Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[41] = index;
                            Chart.setLegend(index, String.Format("CH{0} 배기 온도", Item4.Channel));
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                        }
                        else
                        {
                            Item4.Item2Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[41];
                        seriesList.Remove(41);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
                case "Item3Check":
                    if (Item4.Item3Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[42] = index;

                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 평균히터오프타임", Item4.Channel));
                        }
                        else
                        {
                            Item4.Item3Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[42];
                        seriesList.Remove(42);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
                case "Item4Check":
                    if (Item4.Item4Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[43] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터운전", Item4.Channel));
                        }
                        else
                        {
                            Item4.Item4Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[43];
                        seriesList.Remove(43);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
                case "Item5Check":
                    if (Item4.Item5Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[44] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 히터오프타임", Item4.Channel));
                        }
                        else
                        {
                            Item4.Item5Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[44];
                        seriesList.Remove(44);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
                case "Item6Check":
                    if (Item4.Item6Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[45] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 배기온도평균", Item4.Channel));
                        }
                        else
                        {
                            Item4.Item6Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[45];
                        seriesList.Remove(45);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
                case "Item7Check":
                    if (Item4.Item7Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[46] = index;
                            Chart.ViewModel.setSeries(index, 1, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 열풍히터duty", Item4.Channel));
                        }
                        else
                        {
                            Item4.Item7Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[46];
                        seriesList.Remove(46);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
                case "Item8Check":
                    if (Item4.Item8Check.IsChecked.Value)
                    {
                        if (seriesList.Count < 8)
                        {
                            int index = getIndex();
                            seriesList[47] = index;
                            Chart.ViewModel.setSeries(index, 2, colorList[index]);
                            Chart.setLegend(index, String.Format("CH{0} 메인모터전류", Item4.Channel));
                        }
                        else
                        {
                            Item4.Item8Check.IsChecked = false;
                            //MessageBox.Show("최대 8개 선택 가능합니다.");
                            ToastMessage.ToastService.AppToast.Show("최대 8개 선택 가능합니다.");
                        }
                    }
                    else
                    {
                        int index = seriesList[47];
                        seriesList.Remove(47);
                        Chart.ViewModel.unSetSeries(index);
                        Chart.setLegend(index, "");
                    }
                    break;
            }
        }

        private void StartDummyChannelsIfNeeded()
        {
                     // 0~7 (총 8채널)
            for (int uiIndex = 0; uiIndex < 8; uiIndex++)
            {
                var ch = GetChannelItem(uiIndex);
                if (ch == null) continue;

                if (!ch.IsDummyEnabled) continue;

                            // ✅ clientNumber 규칙: 실기구가 channel.Channel = data.clientNumber - 10 이므로
                            // 더미도 동일하게 10 + 채널번호로 맞추기
                int clientNumber = 10 + uiIndex;

                             // UI index 매핑도 실기구와 동일하게 window.channelList 사용
                long key = DateTimeOffset.Now.Ticks + clientNumber;
                channelList[key] = uiIndex;

                             // 기존 OnConnected와 유사하게 UI 초기화 + 채널 번호 세팅
                ch.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ch.clearData();
                                   // ClearCheck(ch);  // 실기구에서 쓰던 체크 초기화 함수가 필요하면 호출
                    ch.Channel = clientNumber - 10; // 즉 uiIndex
                }));

                             // ClientData 더미 생성 후 시작
                var dummyClient = new BliMonitorTest.util.ClientData(ch, clientNumber)
                {
                    TimeMills = key, // 위에서 만든 key로 통일(중요: channelList 키와 맞추기)
                    Run = true
                };

                var manager = new ClientManager();
                manager.StartDummyChannel(dummyClient);
            }
        }

    }
}
