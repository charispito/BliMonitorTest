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
    /// FourItemGroup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class FourItemGroup : UserControl
    {
        public FourItemGroup()
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
        public string Value3Text
        {
            get
            {
                return this.Value3.Content.ToString();
            }
            set
            {
                this.Value3.Content = value;
            }
        }
        [Category("Text"), Description("Set Text")]
        public string Value4Text
        {
            get
            {
                return this.Value4.Content.ToString();
            }
            set
            {
                this.Value4.Content = value;
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

        [Category("Text"), Description("Set Text")]
        public string Content3
        {
            get
            {
                return this.Item3Title.Content.ToString();
            }
            set
            {
                this.Item3Title.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Unit3Text
        {
            get
            {
                return this.Unit3.Content.ToString();
            }
            set
            {
                this.Unit3.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Content4
        {
            get
            {
                return this.Item4Title.Content.ToString();
            }
            set
            {
                this.Item4Title.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Unit4Text
        {
            get
            {
                return this.Unit4.Content.ToString();
            }
            set
            {
                this.Unit4.Content = value;
            }
        }
    }
}
