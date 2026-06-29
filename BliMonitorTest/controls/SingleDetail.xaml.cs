using System.Windows.Controls;

namespace BliMonitorTest.controls
{
    public partial class SingleDetail : UserControl
    {
        public SingleDetail()
        {
            InitializeComponent();
            MapTitles(DefaultNames);
        }

        public static readonly string[] DefaultNames = new[]
        {
            "HOT 선택", "150mL",
            "WARM 선택", "1000mL",
            "NORMAL 선택", "OUTLET",
            "COOL 선택", "TEST MODE",
            "COLD 선택", "NIGHT",
            "REHEAT", "HEATER PWM"
        };

        static string OnOff(bool v) => v ? "ON" : "OFF";

        public void MapTitles(string[] names)
        {
            var arr = new[] { DL0, DL1, DL2, DL3, DL4, DL5, DL6, DL7, DL8, DL9, DL10, DL11 };
            for (int i = 0; i < arr.Length && i < names.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(names[i]))
                    arr[i].label.Content = names[i];
            }
        }

        public void UpdateButtons(
            bool hot, bool warm, bool normal, bool cool, bool cold,
            bool reheat, bool qty150, bool qty1000, bool outlet,
            bool testMode, bool night, bool heaterPwm)
        {
            DL0.cont.Text = OnOff(hot);
            DL1.cont.Text = OnOff(qty150);

            DL2.cont.Text = OnOff(warm);
            DL3.cont.Text = OnOff(qty1000);

            DL4.cont.Text = OnOff(normal);
            DL5.cont.Text = OnOff(outlet);

            DL6.cont.Text = OnOff(cool);
            DL7.cont.Text = OnOff(testMode);

            DL8.cont.Text = OnOff(cold);
            DL9.cont.Text = OnOff(night);

            DL10.cont.Text = OnOff(reheat);
            DL11.cont.Text = OnOff(heaterPwm);
        }
    }
}
