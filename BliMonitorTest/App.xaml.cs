using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BliMonitorTest
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // log4net 설정 로드
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(baseDir, "Log4Net.config");
            XmlConfigurator.Configure(new FileInfo(configPath));

            // 60일 보존 정책 적용
            CleanupOldLogs(Path.Combine(baseDir, "logs"), TimeSpan.FromDays(3));

            LogManager.GetLogger(typeof(App)).Info("log4net 초기화 및 로그 정리 완료");
        }

        private static void CleanupOldLogs(string logsDir, TimeSpan keep)
        {
            try
            {
                if (!Directory.Exists(logsDir)) return;

                var cutoff = DateTime.Now - keep;
                var files = Directory.EnumerateFiles(logsDir, "*.log", SearchOption.TopDirectoryOnly);

                foreach (var f in files)
                {
                    var info = new FileInfo(f);
                    // 생성/수정 날짜 중 보존 기준으로 사용할 항목 선택
                    var referenceDate = info.LastWriteTime; // 또는 info.CreationTime
                    if (referenceDate < cutoff)
                    {
                        info.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                // 삭제 실패는 로깅만 하고 계속 진행
                LogManager.GetLogger(typeof(App)).Warn("오래된 로그 삭제 중 예외 발생", ex);
            }
        }
    }
}
