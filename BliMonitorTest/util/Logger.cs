using System;
using System.IO;
using log4net;
using log4net.Config;

namespace Util
{
    public static class Logger
    {
        private static bool _initialized;
        private static readonly object _lock = new object();

        // 클래스별 로거 획득
        public static ILog GetLogger(Type type)
        {
            EnsureInitialized();
            return LogManager.GetLogger(type);
        }

        // 자주 쓰는 래퍼 (원하면 제거 가능)
        public static void Debug(Type type, string message) => GetLogger(type).Debug(message);
        public static void Info(Type type, string message) => GetLogger(type).Info(message);
        public static void Warn(Type type, string message) => GetLogger(type).Warn(message);
        public static void Error(Type type, string message) => GetLogger(type).Error(message);
        public static void Error(Type type, string message, Exception ex) => GetLogger(type).Error(message, ex);

        // 최초 1회 설정 로드
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;

                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log4Net.config");
                var fi = new FileInfo(configPath);
                if (!fi.Exists)
                {
                    // 기본 경로에 없으면 프로젝트 루트 등 다른 위치에서도 탐색 가능
                    // 필요하면 예외를 던지거나, 임시 기본 설정을 적용하는 로직을 추가
                }
                XmlConfigurator.Configure(fi);
                _initialized = true;
            }
        }
    }
}
