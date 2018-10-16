using System.ComponentModel;

namespace SoliditySHA3MinerUI.API
{
    public class Dashboard : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion INotifyPropertyChanged

        private string _PciBusID;

        public string PciBusID
        {
            get => string.IsNullOrWhiteSpace(_PciBusID)
                ? "--"
                : _PciBusID;
            set
            {
                _PciBusID = value;
                OnPropertyChanged("PciBusID");
            }
        }

        private string _Brand;

        public string Brand
        {
            get => string.IsNullOrWhiteSpace(_Brand)
                ? "--"
                : _Brand;
            set
            {
                _Brand = value;
                OnPropertyChanged("Brand");
            }
        }

        private string _Name;

        public string Name
        {
            get => string.IsNullOrWhiteSpace(_Name)
                ? "--"
                : _Name;
            set
            {
                _Name = value;
                OnPropertyChanged("Name");
            }
        }

        private decimal _Hashrate;

        public decimal Hashrate
        {
            get => _Hashrate;
            set
            {
                _Hashrate = value;
                OnPropertyChanged("Hashrate");
                OnPropertyChanged("Hashrate_String");
            }
        }

        private string _HashRateUnit;

        public string HashRateUnit
        {
            get => _HashRateUnit ?? string.Empty;
            set
            {
                _HashRateUnit = value;
                OnPropertyChanged("HashrateUnit");
            }
        }

        public string Hashrate_String
        {
            get => (_Hashrate >= 1000)
                ? (_Hashrate.ToString("N0") + _HashRateUnit)
                : (_Hashrate > 100)
                ? (_Hashrate.ToString("N1") + _HashRateUnit)
                : (_Hashrate > 10)
                ? (_Hashrate.ToString("N2") + _HashRateUnit)
                : (_Hashrate > 0)
                ? (_Hashrate.ToString("N3") + _HashRateUnit)
                : ("--" + _HashRateUnit);
        }

        private decimal _Intensity;

        public decimal Intensity
        {
            get => _Intensity;
            set
            {
                _Intensity = value;
                OnPropertyChanged("Intensity");
                OnPropertyChanged("Intensity_String");
            }
        }

        public string Intensity_String
        {
            get => (_Intensity > 0)
                ? (_Intensity.ToString("N4"))
                : ("--");
        }

        private decimal _UtilizationPercent;

        public decimal UtilizationPercent
        {
            get => _UtilizationPercent;
            set
            {
                _UtilizationPercent = value;
                OnPropertyChanged("UtilizationPercent");
                OnPropertyChanged("UtilizationPercent_String");
            }
        }

        public string UtilizationPercent_String
        {
            get => (_UtilizationPercent > 0)
                ? (_UtilizationPercent.ToString("N0") + '%')
                : ("--%");
        }

        private decimal _Temperature;

        public decimal Temperature
        {
            get => _Temperature;
            set
            {
                _Temperature = value;
                OnPropertyChanged("Temperature");
                OnPropertyChanged("Temperature_String");
            }
        }

        public string Temperature_String
        {
            get => (_Temperature > int.MinValue)
                ? (_Temperature.ToString("N0") + "°C")
                : ("--°C");
        }

        private int _FanTachometer;

        public int FanTachometer
        {
            get => _FanTachometer;
            set
            {
                _FanTachometer = value;
                OnPropertyChanged("FanTachometer");
                OnPropertyChanged("FanTachometer_String");
            }
        }

        public string FanTachometer_String
        {
            get => (_FanTachometer > 0)
                ? (_FanTachometer.ToString() + "RPM")
                : ("--RPM");
        }

        private decimal _FanLevelPercent;

        public decimal FanLevelPercent
        {
            get => _FanLevelPercent;
            set
            {
                _FanLevelPercent = value;
                OnPropertyChanged("FanLevelPercent");
                OnPropertyChanged("FanLevelPercent_String");
            }
        }

        public string FanLevelPercent_String
        {
            get => (_FanLevelPercent > 0)
                ? (_FanLevelPercent.ToString("N0") + '%')
                : ("--%");
        }

        private int _CoreClock;

        public int CoreClock
        {
            get => _CoreClock;
            set
            {
                _CoreClock = value;
                OnPropertyChanged("CoreClock");
                OnPropertyChanged("CoreClock_String");
            }
        }

        public string CoreClock_String
        {
            get => (_CoreClock > 0)
                ? (_CoreClock.ToString() + "MHz")
                : ("--MHz");
        }

        private int _MemoryClock;

        public int MemoryClock
        {
            get => _MemoryClock;
            set
            {
                _MemoryClock = value;
                OnPropertyChanged("MemoryClock");
                OnPropertyChanged("MemoryClock_String");
            }
        }

        public string MemoryClock_String
        {
            get => (_MemoryClock > 0)
                ? (_MemoryClock.ToString() + "MHz")
                : ("--MHz");
        }

        private decimal _PowerLevelPercent;

        public decimal PowerLevelPercent
        {
            get => _PowerLevelPercent;
            set
            {
                _PowerLevelPercent = value;
                OnPropertyChanged("PowerLevelPercent");
                OnPropertyChanged("PowerLevelPercent_String");
            }
        }

        public string PowerLevelPercent_String
        {
            get => (_PowerLevelPercent > 0)
                ? (_PowerLevelPercent.ToString("N2") + '%')
                : ("--%");
        }
    }
}