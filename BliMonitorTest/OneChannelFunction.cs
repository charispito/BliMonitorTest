using OxyPlot;
using BliMonitorTest.data;
using BliMonitorTest.util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest
{
    partial class OneChannelWindow
    {
        private DateTime? _dummyStartTime;

        public string GetDateTime()
        {
            DateTime NowDate = DateTime.Now;
            return NowDate.ToString("yyyy-MM-dd HH:mm:ss") + ":" + NowDate.Millisecond.ToString("000");
        }
       
        private string CommandToString(byte[] command)
        {
            string hex = "";
            foreach (byte b in command)
            {
                hex += " " + b.ToString("X2");
            }
            return hex;
        }       

        private void CheckCommand(byte[] array)
        {
            array.PrintHex(1);
            byte check = Protocol.GetCheckSum(array, 1, array.Length - 3);
            try
            {
                switch (array[2])
                {
                    case 0xA0:
                        Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[55].ToString("X2"));
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            setView(array);
                        }));
                        break;
                    case 0xAA:
                        Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[55].ToString("X2"));
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            setView(array);
                        }));
                        break;
                    case 0x99:
                        Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[68].ToString("X2"));
                        if (channel.ParameterMode && channel.parameterWindow != null)
                        {
                            channel.parameterWindow.setParameter(array);
                        }
                        break;
                    case 0xB9:
                        Console.WriteLine("CheckSum: {0}, NewCheckSum: {1}, ReceivedCheckSum: {2}", check.ToString("X2"), (check ^ 0xFF).ToString("X2"), array[68].ToString("X2"));
                        if (channel.ParameterMode && channel.parameterWindow != null)
                        {
                            channel.parameterWindow.setError(array);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            
        }

        private void setView(byte[] data)
        {
            if(data.Length < 4 || data.Length != data[3])
            {
                return;
            }

            bool IsDummyEnabled = (UseDummyCheck?.IsChecked == true);

            // 기본은 기존 로직대로 TestTime 기반
            long total_second = (long)TestTime.TotalSeconds;
            double total_minute = total_second / 60.0;

            // (기존에 있던 보정 로직 유지: 필요 없으면 제거 가능)
            double remain_second = (double)(total_second % 60) / 100.0;
            double remain_value = 0.40 / 60.0;

            
            if (total_second > 0)
            {
                total_minute += (remain_second + remain_value * (total_second % 60));
            }
            else
            {
                total_minute += remain_second;
            }

            /*
            // 더미일경우
            if (IsDummyEnabled)
            {
                // null일 때만 1회 설정 (핵심)
                if (_dummyStartTime == null)
                    _dummyStartTime = DateTime.Now;

                TimeSpan elapsed = DateTime.Now - _dummyStartTime.Value;

                total_second = (long)elapsed.TotalSeconds;
                total_minute = elapsed.TotalMinutes;   // 분 단위 double (가장 깔끔)
            }
            else
            {
                // 더미 해제 시 리셋(선택)
                _dummyStartTime = null;

                // 다시 TestTime 기반으로 (원래 의미 유지)
                total_second = (long)TestTime.TotalSeconds;
                total_minute = TestTime.TotalMinutes;
            }
            */

            // null일 때만 1회 설정 (핵심)
            if (_dummyStartTime == null)
                _dummyStartTime = DateTime.Now;

            TimeSpan elapsed = DateTime.Now - _dummyStartTime.Value;

            total_second = (long)elapsed.TotalSeconds;
            total_minute = elapsed.TotalMinutes;   // 분 단위 double (가장 깔끔)

            int motorRun = data[5];
            int heateroff = data[7]; //히터 오프타임
            int heatertemp = data[6]; //히터 온도
            int airtemp = data[8]; //배기 온도
            int airaverage = data[9];
            int airheatertemp = data[10]; //열풍 온도
            int heaterduty = data[11];
            int mode = data[15];
            int minute = data[16];
            int second = data[17];
            if(mode == 7)
            {
                channel.run = false;
            }
            else
            {
                if (!channel.run)
                {
                    channel.run = true;
                }
            }
            int t_hour = data[18];
            int t_min = data[19];
            int t_sec = data[20];
            int runTime = data[21];
            string time = string.Format("{0:D2}:{1:D2}", minute, second);
            string t_time = string.Format("{0:D2}:{1:D2}:{2:D2}", t_hour, t_min, t_sec);
            byte[] current = { data[13], data[14] };
            Array.Reverse(current);
            int currentInt = BitConverter.ToInt16(current, 0) * 10;
            float currnetDouble = (float)currentInt / 1000.0f;
            float currentfloat = (data[13] & 0xFF) << 8;
            currentfloat += (data[14] & 0xFF);
            currentfloat = currentfloat / 100.0f;
            if (currentfloat > 1)
                currentfloat = -1;
            int year = data[51];
            int month = data[52];
            int day = data[53];
            int version = data[54];
            string micom = $"{year}.{month}.{day} ver {version}";
            //string micom = $"{year}.{month}.{day}. ver {version}";
            byte error0 = data[49];
            byte error1 = data[50];
            int[] binary0 = Enumerable.Range(1, 8).Select(i => error0 / (1 << (8 - i)) % 2).ToArray();
            int[] binary1 = Enumerable.Range(1, 8).Select(i => error1 / (1 << (8 - i)) % 2).ToArray();
            //Array.Reverse(binary0);
            //Array.Reverse(binary1);
            string errorStr = GetErrorName(binary0, binary1);
            channel.StateContent.Content = errorStr;
            //5 = 0101 RUN CCW
            //9 = 1001 RUN STOP
            //3 = 0011 RUN CW
            //8 = 1000 STOP 
            //2 = 0010 CW
            //4 = 0100 CCW
            bool[] errors0 = new bool[8];
            bool[] errors1 = new bool[8];
            for (int i = 0; i < 8; i++)
            {
                if (binary0[i] == 0)
                {
                    errors0[i] = true;
                }
                else
                {
                    errors0[i] = false;
                }
                if (binary1[i] == 0)
                {
                    errors1[i] = true;
                }
                else
                {
                    errors1[i] = false;
                }
            }
            if (errors1[6])
            {
                channel.Item16.cont.Content = "정상";
            }
            else
            {
                channel.Item16.cont.Content = "감지";
            }
            int motorRunTime = data[12];
            int cw_on = data[22];
            int cw_off = data[23];
            int ccw_on = data[24];
            int ccw_off = data[25];
            int foreign_time = data[31];
            int foreign_current = data[32];
            int foreign_count = data[33];
            int motor_over_current = data[34];
            int motor_over_count = data[35];
            int ventile_temp = data[36];
            int heater_setting_temp = data[26];
            int heater_setting_offtime = data[27];
            int fan_duty = data[29];
            int hot_air_fan_duty = data[30];
            float heateroffTime = (float)(heateroff / 10.0f);
            float averOffTime = (float)(airheatertemp / 10.0f);
            DateTime now = DateTime.Now;

            ReadData read = new ReadData()
            {
                date = DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss"),
                mode = mode + 1,
                remain_time = time,
                heater_temp = heatertemp,
                heater_off_time = heateroff,
                air_temp = airtemp,
                fan_speed = fan_duty,
                hot_air_temp = airheatertemp,
                hot_air_ontime = heaterduty,
                motor = getMotorState(motorRun),
                motor_current = currentfloat / 2.0
            };

            channel.WriteFile(read);

            DetailView.operation.Value.Content = runTime;

            DetailView.motor.Value11.Content = cw_on; //motor cw on
            DetailView.motor.Value12.Content = ccw_on; //motor cw off
            DetailView.motor.Value21.Content = cw_off; //motor ccw on
            DetailView.motor.Value22.Content = ccw_off; //motor ccw off

            DetailView.material.Value.Content = foreign_time; //foreign time
            DetailView.overflow.Value11.Content = string.Format("{0: 0.0#A}", (float)(foreign_current / 10.0f)); //overflow current1
            DetailView.overflow.Value12.Content = foreign_count; //overflow count1
            DetailView.overflow.Value21.Content = string.Format("{0: 0.0#A}", (float)(motor_over_current / 10.0f)); //overflow current2
            DetailView.overflow.Value22.Content = motor_over_count; //overflow count2

            DetailView.temper.Value1.Content = heater_setting_temp;
            DetailView.temper.Value2.Content = string.Format("{0: 0.0#}", (float)(heater_setting_offtime / 10.0f));
            DetailView.temper.Value3.Content = ventile_temp;
            DetailView.temper.Value4.Content = fan_duty;

            DetailView.heater.Value1.Content = airheatertemp;
            DetailView.heater.Value2.Content = heaterduty;

            
            channel.VersionBox.cont.Content = getModelName(data[48]);
            channel.CompileBox.cont.Content = micom;
            channel.Item1.cont.Content = heatertemp + "ºC";
            channel.Item2.cont.Content = airtemp + "ºC";
            if (channel.IsNewVersion)
            {
                channel.Item3.cont.Content = averOffTime + "ms";
            }
            else
            {
                channel.Item3.cont.Content = airheatertemp + "ºC";
            }
            channel.Item5.cont.Content = fan_duty + "%";
            channel.Item15.cont.Content = hot_air_fan_duty + "%";
            channel.Item11.cont.Content = heateroffTime + "ms";
            channel.Item12.cont.Content = airaverage + "ºC";
            channel.Item13.cont.Content = heaterduty;
            channel.Item14.cont.Content = string.Format("{0:0.00A}", currentfloat / 2.0);
            channel.Item6.cont.Content = t_time;
            channel.Item4.Title = getMotorState(motorRun);
            channel.Item4.cont.Content = motorRunTime.ToString() + "s";
            channel.ModeTimeView.label.Content = string.Format("모드{0}", mode + 1);
            channel.ModeTimeView.cont.Content = time;
            
            Console.WriteLine("NOW: " + DateTime.Now.ToFileTime());
            if (channel.Item1Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[10], new DataPoint(total_minute, heatertemp));
                //channel.list1.Add(new KeyValuePair<double, int>(total_minute, heatertemp));
            }
            if (channel.Item2Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[11], new DataPoint(total_minute, airtemp));
                //channel.list2.Add(new KeyValuePair<double, int>(total_minute, airtemp));
            }
            if (channel.Item3Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[12], new DataPoint(total_minute, airheatertemp));
                //channel.list3.Add(new KeyValuePair<double, int>(total_minute, airheatertemp));
            }
            if (channel.Item4Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[13], new DataPoint(total_minute, getMotorValue(motorRun)));
                //channel.list4.Add(new KeyValuePair<double, int>(total_minute, getMotorValue(motorRun)));
            }
            if (channel.Item5Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[14], new DataPoint(total_minute, heateroff));
                //channel.list5.Add(new KeyValuePair<double, int>(total_minute, heateroff));
            }
            if (channel.Item6Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[15], new DataPoint(total_minute, airaverage));
                //channel.list6.Add(new KeyValuePair<double, int>(total_minute, airaverage));
            }
            if (channel.Item7Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[16], new DataPoint(total_minute, heaterduty));
                //channel.list7.Add(new KeyValuePair<double, int>(total_minute, heaterduty));
            }
            if (channel.Item8Check.IsChecked.Value)
            {
                channel.chartView.ViewModel.AddData(seriesList[17], new DataPoint(total_minute, currnetDouble / 2.0));
                //channel.list8.Add(new KeyValuePair<double, double>(total_minute, currnetDouble / 2.0));
            }

            log.Debug($"isDummy={IsDummyEnabled} total_second={total_second} total_minute={total_minute:F3} start={_dummyStartTime:HH:mm:ss.fff}");
            channel.chartView.ViewModel.panXAxis(total_minute);
        }

        public void OnStart()
        {
            if (receivedData == null)
                receivedData = new List<byte>();
            if (parameterReceived == null)
                parameterReceived = new List<byte>();
        }

        private string GetErrorName(int[] errors0, int[] errors1)
        {
            Array.Reverse(errors0);
            Array.Reverse(errors1);
            StringBuilder builder = new StringBuilder();
            int cnt = 0;
            if (errors0 != null && errors0.Length > 0)
                for (int i = 0; i < errors0.Length; i++)
                {
                    if (errors0[i] == 1)
                    {
                        cnt++;
                        switch (i)
                        {
                            case 0:
                                builder.AppendLine("모터 과부하", true);
                                break;
                            case 1:
                                builder.AppendLine("모터 단선", true);
                                break;
                            case 2:
                                builder.AppendLine("히터 동작 이상", true);
                                break;
                            case 3:
                                if (errors0[2] != 1)
                                    builder.AppendLine("히터 동작 이상", true);
                                else
                                    cnt--;
                                break;
                            case 4:
                                builder.AppendLine("히터 센서 이상", true);
                                break;
                            case 5:
                                builder.AppendLine("배기 온도 이상", true);
                                break;
                            case 6:
                                builder.AppendLine("배기 센서 이상", true);
                                break;
                            case 7:
                                builder.AppendLine("배기 팬 이상", true);
                                break;
                        }
                    }
                }
            if (errors1 != null && errors1.Length > 0)
                for (int i = 0; i < errors1.Length; i++)
                {
                    if (errors1[i] == 1)
                    {
                        cnt++;
                        switch (i)
                        {
                            case 0:
                                builder.AppendLine("이물질감지", true);
                                break;
                            case 1:
                                builder.AppendLine("도어 열림", true);
                                break;
                            case 2:
                                if (errors1[1] != 1)
                                    builder.AppendLine("도어 열림", true);
                                else
                                    cnt--;
                                break;
                            case 3:
                                builder.AppendLine("열풍 팬 에러", true);
                                break;
                            case 4:
                                builder.AppendLine("열풍 히터 과열", true);
                                break;
                            case 5:
                                builder.AppendLine("열풍 히터 오픈", true);
                                break;
                            case 6:
                                builder.AppendLine("만수, 워터센서 오픈", true);
                                break;
                            case 7:
                                builder.AppendLine("열풍 히터 저온", true);
                                break;
                        }
                    }
                }
            return builder.ToString();
        }

        private string getModelName(int model)
        {
            switch (model)
            {
                case 0:
                    return "PCS400";
                case 1:
                    return "PCS500";
                case 2:
                    return "PCS350";
                case 3:
                    return "PCS400_T";
                case 4:
                    return "PCS400P";
                case 5:
                    return "PCS400P_T";
                case 6:
                    return "PCS500N";
                case 7:
                    return "PCS400A_T_E";
                case 8:
                    return "PCS400P_T_E";
                default:
                    return "";
            }
        }

        private int getMotorValue(int run)
        {
            Console.WriteLine("Value:{0}", run);
            switch (run)
            {
                case 2:
                    return 75;
                case 3:
                    return 75;
                case 4:
                    return 25;
                case 5:
                    return 25;
                case 8:
                    return 50;
                case 9:
                    return 50;
                default:
                    return 0;
            }
        }

        private string getMotorState(int run)
        {
            switch (run)
            {
                case 2:
                    return "CW";
                case 3:
                    return "CW";
                case 4:
                    return "CCW";
                case 5:
                    return "CCW";
                case 8:
                    return "STOP";
                case 9:
                    return "STOP";
                default:
                    return "";
            }
        }
    }
}
