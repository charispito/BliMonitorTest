using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public class SettingData: INotifyPropertyChanged
    {
        private string _Name;
        public string Name
        {
            get { return _Name; }
            set { 
                _Name = value;
                Notify("Name");
            }
        }

        private int _Value;
        public int Value
        {
            get { return _Value; }
            set
            {
                _Value = value;
                Notify("Value");
            }
        }

        private int _Value2;
        public int Value2
        {
            get { return _Value2; }
            set
            {
                _Value2 = value;
                Notify("Value2");
            }
        }

        private int _Value3;
        public int Value3
        {
            get { return _Value3; }
            set
            {
                _Value3 = value;
                Notify("Value3");
            }
        }

        private int _Value4;
        public int Value4
        {
            get { return _Value4; }
            set
            {
                _Value4 = value;
                Notify("Value4");
            }
        }

        private int _Value5;
        public int Value5
        {
            get { return _Value5; }
            set
            {
                _Value5 = value;
                Notify("Value5");
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
