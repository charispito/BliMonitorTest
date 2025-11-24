using BliMonitorTest.setting;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public class ConfigFileManagement
    {
        public void InitPath()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting";
            bool exist = Directory.Exists(path);
            if (!exist)
            {
                Directory.CreateDirectory(path);
            }

        }
        public SettingItem ReadFromFile(string name)
        {
            SettingItem item = null;
            if (!name.Contains("config"))
            {
                name += ".config";
            }
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting";
            bool exist = Directory.Exists(path);
            if (!exist)
            {
                Directory.CreateDirectory(path);
                return null;
            }
            ExeConfigurationFileMap configMap = new ExeConfigurationFileMap();
            configMap.ExeConfigFilename = path + "\\" + name;
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            if (config == null)
                return null;
            SettingSection section = (SettingSection)config.GetSection("Mode1");
            List<SectionItem> mode1 = new List<SectionItem>();
            foreach(object key in section.Instance.baseGetAllKeys())
            {
                Setting element = section.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2};
                mode1.Add(sectionItem);
            }
            SettingSection section2 = (SettingSection)config.GetSection("Mode2");
            List<SectionItem> mode2 = new List<SectionItem>();
            foreach (object key in section2.Instance.baseGetAllKeys())
            {
                Setting element = section2.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                mode2.Add(sectionItem);
            }
            SettingSection section3 = (SettingSection)config.GetSection("Mode3");
            List<SectionItem> mode3 = new List<SectionItem>();
            foreach (object key in section3.Instance.baseGetAllKeys())
            {
                Setting element = section3.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                mode3.Add(sectionItem);
            }
            SettingSection section4 = (SettingSection)config.GetSection("Mode4");
            List<SectionItem> mode4 = new List<SectionItem>();
            foreach (object key in section4.Instance.baseGetAllKeys())
            {
                Setting element = section4.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                mode4.Add(sectionItem);
            }
            SettingSection section5 = (SettingSection)config.GetSection("Mode5");
            List<SectionItem> mode5 = new List<SectionItem>();
            foreach (object key in section5.Instance.baseGetAllKeys())
            {
                Setting element = section5.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                mode5.Add(sectionItem);
            }
            SettingSection section6 = (SettingSection)config.GetSection("Motor");
            List<SectionItem> motor = new List<SectionItem>();
            foreach (object key in section6.Instance.baseGetAllKeys())
            {
                Setting element = section6.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                motor.Add(sectionItem);
            }
            SettingSection section7 = (SettingSection)config.GetSection("Fan");
            List<SectionItem> fan = new List<SectionItem>();
            foreach (object key in section7.Instance.baseGetAllKeys())
            {
                Setting element = section7.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                fan.Add(sectionItem);
            }
            SettingSection section8 = (SettingSection)config.GetSection("Heater1");
            List<SectionItem> heater1 = new List<SectionItem>();
            foreach (object key in section8.Instance.baseGetAllKeys())
            {
                Setting element = section8.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                heater1.Add(sectionItem);
            }
            SettingSection section9 = (SettingSection)config.GetSection("Heater2");
            List<SectionItem> heater2 = new List<SectionItem>();
            foreach (object key in section9.Instance.baseGetAllKeys())
            {
                Setting element = section9.Instance.baseGet(key.ToString());
                SectionItem sectionItem = new SectionItem() { Index = element.Index, Name = element.Name, Value1 = element.Value1, Value2 = element.Value2 };
                heater2.Add(sectionItem);
            }

            item = new SettingItem() { mode1 = mode1, mode2 = mode2, mode3 = mode3, mode4 = mode4, mode5 = mode5, motor = motor, fan = fan, heater1 = heater1, heater2 = heater2 };
            return item;
        }

        public void CreateConfig(string name, SettingItem item)
        {
            string path = string.Format(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ParameterSetting\\{0}.config", name);
            ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = path
            };

            var config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
            config.Sections.Clear();
            config.AppSettings.Settings.Clear();
            int index = 1;
            SettingSection section1 = new SettingSection();
            SettingSection section2 = new SettingSection();
            SettingSection section3 = new SettingSection();
            SettingSection section4 = new SettingSection();
            SettingSection section5 = new SettingSection();
            SettingSection section6 = new SettingSection();
            SettingSection section7 = new SettingSection();
            SettingSection section8 = new SettingSection();
            SettingSection section9 = new SettingSection();

            foreach (SectionItem sectionItem in item.mode1)
            {                
                section1.Instance.baseAdd(new Setting() { Index = index.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2});
                index++;
            }
            config.Sections.Add("Mode1", section1);
            int index1 = 1;
            foreach (SectionItem sectionItem in item.mode2)
            {
                section2.Instance.baseAdd(new Setting() { Index = index1.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index1++;
            }
            config.Sections.Add("Mode2", section2);
            int index2 = 1;
            foreach (SectionItem sectionItem in item.mode3)
            {
                section3.Instance.baseAdd(new Setting() { Index = index2.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index2++;
            }
            config.Sections.Add("Mode3", section3);
            int index3 = 1;
            foreach (SectionItem sectionItem in item.mode4)
            {
                section4.Instance.baseAdd(new Setting() { Index = index3.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index3++;
            }
            config.Sections.Add("Mode4", section4);
            int index4 = 1;
            foreach (SectionItem sectionItem in item.mode5)
            {
                section5.Instance.baseAdd(new Setting() { Index = index4.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index4++;
            }
            config.Sections.Add("Mode5", section5);
            int index5 = 1;
            foreach (SectionItem sectionItem in item.motor)
            {
                section6.Instance.baseAdd(new Setting() { Index = index5.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index5++;
            }
            config.Sections.Add("Motor", section6);
            int index6 = 1;
            foreach (SectionItem sectionItem in item.fan)
            {
                section7.Instance.baseAdd(new Setting() { Index = index6.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index6++;
            }
            config.Sections.Add("Fan", section7);
            int index7 = 1;
            foreach (SectionItem sectionItem in item.heater1)
            {
                section8.Instance.baseAdd(new Setting() { Index = index7.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index7++;
            }
            config.Sections.Add("Heater1", section8);
            int index8 = 1;
            foreach (SectionItem sectionItem in item.heater2)
            {
                section9.Instance.baseAdd(new Setting() { Index = index8.ToString(), Name = sectionItem.Name, Value1 = sectionItem.Value1, Value2 = sectionItem.Value2 });
                index8++;
            }
            config.Sections.Add("Heater2", section9);
            config.Save();
        }
    }
}
