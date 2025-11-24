using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.setting
{
    public class SettingSection : ConfigurationSection
    {
        [ConfigurationProperty("", IsRequired = true, IsDefaultCollection = true)]
        public SettingCollection Instance
        {
            get { return (SettingCollection)this[""]; }
            set { this[""] = value; }
        }
    }
}
