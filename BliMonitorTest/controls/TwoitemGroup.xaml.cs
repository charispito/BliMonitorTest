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
    /// TwoitemGroup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TwoitemGroup : UserControl
    {
        public TwoitemGroup()
        {
            InitializeComponent();
        }

        [Category("Text"), Description("Set Text")]
        public string Text
        {
            get
            {
                return this.Title.Content.ToString();
            }
            set
            {
                this.Title.Content = value;
            }
        }
        [Category("Text"), Description("Set Text")]
        public string Content1
        {
            get
            {
                return this.Item1Title.Content.ToString();
            }
            set
            {
                this.Item1Title.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Value1Text
        {
            get
            {
                return this.Value1.Content.ToString();
            }
            set
            {
                this.Value1.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Value2Text
        {
            get
            {
                return this.Value2.Content.ToString();
            }
            set
            {
                this.Value2.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Unit1
        {
            get
            {
                return this.Unit.Content.ToString();
            }
            set
            {
                this.Unit.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Content2
        {
            get
            {
                return this.Item2Title.Content.ToString();
            }
            set
            {
                this.Item2Title.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Unit2Text
        {
            get
            {
                return this.Unit2.Content.ToString();
            }
            set
            {
                this.Unit2.Content = value;
            }
        }
    }
}
