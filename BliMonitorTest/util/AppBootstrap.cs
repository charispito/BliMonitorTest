// 파일: Bootstrap/AppBootstrap.cs
using BliMonitorTest.util.StoragePathUtil;
using log4net;
using Microsoft.Data.Sqlite;
using System.IO;

namespace BliMonitorTest.Bootstrap
{
    public static class AppBootstrap
    {
        public static void InitDatabaseAtStartup(ILog log = null, string overrideDbPath = null)
        {
            string dbPath = overrideDbPath ?? StoragePathUtil.GetDbPath();

            // 1) 디렉터리 보장
            string dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // 2) 스키마 보장 (DDL)
            SqliteConnection con = null;
            try
            {
                con = new SqliteConnection($"Data Source={dbPath};");
                con.Open();

                bool ready = false;
                // ref 인자 요구하는 기존 시그니처 대응
                BliMonitorTest.util.MonitoringDb.MonitoringDb.EnsureDb(ref con, dbPath, ref ready);
            }
            finally
            {
                // using 대신 명시적 Dispose
                con?.Dispose();
            }

            // 3) 쓰기 서비스 시작
            BliMonitorTest.util.MonitoringDb.MonitoringDbWriteService
                .Instance.Start(dbPath);

            log?.Info($"DB initialized and write service started at: {dbPath}");
        }
    }
}
