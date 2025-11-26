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
    /// HeaderView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class HeaderView : UserControl
    {
        public HeaderView()
        {
            InitializeComponent();
        }

        [Category("Text"), Description("Set Text")]
        public string Title
        {
            get
            {
                return this.header1.Content.ToString();
            }
            set
            {
                this.header1.Content = value;
            }
        }

        [Category("Text"), Description("Set Text")]
        public string Text
        {
            get
            {
                return this.header2.Content.ToString();
            }
            set
            {
                this.header2.Content = value;
            }
        }

        [Category("TitleColor"), Description("Set Title Color")]
        public string TitleColor
        {
            get
            {
                return this.header1.Foreground.ToString();
            }
            set
            {
                this.header1.Foreground = new BrushConverter().ConvertFromString(value) as SolidColorBrush;
            }
        }

        [Category("TitleSize"), Description("Set Title Size")]
        public double TitleSize
        {
            get
            {
                return this.header1.FontSize;
            }
            set
            {
                this.header1.FontSize = value;
            }
        }

        [Category("ContentSize"), Description("Set Content Size")]
        public double ContentSize
        {
            get
            {
                return this.header2.FontSize;
            }
            set
            {
                this.header2.FontSize = value;
            }
        }

        [Category("TitleBorderBrush"), Description("Set Title BorderBrush")]
        public Brush TitleBroderBrush
        {
            get
            {
                return this.header1.BorderBrush;
            }
            set
            {
                this.header1.BorderBrush = value;
            }
        }

        [Category("ContentBorderBrush"), Description("Set Content BorderBrush")]
        public Brush ContentBroderBrush
        {
            get
            {
                return this.header2.BorderBrush;
            }
            set
            {
                this.header2.BorderBrush = value;
            }
        }

        [Category("ContentWeight"), Description("Set ContentWeight")]
        public FontWeight ContentWeight
        {
            get
            {
                return this.header1.FontWeight;
            }
            set
            {
                this.header1.FontWeight = value;
            }
        }

        [Category("ContentColor"), Description("Set Title Color")]
        public string ContentColor
        {
            get
            {
                return this.header2.Foreground.ToString();
            }
            set
            {
                this.header2.Foreground = new BrushConverter().ConvertFromString(value) as SolidColorBrush;
            }
        }
        [Category("TitleBackground"), Description("Set Title Background")]
        public string TitleBackground
        {
            get
            {
                return this.header1.Background.ToString();
            }
            set
            {
                this.header1.Background = new BrushConverter().ConvertFromString(value) as SolidColorBrush;
            }
        }
    }
}
