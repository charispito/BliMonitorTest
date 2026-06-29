using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public class ErrorData: INotifyPropertyChanged
    {
        private string _Name;
        public string Name
        {
            get { return _Name; }
            set
            {
                _Name = value;
                Notify("Name");
            }
        }

        private string _Value;
        public string Value
        {
            get { return _Value; }
            set
            {
                _Value = value;
                Notify("Value");
            }
        }

        private string _Value2;
        public string Value2
        {
            get { return _Value2; }
            set
            {
                _Value2 = value;
                Notify("Value2");
            }
        }

        private string _Value3;
        public string Value3
        {
            get { return _Value3; }
            set
            {
                _Value3 = value;
                Notify("Value3");
            }
        }

        private string _Value4;
        public string Value4
        {
            get { return _Value4; }
            set
            {
                _Value4 = value;
                Notify("Value4");
            }
        }

        private string _Value5;
        public string Value5
        {
            get { return _Value5; }
            set
            {
                _Value5 = value;
                Notify("Value5");
            }
        }

        private string _Value6;
        public string Value6
        {
            get { return _Value6; }
            set
            {
                _Value6 = value;
                Notify("Value6");
            }
        }

        private string _Value7;
        public string Value7
        {
            get { return _Value7; }
            set
            {
                _Value7 = value;
                Notify("Value7");
            }
        }

        private string _Value8;
        public string Value8
        {
            get { return _Value8; }
            set
            {
                _Value8 = value;
                Notify("Value8");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void Notify(string propName)
        {
            try
            {
                if (this.PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propName));
                }
            }
            catch (StackOverflowException e)
            {
                Console.WriteLine(e.ToString());
            }

        }
    }
}
