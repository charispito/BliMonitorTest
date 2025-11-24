using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public class ReadData
    {
        public string date { get; set; }
        public int mode { get; set; }
        public string remain_time { get; set; }
        public int heater_temp { get; set; }
        public float heater_off_time { get; set; }
        public int air_temp { get; set; }
        public int fan_speed { get; set; }
        public int hot_air_temp { get; set; }
        public int hot_air_ontime { get; set; }
        public string motor { get; set; }
        public double motor_current { get; set; }
        public int number { get; set; }
        public int offtime_sum { get; set; }
        public int off_average { get; set; }
        public int air_sum { get; set; }
        public int air_average { get; set; }
        public string model { get; set; }
    }
}
