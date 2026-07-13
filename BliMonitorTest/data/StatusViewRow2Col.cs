using System.ComponentModel;

namespace BliMonitorTest.data
{
    public class StatusViewRow2Col : INotifyPropertyChanged
    {
        private string _name1;
        private string _value1;
        private string _name2;
        private string _value2;

        public string Name1
        {
            get => _name1;
            set
            {
                if (_name1 != value)
                {
                    _name1 = value;
                    OnPropertyChanged(nameof(Name1));
                }
            }
        }

        public string Value1
        {
            get => _value1;
            set
            {
                if (_value1 != value)
                {
                    _value1 = value;
                    OnPropertyChanged(nameof(Value1));
                }
            }
        }

        public string Name2
        {
            get => _name2;
            set
            {
                if (_name2 != value)
                {
                    _name2 = value;
                    OnPropertyChanged(nameof(Name2));
                }
            }
        }

        public string Value2
        {
            get => _value2;
            set
            {
                if (_value2 != value)
                {
                    _value2 = value;
                    OnPropertyChanged(nameof(Value2));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
