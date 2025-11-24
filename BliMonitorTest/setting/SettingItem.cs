using BliMonitorTest.data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.setting
{
    public class SettingItem
    {
        public List<SectionItem> mode1 { get; set; }
        public List<SectionItem> mode2 { get; set; }
        public List<SectionItem> mode3 { get; set; }
        public List<SectionItem> mode4 { get; set; }
        public List<SectionItem> mode5 { get; set; }
        public List<SectionItem> motor { get; set; }
        public List<SectionItem> fan { get; set; }
        public List<SectionItem> heater1 { get; set; }
        public List<SectionItem> heater2 { get; set; }
    }
}
