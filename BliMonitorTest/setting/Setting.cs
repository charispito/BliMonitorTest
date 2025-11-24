using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.setting
{
    public class Setting: ConfigurationElement
    {
        [ConfigurationProperty("Name", IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string)base["Name"];}
            set { base["Name"] = value;}
        }
        [ConfigurationProperty("Value1", IsKey = false, IsRequired = true)]
        public int Value1
        {
            get { return (int)base["Value1"];}
            set { base["Value1"] = value;}
        }
        [ConfigurationProperty("Value2", IsKey = false, IsRequired = false)]
        public int Value2
        {
            get { return (int)base["Value2"]; }
            set { base["Value2"] = value; }
        }
        [ConfigurationProperty("Index", IsKey = true, IsRequired = false)]
        public string Index
        {
            get { return (string)base["Index"]; }
            set { base["Index"] = value; }
        }
    }
}
