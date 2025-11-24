using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BliMonitorTest.data
{
    public class CompileData: INotifyPropertyChanged
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

        private int _Year;
        public int Year
        {
            get { return _Year; }
            set
            {
                _Year = value;
                Notify("Year");
            }
        }

        private int _Month;
        public int Month
        {
            get { return _Month; }
            set
            {
                _Month = value;
                Notify("_Month");
            }
        }

        private int _Day;
        public int Day
        {
            get { return _Day; }
            set
            {
                _Day = value;
                Notify("Day");
            }
        }

        private int _Ver;
        public int Ver
        {
            get { return _Ver; }
            set
            {
                _Ver = value;
                Notify("Ver");
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
