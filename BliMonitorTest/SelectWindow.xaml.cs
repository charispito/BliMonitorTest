using log4net;
using System;
using System.Windows;
using BliMonitorTest.Bootstrap;

namespace BliMonitorTest
{
    /// <summary>
    /// SelectWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SelectWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SelectWindow));
        private bool _dbInitialized = false;

        public SelectWindow()
        {
            InitializeComponent();
            OneChannelButton.Click += OneChannelButton_Click;
            MultiChannelButton.Click += MultiChannelButton_Click;

            // 윈도우 로드 후 DB 초기화
            this.Loaded += SelectWindow_Loaded;
        }

        private async void SelectWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // UI 멈춤 방지용 최소 비동기 양보
                await System.Threading.Tasks.Task.Yield();

                AppBootstrap.InitDatabaseAtStartup(log);
                _dbInitialized = true;
            }
            catch (Exception ex)
            {
                log.Error("초기 DB 준비 실패", ex);
                ToastMessage.ToastService.AppToast.Show("초기 DB 준비에 실패했습니다.");
                Application.Current.Shutdown();
            }
        }

        private void OneChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_dbInitialized)
            {
                ToastMessage.ToastService.AppToast.Show("DB 초기화 중입니다. 잠시만 기다려 주세요.");
                return;
            }

            new OneChannelWindow().Show();
            this.Close();
        }

        private void MultiChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_dbInitialized)
            {
                ToastMessage.ToastService.AppToast.Show("DB 초기화 중입니다. 잠시만 기다려 주세요.");
                return;
            }

            new MultiWindow1().Show();
            this.Close();
        }
    }
}
