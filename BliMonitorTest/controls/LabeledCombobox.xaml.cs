using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BliMonitorTest.controls
{
    /// <summary>
    /// LabeledCombobox.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LabeledCombobox : UserControl
    {
        public LabeledCombobox()
        {
            InitializeComponent();
        }

        [Category("Text"), Description("Set Text")]
        public string Title
        {
            get
            {
                return this.label.Content.ToString();
            }
            set
            {
                this.label.Content = value;
            }
        }
    }
}
