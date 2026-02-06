using BliMonitorTest.controls;
using BliMonitorTest.data;
using BliMonitorTest.util;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace BliMonitorTest
{
    partial class OneChannelWindow
    {
        private DateTime? _dummyStartTime;

              // ===== 차트 UI 튜닝용(기준선 + 이상치 표시) =====
        private ScatterSeries _anomalySeries;
        private LineAnnotation _warnHighLine;
        private LineAnnotation _warnLowLine;
        private double? _prevY = null;

              // 기본 옵션(원하면 UI에서 제어)
        private bool _enableWarnLine = true;
        private bool _enableAnomaly = true;
        private double _warnLow = double.NaN;
        private double _warnHigh = double.NaN;
        private double _spikeDelta = 15;
        private int _maxPoints = 360; // 10초*360 = 1시간

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
            if (data.Length < 4 || data.Length != data[3])
            {
                return;
            }

            bool IsDummyEnabled = (UseDummyCheck?.IsChecked == true);

            long total_second = (long)TestTime.TotalSeconds;
            double total_minute = total_second / 60.0;

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

                     // null일 때만 1회 설정 (핵심)
            if (_dummyStartTime == null)
                _dummyStartTime = DateTime.Now;

            TimeSpan elapsed = DateTime.Now - _dummyStartTime.Value;

            total_second = (long)elapsed.TotalSeconds;
            total_minute = elapsed.TotalMinutes;   // 분 단위 double (가장 깔끔)

                     // 데이터 화면 매핑
            byte modelCode = data[5];
            byte swVersion = data[6];

            int heaterTemp = data[7];
            int coldTemp = data[8];

            bool waterLevelLow = data[9] != 0;   // ON/OFF
            bool floorSensor = data[10] != 0;  // ON/OFF

            bool uvLed = data[11] != 0;

                     // 3방 SOL: data[12] 비트필드 (바이트의 0번째 비트부터)
            byte triSolByte = data[12];
            bool triSol1 = (triSolByte & (1 << 0)) != 0; // 0번째 비트 → 3방 SOL 1
            bool triSol2 = (triSolByte & (1 << 1)) != 0; // 1번째 비트 → 3방 SOL 2
            bool triSol3 = (triSolByte & (1 << 2)) != 0; // 2번째 비트 → 3방 SOL 3

            bool airVentSol = data[13] != 0;
            bool cvSol = data[14] != 0;

                     // 버튼 2바이트 (LSB→MSB, bit0부터)
            ushort buttons = (ushort)(data[15] | (data[16] << 8));
            bool btnCont = (buttons & (1 << 0)) != 0;
            bool btnVolume = (buttons & (1 << 1)) != 0;
            bool btnFree = (buttons & (1 << 2)) != 0;
            bool btnHighHot = (buttons & (1 << 3)) != 0;
            bool btnHot = (buttons & (1 << 4)) != 0;
            bool btnWarm = (buttons & (1 << 5)) != 0;
            bool btnChild = (buttons & (1 << 6)) != 0;
            bool btnRoom = (buttons & (1 << 7)) != 0;
            bool btnMildCold = (buttons & (1 << 8)) != 0;
            bool btnCold = (buttons & (1 << 9)) != 0;

            bool pumpOn = data[17] != 0;
            bool coldSol = data[18] != 0;
            bool normalSol = data[19] != 0;
            bool hotSol1 = data[20] != 0;

            byte needleState = data[21];
            byte compVolt = data[22];

                     // 4) 중앙 좌측 UI 바인딩
            channel.Item1.cont.Content = $"{heaterTemp}ºC";                 // 히터 온도
            channel.Item2.cont.Content = $"{coldTemp}ºC";                   // 냉수 온도
            channel.Item3.cont.Content = waterLevelLow ? "ON" : "OFF";      // 수위센서
            channel.Item4.cont.Content = floorSensor ? "ON" : "OFF";        // 플로어 센서
            channel.Item5.cont.Content = uvLed ? "ON" : "OFF";              // UV LED

                     // 3방 SOL 요약(0번째 비트부터: 1,2,3)
            channel.Item6.cont.Content = $"{OnOff(triSol1)} / {OnOff(triSol2)} / {OnOff(triSol3)}";

            channel.Item11.cont.Content = airVentSol ? "ON" : "OFF";        // Air Vent Sol
            channel.Item12.cont.Content = cvSol ? "ON" : "OFF";             // C/V
            channel.Item13.cont.Content = pumpOn ? "ON" : "OFF";            // PUMP
            channel.Item14.cont.Content = coldSol ? "ON" : "OFF";           // Cold Sol
            channel.Item15.cont.Content = normalSol ? "ON" : "OFF";         // Normal Sol
            channel.Item16.cont.Content = hotSol1 ? "ON" : "OFF";           // Hot Sol

            channel.Item17.cont.Content = $"{NeedleToText(needleState)}";
            channel.Item18.cont.Content = getModelName(modelCode);

                     // 운전/대기 표시(좌측 상단 박스와 연동)
            channel.run = pumpOn;

                     // 5) 중앙 우측 버튼 패널 업데이트
            DetailView.UpdateButtons(btnCont, btnVolume, btnFree, btnHighHot, btnHot, btnWarm, btnChild, btnRoom, btnMildCold, btnCold);

                     // 5-1) 수신 데이터 파일 기록
            var resp = ResponsePacket57.Parse(data);
            if (resp != null)
            {
                channel.WriteFile(resp, channelNo: 1, sourceType: BliMonitorTest.util.MonitoringDb.MonitoringDb.SOURCE_SINGLE);
            }

            // 6) 차트 AddData – 고정 키(10~17) 사용 : 10:히터(ºC), 11:냉수(ºC), 12:수위센서(0/1), 13:플로어(0/1), 14:AirVent(0/1), 15:C/V(0/1), 16:PUMP(0/1), 17:ColdSol(0/1)
            if (channel.Item1Check.IsChecked == true && seriesList.ContainsKey(10))
            {
                channel.chartView.ViewModel.AddData(seriesList[10], new DataPoint(total_minute, heaterTemp));
            }
            if (channel.Item2Check.IsChecked == true && seriesList.ContainsKey(11))
            {
                channel.chartView.ViewModel.AddData(seriesList[11], new DataPoint(total_minute, coldTemp));
            }
            if (channel.Item3Check.IsChecked == true && seriesList.ContainsKey(12))
            {
                channel.chartView.ViewModel.AddData(seriesList[12], new DataPoint(total_minute, waterLevelLow ? 1 : 0));
            }
            if (channel.Item4Check.IsChecked == true && seriesList.ContainsKey(13))
            {
                channel.chartView.ViewModel.AddData(seriesList[13], new DataPoint(total_minute, floorSensor ? 1 : 0));
            }
            if (channel.Item5Check.IsChecked == true && seriesList.ContainsKey(14))
            {
                channel.chartView.ViewModel.AddData(seriesList[14], new DataPoint(total_minute, airVentSol ? 1 : 0));
            }
            if (channel.Item6Check.IsChecked == true && seriesList.ContainsKey(15))
            {
                channel.chartView.ViewModel.AddData(seriesList[15], new DataPoint(total_minute, cvSol ? 1 : 0));
            }
            if (channel.Item7Check.IsChecked == true && seriesList.ContainsKey(16))
            {
                channel.chartView.ViewModel.AddData(seriesList[16], new DataPoint(total_minute, pumpOn ? 1 : 0));
            }
            if (channel.Item8Check.IsChecked == true && seriesList.ContainsKey(17))
            {
                channel.chartView.ViewModel.AddData(seriesList[17], new DataPoint(total_minute, coldSol ? 1 : 0));
            }

            log.Debug($"isDummy={IsDummyEnabled} total_second={total_second} total_minute={total_minute:F3} start={_dummyStartTime:HH:mm:ss.fff}");
            channel.chartView.ViewModel.panXAxis(total_minute);
        }

        private string OnOff(bool v) => v ? "ON" : "OFF";

        private string NeedleToText(byte st)
        {
            switch (st)
            {
                case 0: return "상승상태";
                case 1: return "동작중";
                case 2: return "하강상태";
                default: return st.ToString();
            }
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
                    return "BSH-311";
                case 1:
                    return "BSS-311";
                case 2:
                    return "BSS-314";
                case 3:
                    return "BSS-310";
                case 4:
                    return "BSS-330";
                case 5:
                    return "BSS-341";
                case 6:
                    return "DUO 8";
                case 7:
                    return "Hybrid";
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

        private PlotModel GetPlotModelOrNull()
        {
            // 단일채널에서 Chart(또는 chartView)에 접근하는 실제 객체명을 여기에 맞춰주세요.
            // 예: Chart가 PlotView면 Chart.Model
            return channel.chartView.ViewModel.PlotModel;
            //return Chart?.Model;
        }

        private void EnsureChartEnhancements()
        {
            var model = GetPlotModelOrNull();
            if (model == null) return;

            if (_anomalySeries == null)
            {
                _anomalySeries = new ScatterSeries
                {
                    Title = "이상치",
                    MarkerType = MarkerType.Circle,
                    MarkerFill = OxyColors.Red,
                    MarkerStroke = OxyColors.Transparent,
                    MarkerSize = 3.5
                };
                model.Series.Add(_anomalySeries);
            }

            if (_warnHighLine == null)
            {
                _warnHighLine = new LineAnnotation
                {
                    Type = LineAnnotationType.Horizontal,
                    Color = OxyColors.OrangeRed,
                    LineStyle = LineStyle.Dash,
                    Text = "상한",
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                    StrokeThickness = 1
                };
                model.Annotations.Add(_warnHighLine);
            }

            if (_warnLowLine == null)
            {
                _warnLowLine = new LineAnnotation
                {
                    Type = LineAnnotationType.Horizontal,
                    Color = OxyColors.OrangeRed,
                    LineStyle = LineStyle.Dash,
                    Text = "하한",
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                    StrokeThickness = 1
                };
                model.Annotations.Add(_warnLowLine);
            }

            ApplyWarnLinesVisibility();
        }

        private void ApplyWarnLinesVisibility()
        {
            if (_warnHighLine == null || _warnLowLine == null) return;

            if (!_enableWarnLine || double.IsNaN(_warnHigh))
            {
                _warnHighLine.Color = OxyColors.Transparent;
                _warnHighLine.Text = "";
            }
            else
            {
                _warnHighLine.Color = OxyColors.OrangeRed;
                _warnHighLine.Y = _warnHigh;
                _warnHighLine.Text = $"상한 {_warnHigh}";
            }

            if (!_enableWarnLine || double.IsNaN(_warnLow))
            {
                _warnLowLine.Color = OxyColors.Transparent;
                _warnLowLine.Text = "";
            }
            else
            {
                _warnLowLine.Color = OxyColors.OrangeRed;
                _warnLowLine.Y = _warnLow;
                _warnLowLine.Text = $"하한 {_warnLow}";
            }
        }

        private void AddAnomalyPointIfNeeded(double x, double y)
        {
            if (!_enableAnomaly) return;
            if (_anomalySeries == null) return;

            bool outOfRange =
                (!double.IsNaN(_warnHigh) && y > _warnHigh) ||
                (!double.IsNaN(_warnLow) && y < _warnLow);

            bool spike = false;
            if (_prevY.HasValue)
                spike = Math.Abs(y - _prevY.Value) >= _spikeDelta;

            if (outOfRange || spike)
                _anomalySeries.Points.Add(new ScatterPoint(x, y));

            _prevY = y;
        }

        private void TrimIfNeeded(LineSeries line)
        {
            if (line == null) return;
            if (_maxPoints <= 0) return;

            while (line.Points.Count > _maxPoints)
                line.Points.RemoveAt(0);

            if (_anomalySeries != null)
            {
                while (_anomalySeries.Points.Count > _maxPoints)
                    _anomalySeries.Points.RemoveAt(0);
            }
        }

        private LineSeries GetFirstLineSeriesOrNull()
        {
            var model = GetPlotModelOrNull();
            if (model == null) return null;
            return model.Series.OfType<LineSeries>().FirstOrDefault();
        }

        public void ClearChartDataAndResetTime()
        {
            _dummyStartTime = null;
            _prevY = null;

            if (_anomalySeries != null)
            {
                if (_anomalySeries.Points != null)
                    _anomalySeries.Points.Clear();
            }

            // 자기 차트뷰의 PlotModel/FullscreenPlotModel도 방어적으로 초기화
            BliMonitorTest.data.MainViewModel vm = channel.chartView != null ? channel.chartView.ViewModel : null;
            if (vm != null)
            {
                OxyPlot.PlotModel model = vm.PlotModel;
                if (model != null)
                {
                    if (model.Series != null)
                    {
                        for (int i = 0; i < model.Series.Count; i++)
                        {
                            OxyPlot.Series.Series s = model.Series[i];

                            OxyPlot.Series.LineSeries ls = s as OxyPlot.Series.LineSeries;
                            if (ls != null)
                            {
                                if (ls.Points != null) ls.Points.Clear();
                                continue;
                            }

                            OxyPlot.Series.ScatterSeries ss = s as OxyPlot.Series.ScatterSeries;
                            if (ss != null)
                            {
                                if (ss.Points != null) ss.Points.Clear();
                                continue;
                            }

                            OxyPlot.Series.AreaSeries ars = s as OxyPlot.Series.AreaSeries;
                            if (ars != null)
                            {
                                if (ars.Points != null) ars.Points.Clear();
                                if (ars.Points2 != null) ars.Points2.Clear();
                                continue;
                            }

                            OxyPlot.Series.StemSeries sts = s as OxyPlot.Series.StemSeries;
                            if (sts != null)
                            {
                                if (sts.Points != null) sts.Points.Clear();
                                continue;
                            }
                        }
                    }
                    model.ResetAllAxes();
                    model.InvalidatePlot(true);
                }

                if (vm.FullscreenPlotModel != null)
                {
                    OxyPlot.PlotModel f = vm.FullscreenPlotModel;
                    if (f.Series != null)
                    {
                        for (int i = 0; i < f.Series.Count; i++)
                        {
                            OxyPlot.Series.Series s = f.Series[i];

                            OxyPlot.Series.LineSeries ls = s as OxyPlot.Series.LineSeries;
                            if (ls != null)
                            {
                                if (ls.Points != null) ls.Points.Clear();
                                continue;
                            }

                            OxyPlot.Series.ScatterSeries ss = s as OxyPlot.Series.ScatterSeries;
                            if (ss != null)
                            {
                                if (ss.Points != null) ss.Points.Clear();
                                continue;
                            }
                        }
                    }
                    f.ResetAllAxes();
                    f.InvalidatePlot(true);
                }
            }
        }


    }
}
