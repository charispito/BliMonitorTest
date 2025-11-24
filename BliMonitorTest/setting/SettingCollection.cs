using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.setting
{
    public class SettingCollection: ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new Setting();
        }

        public void baseAdd(ConfigurationElement element)
        {
            BaseAdd(element, false);
        }

        public void baseRemove(string item)
        {
            BaseRemove(item);
        }

        public object[] baseGetAllKeys()
        {
            return BaseGetAllKeys();
        }

        public Setting baseGet(string item)
        {
            return (Setting)BaseGet(item);
        }

        public ConfigurationElement Create()
        {
            return CreateNewElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Setting)element).Index;
        }

        public object GetElement(ConfigurationElement element)
        {
            return GetElementKey(element);
        }
    }
}
