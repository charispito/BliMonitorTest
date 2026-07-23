using BliMonitorTest.data;
using BliMonitorTest.util;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BliMonitorTest
{
    partial class OneChannelWindow
    {
        private DateTime? _dummyStartTime;

        public string GetDateTime()
        {
            DateTime nowDate = DateTime.Now;
            return nowDate.ToString("yyyy-MM-dd HH:mm:ss") + ":" + nowDate.Millisecond.ToString("000");
        }

        private void CheckCommand(byte[] array)
        {
            System.Diagnostics.Debug.WriteLine($"CheckCommand len={array?.Length}, cmd=0x{array?[2]:X2}");

            if (array == null || array.Length < 7)
                return;

            array.PrintHex(1);

            try
            {
                switch (array[2])
                {
                    case 0xA0:
                        if (array.Length != 37)
                            return;

                        var pkt = Duo8PacketParser.ParseStatus(array);
                        if (pkt == null)
                            return;

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MarkValidStatusResponseReceived();
                            setView(array);
                        }));
                        break;

                    case 0xB9:
                        if (array.Length != 134)
                            return;

                        if (channel.ParameterMode && channel.parameterWindow != null)
                        {
                            channel.parameterWindow.setError(array);
                        }
                        break;

                    case 0xC1:
                        System.Diagnostics.Debug.WriteLine($"[C1] RX frame complete len={array.Length}, ParameterMode={channel.ParameterMode}, parameterWindowNull={channel.parameterWindow == null}");

                        if (array.Length != 76)
                            return;

                        if (channel.ParameterMode && channel.parameterWindow != null)
                        {
                            System.Diagnostics.Debug.WriteLine("[C1] calling setParameterResponse()");
                            channel.parameterWindow.setParameterResponse(array);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[C1] dropped - no active parameter window");
                        }
                        break;

                    case 0xC2:
                        if (array.Length != 8)
                            return;

                        if (channel.ParameterMode && channel.parameterWindow != null)
                        {
                            channel.parameterWindow.setParameterWriteAck(array);
                        }
                        break;

                    case 0xB6:
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void MarkValidStatusResponseReceived()
        {
            _lastResponseAt = DateTime.Now;
            _hasValidStatusResponse = true;

            if (_isConnecting)
            {
                _isConnecting = false;
                _waitingFirstResponse = false;
                StopConnectionWatchdog();

                channel.ConnectState = 1;
                ConnectButton.Content = "해제";

                StartTimerSafe();

                log.Info("정상 STATUS 응답 수신 - 연결 성공 처리");
                return;
            }

            _waitingFirstResponse = false;
            StopConnectionWatchdog();
        }

        private void setView(byte[] data)
        {
            if (data == null || data.Length != 37)
                return;

            Duo8StatusPacket pkt = Duo8PacketParser.ParseStatus(data);
            if (pkt == null)
                return;

            try
            {
                if (channel != null && channel.SaveInDesktop)
                {
                    int sourceType = UseDummy ? 0 : 1;
                    channel.WriteFile(pkt, 1, sourceType);
                }
            }
            catch (Exception ex)
            {
                log.Warn("상태 수신 저장 실패", ex);
            }

            if (_dummyStartTime == null)
                _dummyStartTime = DateTime.Now;

            double totalMinute = UseDummy
                ? (DateTime.Now - _dummyStartTime.Value).TotalMinutes
                : TestTime.TotalMinutes;

            string modelName = Duo8ValueText.GetModelName(pkt.ModelCode);
            string errorText = Duo8ValueText.GetErrorText(pkt.ErrorCode);
            string modeText = Duo8ValueText.GetModeText(pkt.ModeSelected);
            string qtyText = Duo8ValueText.GetQtyText(pkt.QtySelected);
            string phaseText = Duo8ValueText.GetDispensePhaseText(pkt.DispensePhase);
            string subPhaseText = Duo8ValueText.GetDispenseSubPhaseText(pkt.DispenseSubPhase);

            ushort buttonInfo = pkt.ButtonInfo;
            byte statusA = pkt.StatusA;
            byte statusB = pkt.StatusB;

            channel.Item1.cont.Text = pkt.HotTempRaw.ToString();
            channel.Item2.cont.Text = pkt.ColdTempRaw.ToString();
            channel.Item3.cont.Text = Duo8ValueText.ToYesNo(pkt.WaterInitDone);
            channel.Item4.cont.Text = Duo8ValueText.ToYesNo(pkt.WaterInitGo);
            channel.Item5.cont.Text = Duo8ValueText.ToYesNo(pkt.EmptyDetect);
            channel.Item6.cont.Text = Duo8ValueText.ToYesNo(pkt.BufferLow);

            // 변경됨: byte10/11 = PCB HW/SW Version
            channel.Item7.cont.Text = pkt.PcbHwVersion.ToString();
            channel.Item8.cont.Text = pkt.PcbSwVersion.ToString();

            channel.Item9.cont.Text = pkt.HeaterPwm.ToString();
            channel.Item10.cont.Text = Duo8ValueText.ToOnOff(pkt.Night);
            channel.Item17.cont.Text = Duo8ValueText.ToYesNo(pkt.FloatLowStable);
            channel.Item18.cont.Text = Duo8ValueText.ToYesNo(pkt.BallTopFullStable);
            channel.Item19.cont.Text = Duo8ValueText.ToYesNo(pkt.WaterBufFullStable);

            channel.Item11.cont.Text = Duo8ValueText.ToOnOff(pkt.TestMode);
            channel.Item12.cont.Text = modelName;
            channel.Item13.cont.Text = $"0x{pkt.ErrorCode:X2} / {errorText}";
            channel.Item14.cont.Text = modeText + " / " + qtyText;
            channel.Item15.cont.Text = phaseText;
            channel.Item16.cont.Text = subPhaseText;
            channel.Item20.cont.Text = Duo8ValueText.ToOnOff(pkt.HeaterOutput);
            channel.Item21.cont.Text = Duo8ValueText.ToOnOff(pkt.CompressorOutput);
            channel.Item22.cont.Text = Duo8ValueText.ToOnOff(pkt.HotValveOutput);
            channel.Item23.cont.Text = Duo8ValueText.ToOnOff(pkt.ColdSelectOutput);
            channel.Item24.cont.Text = Duo8ValueText.ToOnOff(pkt.OutletValveOutput);
            channel.Item25.cont.Text = $"0x{pkt.ButtonInfo:X4}";
            channel.Item26.cont.Text = $"A:0x{pkt.StatusA:X2} / B:0x{pkt.StatusB:X2}";

            channel.Item40.cont.Text = Duo8ValueText.GetBitState((statusA & 0x01) != 0);
            channel.Item41.cont.Text = Duo8ValueText.GetBitState((statusA & 0x02) != 0);
            channel.Item42.cont.Text = Duo8ValueText.GetBitState((statusA & 0x04) != 0);
            channel.Item43.cont.Text = Duo8ValueText.GetBitState((statusA & 0x08) != 0);
            channel.Item44.cont.Text = Duo8ValueText.GetBitState((statusA & 0x10) != 0);
            channel.Item45.cont.Text = Duo8ValueText.GetBitState((statusA & 0x20) != 0);
            channel.Item46.cont.Text = Duo8ValueText.GetBitState((statusA & 0x40) != 0);
            channel.Item47.cont.Text = Duo8ValueText.GetBitState((statusA & 0x80) != 0);

            channel.Item48.cont.Text = Duo8ValueText.GetBitState((statusB & 0x01) != 0);
            channel.Item49.cont.Text = Duo8ValueText.GetBitState((statusB & 0x02) != 0);
            channel.Item50.cont.Text = Duo8ValueText.GetBitState((statusB & 0x04) != 0);
            channel.Item51.cont.Text = Duo8ValueText.GetBitState((statusB & 0x08) != 0);
            channel.Item52.cont.Text = Duo8ValueText.GetBitState((statusB & 0x10) != 0);
            channel.Item53.cont.Text = Duo8ValueText.GetBitState((statusB & 0x20) != 0); // Reheat
            channel.Item54.cont.Text = Duo8ValueText.GetBitState((statusB & 0x40) != 0); // HotIng
            channel.Item55.cont.Text = Duo8ValueText.GetBitState((statusB & 0x80) != 0);

            channel.Statebox.cont.Text = errorText == "NONE"
                ? phaseText + (subPhaseText == "IDLE" ? "" : " / " + subPhaseText)
                : errorText;

            channel.run = pkt.OutletValveOutput != 0;

            bool btnHot = (buttonInfo & 0x0001) != 0;
            bool btnWarm = (buttonInfo & 0x0002) != 0;
            bool btnNormal = (buttonInfo & 0x0004) != 0;
            bool btnCool = (buttonInfo & 0x0008) != 0;
            bool btnCold = (buttonInfo & 0x0010) != 0;
            bool btnReheat = (buttonInfo & 0x0020) != 0;
            bool btn150 = (buttonInfo & 0x0040) != 0;
            bool btn1000 = (buttonInfo & 0x0080) != 0;
            bool btnOutlet = (buttonInfo & 0x0100) != 0;

            DetailView.UpdateButtons(
                btnHot, btnWarm, btnNormal, btnCool, btnCold,
                btnReheat, btn150, btn1000, btnOutlet,
                pkt.TestMode != 0, pkt.Night != 0, pkt.HeaterPwm != 0
            );

            if (channel.Item1Check.IsChecked == true && seriesList.ContainsKey(10))
                channel.chartView.ViewModel.AddData(seriesList[10], new DataPoint(totalMinute, pkt.HotTempRaw));

            if (channel.Item2Check.IsChecked == true && seriesList.ContainsKey(11))
                channel.chartView.ViewModel.AddData(seriesList[11], new DataPoint(totalMinute, pkt.ColdTempRaw));

            if (channel.Item3Check.IsChecked == true && seriesList.ContainsKey(12))
                channel.chartView.ViewModel.AddData(seriesList[12], new DataPoint(totalMinute, pkt.WaterInitDone != 0 ? 1 : 0));

            if (channel.Item4Check.IsChecked == true && seriesList.ContainsKey(13))
                channel.chartView.ViewModel.AddData(seriesList[13], new DataPoint(totalMinute, pkt.EmptyDetect != 0 ? 1 : 0));

            if (channel.Item5Check.IsChecked == true && seriesList.ContainsKey(14))
                channel.chartView.ViewModel.AddData(seriesList[14], new DataPoint(totalMinute, pkt.HeaterOutput != 0 ? 1 : 0));

            if (channel.Item6Check.IsChecked == true && seriesList.ContainsKey(15))
                channel.chartView.ViewModel.AddData(seriesList[15], new DataPoint(totalMinute, pkt.CompressorOutput != 0 ? 1 : 0));

            if (channel.Item7Check.IsChecked == true && seriesList.ContainsKey(16))
                channel.chartView.ViewModel.AddData(seriesList[16], new DataPoint(totalMinute, pkt.HotValveOutput != 0 ? 1 : 0));

            if (channel.Item8Check.IsChecked == true && seriesList.ContainsKey(17))
                channel.chartView.ViewModel.AddData(seriesList[17], new DataPoint(totalMinute, pkt.OutletValveOutput != 0 ? 1 : 0));

            channel.chartView.ViewModel.panXAxis(totalMinute);
        }

        public void OnStart()
        {
            if (receivedData == null)
                receivedData = new List<byte>();
            if (parameterReceived == null)
                parameterReceived = new List<byte>();
        }

        public void ClearChartDataAndResetTime()
        {
            _dummyStartTime = null;

            var vm = channel.chartView != null ? channel.chartView.ViewModel : null;
            if (vm != null)
            {
                if (vm.PlotModel != null)
                {
                    foreach (var s in vm.PlotModel.Series.OfType<OxyPlot.Series.LineSeries>())
                        s.Points.Clear();

                    vm.PlotModel.ResetAllAxes();
                    vm.PlotModel.InvalidatePlot(true);
                }

                if (vm.FullscreenPlotModel != null)
                {
                    foreach (var s in vm.FullscreenPlotModel.Series.OfType<OxyPlot.Series.LineSeries>())
                        s.Points.Clear();

                    vm.FullscreenPlotModel.ResetAllAxes();
                    vm.FullscreenPlotModel.InvalidatePlot(true);
                }
            }
        }
    }
}
