using System;
using System.Windows;
using OxyPlot;

namespace BliMonitorTest
{
    public sealed class PlotSyncHub
    {
        private readonly object _lock = new object();

        public PlotModel MainModel { get; private set; }
        public PlotModel FullscreenModel { get; private set; }

        public PlotSyncHub(Func<PlotModel> createModel)
        {
            MainModel = createModel();
            FullscreenModel = createModel();
        }

        public void UpdateBoth(Action<PlotModel> update)
        {
            lock (_lock)
            {
                // UI 스레드에서 PlotModel/Series 변경 + Invalidate
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    update(MainModel);
                    update(FullscreenModel);

                    MainModel.InvalidatePlot(true);
                    FullscreenModel.InvalidatePlot(true);
                }));
            }
        }
    }
}
