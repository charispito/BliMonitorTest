using System;
using System.IO;

namespace BliMonitorTest.util.StoragePathUtil
{
    internal static class StoragePathUtil
    {
        public static string GetExeDirectory()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                dir = Environment.CurrentDirectory;
            }
            return dir;
        }

        /// <summary>
        /// DB 경로: exe 폴더 + Monitoring.db (고정)
        /// </summary>
        public static string GetDbPath()
        {
            return Path.Combine(GetExeDirectory(), "Monitoring.db");
        }

        /// <summary>
        /// CSV 저장 폴더:
        /// - saveInDesktop=true -> 바탕화면/ChannelData
        /// - false -> exe폴더/ChannelData
        /// </summary>
        public static string GetCsvDirectory(bool saveInDesktop)
        {
            if (saveInDesktop)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                return Path.Combine(desktop, "ChannelData");
            }

            return Path.Combine(GetExeDirectory(), "ChannelData");
        }

        /// <summary>
        /// 시간 단위 CSV 파일 경로 예) Monitoring_20260112_16.csv
        /// prefix는 단일/다채널 공통 파일이면 "Monitoring" 같이 고정 추천.
        /// </summary>
        public static string GetHourlyCsvPath(bool saveInDesktop, string prefix, DateTime timestamp)
        {
            string dir = GetCsvDirectory(saveInDesktop);
            EnsureDirectory(dir);

            string safePrefix = prefix;
            if (string.IsNullOrEmpty(safePrefix))
            {
                safePrefix = "Monitoring";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safePrefix = safePrefix.Replace(c.ToString(), "_");
            }

            string fileName = safePrefix + "_" + timestamp.ToString("yyyyMMdd_HH") + ".csv";
            return Path.Combine(dir, fileName);
        }

        public static void EnsureDirectory(string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public static void EnsureDirectoryForFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            EnsureDirectory(dir);
        }
    }
}
