using BliMonitorTest.util;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public class AppConfiguration
    {
        public static string GetAppConfig(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public static string GetAppConfig(string key, string def)
        {
            if (ConfigurationManager.AppSettings[key].IsEmpty())
            {
                return def;
            }
            else
            {
                return ConfigurationManager.AppSettings[key];
            }
        }

        public static void SetAppConfig(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection cfgCollection = config.AppSettings.Settings;

            cfgCollection.Remove(key);
            cfgCollection.Add(key, value);

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
        }

        public static void AddAppConfig(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection cfgCollection = config.AppSettings.Settings;

            cfgCollection.Add(key, value);

            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
        }

        public static void RemoveAppConfig(string key)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection cfgCollection = config.AppSettings.Settings;

            try
            {
                cfgCollection.Remove(key);

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(config.AppSettings.SectionInformation.Name);
            }
            catch { }
        }
    }
}
