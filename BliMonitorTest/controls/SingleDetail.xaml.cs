using System.Windows.Controls;

namespace BliMonitorTest.controls
{
    public partial class SingleDetail : UserControl
    {
        public SingleDetail()
        {
            InitializeComponent();
            // 기본 제목
            MapTitles(DefaultNames);
        }

        public static readonly string[] DefaultNames = new[]
        {
            "연속출수","정량출수","자유출수","고온수","온수", "약온수","차일드락","상온수","약냉수","냉수"
        };

        static string OnOff(bool v) => v ? "ON" : "OFF";

        public void MapTitles(string[] names)
        {
            var arr = new[] { DL0, DL1, DL2, DL3, DL4, DL5, DL6, DL7, DL8, DL9 };
            for (int i = 0; i < arr.Length && i < names.Length; i++)
                if (!string.IsNullOrWhiteSpace(names[i]))
                    arr[i].label.Content = names[i]; // DoubleLabel의 제목 영역
        }

        public void UpdateButtons( bool cont, bool volume, bool free, bool highHot, bool hot, bool warm, bool child, bool room, bool mildCold, bool cold)
        {
            DL0.cont.Content = OnOff(cont);
            DL1.cont.Content = OnOff(volume);
            DL2.cont.Content = OnOff(free);
            DL3.cont.Content = OnOff(highHot);
            DL4.cont.Content = OnOff(hot);

            DL5.cont.Content = OnOff(warm);
            DL6.cont.Content = OnOff(child);
            DL7.cont.Content = OnOff(room);
            DL8.cont.Content = OnOff(mildCold);
            DL9.cont.Content = OnOff(cold);
        }
    }
}
