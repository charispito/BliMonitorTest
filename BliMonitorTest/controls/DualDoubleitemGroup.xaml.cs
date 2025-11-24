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
    /// DualDoubleitemGroup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DualDoubleitemGroup : UserControl
    {
        public DualDoubleitemGroup()
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
        public string ValueText11
        {
            get
            {
                return this.Value11.Content.ToString();
            }
            set
            {
                this.Value11.Content = value;
            }
        }
        [Category("Text"), Description("Set Text")]
        public string ValueText12
        {
            get
            {
                return this.Value12.Content.ToString();
            }
            set
            {
                this.Value12.Content = value;
            }
        }
        [Category("Text"), Description("Set Text")]
        public string ValueText21
        {
            get
            {
                return this.Value21.Content.ToString();
            }
            set
            {
                this.Value21.Content = value;
            }
        }
        [Category("Text"), Description("Set Text")]
        public string ValueText22
        {
            get
            {
                return this.Value22.Content.ToString();
            }
            set
            {
                this.Value22.Content = value;
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
        public string Header1
        {
            get
            {
                return this.Header1Text.Content.ToString();
            }
            set
            {
                this.Header1Text.Content = value;
            }
        }
        [Category("Text"), Description("Set Text")]
        public string Header2
        {
            get
            {
                return this.Header2Text.Content.ToString();
            }
            set
            {
                this.Header2Text.Content = value;
            }
        }
    }
}
