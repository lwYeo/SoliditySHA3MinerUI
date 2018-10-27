using System;
using System.ComponentModel;

namespace SoliditySHA3MinerUI.API
{
    public class Summary : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion INotifyPropertyChanged

        private string _MinerAddress;

        public string MinerAddress
        {
            get => string.IsNullOrWhiteSpace(_MinerAddress)
                ? "--"
                : _MinerAddress;
            set
            {
                _MinerAddress = value;
                OnPropertyChanged("MinerAddress");
            }
        }

        private string _MiningURL;

        public string MiningURL
        {
            get => string.IsNullOrWhiteSpace(_MiningURL)
                ? "--"
                : _MiningURL;
            set
            {
                _MiningURL = value;
                OnPropertyChanged("MiningURL");
            }
        }

        private string _CurrentChallenge;

        public string CurrentChallenge
        {
            get => string.IsNullOrWhiteSpace(_CurrentChallenge)
                ? "--"
                : _CurrentChallenge;
            set
            {
                _CurrentChallenge = value;
                OnPropertyChanged("CurrentChallenge");
            }
        }

        private ulong _CurrentDifficulty;

        public ulong CurrentDifficulty
        {
            get => _CurrentDifficulty;
            set
            {
                _CurrentDifficulty = value;
                OnPropertyChanged("CurrentDifficulty");
                OnPropertyChanged("CurrentDifficulty_String");
            }
        }

        public string CurrentDifficulty_String
        {
            get => (_CurrentDifficulty > 0)
                ? (_CurrentDifficulty.ToString())
                : ("--");
        }

        private ulong _EstimateTimeLeftToSolveBlock;

        public ulong EstimateTimeLeftToSolveBlock
        {
            get => _EstimateTimeLeftToSolveBlock;
            set
            {
                _EstimateTimeLeftToSolveBlock = value;
                OnPropertyChanged("EstimateTimeLeftToSolveBlock");
                OnPropertyChanged("EstimateTimeLeftToSolveBlock_String");
            }
        }

        public string EstimateTimeLeftToSolveBlock_String
        {
            get
            {
                if (_EstimateTimeLeftToSolveBlock == 0)
                    return "--d --h --m --s";

                var seconds = _EstimateTimeLeftToSolveBlock;

                var days = (uint)Math.Floor(seconds / 60 / 60 / 24d);
                seconds -= ((ulong)days * 60 * 60 * 24);

                var hours = (uint)Math.Floor(seconds / 60 / 60d);
                seconds -= (hours * 60 * 60);

                var minutes = (uint)Math.Floor(seconds / 60d);
                seconds -= (minutes * 60);
                
                return string.Format("{0:00}d {1:00}h {2:00}m {3:00}s", days, hours, minutes, seconds);
            }
        }

        private decimal _EffectiveHashRate;

        public decimal EffectiveHashRate
        {
            get => _EffectiveHashRate;
            set
            {
                _EffectiveHashRate = value;
                OnPropertyChanged("EffectiveHashRate");
                OnPropertyChanged("EffectiveHashRate_String");
                OnPropertyChanged("HashrateLuck_String");
            }
        }

        public string EffectiveHashRate_String
        {
            get => (_EffectiveHashRate >= 1000)
                ? (_EffectiveHashRate.ToString("N0") + _HashRateUnit)
                : (_EffectiveHashRate > 100)
                ? (_EffectiveHashRate.ToString("N1") + _HashRateUnit)
                : (_EffectiveHashRate > 10)
                ? (_EffectiveHashRate.ToString("N2") + _HashRateUnit)
                : (_EffectiveHashRate > 0)
                ? (_EffectiveHashRate.ToString("N3") + _HashRateUnit)
                : ("--" + _HashRateUnit);
        }

        private decimal _TotalHashRate;

        public decimal TotalHashRate
        {
            get => _TotalHashRate;
            set
            {
                _TotalHashRate = value;
                OnPropertyChanged("TotalHashRate");
                OnPropertyChanged("TotalHashRate_String");
                OnPropertyChanged("HashrateLuck_String");
            }
        }

        public string TotalHashRate_String
        {
            get => (_TotalHashRate >= 1000)
                ? (_TotalHashRate.ToString("N0") + _HashRateUnit)
                : (_TotalHashRate > 100)
                ? (_TotalHashRate.ToString("N1") + _HashRateUnit)
                : (_TotalHashRate > 10)
                ? (_TotalHashRate.ToString("N2") + _HashRateUnit)
                : (_TotalHashRate > 0)
                ? (_TotalHashRate.ToString("N3") + _HashRateUnit)
                : ("--" + _HashRateUnit);
        }

        public string HashRateLuck_String
        {
            get => (_EffectiveHashRate > 0 && _TotalHashRate > 0)
                ? (_EffectiveHashRate / _TotalHashRate * 100).ToString("N2") + '%'
                : ("--%");
        }

        private string _HashRateUnit;

        public string HashRateUnit
        {
            get => _HashRateUnit ?? string.Empty;
            set
            {
                _HashRateUnit = value;
                OnPropertyChanged("HashRateUnit");
            }
        }

        private int _LastSubmitLatencyMS;

        public int LastSubmitLatencyMS
        {
            get => _LastSubmitLatencyMS;
            set
            {
                _LastSubmitLatencyMS = value;
                OnPropertyChanged("LastSubmitLatencyMS");
                OnPropertyChanged("LastSubmitLatencyMS_String");
            }
        }

        public string LastSubmitLatencyMS_String
        {
            get => (_LastSubmitLatencyMS > 0)
                ? (_LastSubmitLatencyMS.ToString() + "ms")
                : ("--ms");
        }

        private int _LatencyMS;

        public int LatencyMS
        {
            get => _LatencyMS;
            set
            {
                _LatencyMS = value;
                OnPropertyChanged("LatencyMS");
                OnPropertyChanged("LatencyMS_String");
            }
        }

        public string LatencyMS_String
        {
            get => (_LatencyMS > 0)
                ? (_LatencyMS.ToString() + "ms")
                : ("--ms");
        }

        private ulong _Uptime;

        public ulong Uptime
        {
            get => _Uptime;
            set
            {
                _Uptime = value;
                OnPropertyChanged("Uptime");
                OnPropertyChanged("Uptime_String");
            }
        }

        public string Uptime_String
        {
            get
            {
                var uptimeTS = TimeSpan.FromSeconds(_Uptime);

                return (_Uptime >= (60 * 60 * 24)) // 1 day
                    ? string.Format("{0}d {1}h {2}m",
                                    uptimeTS.Days.ToString("000"),
                                    uptimeTS.Hours.ToString("00"),
                                    uptimeTS.Minutes.ToString("00"))
                    : (_Uptime >= (60 * 60)) // 1 hour
                    ? string.Format("{0}h {1}m {2}s",
                                    uptimeTS.Hours.ToString("00"),
                                    uptimeTS.Minutes.ToString("00"),
                                    uptimeTS.Seconds.ToString("00"))
                    : (_Uptime >= 60)  // 1 minute
                    ? string.Format("{0}m {1}s",
                                    uptimeTS.Minutes.ToString("00"),
                                    uptimeTS.Seconds.ToString("00"))
                    : (_Uptime > 1)  // 1 second
                    ? _Uptime.ToString() + 's'
                    : "--s";
            }
        }

        private ulong _AcceptedShares;

        public ulong AcceptedShares
        {
            get => _AcceptedShares;
            set
            {
                _AcceptedShares = value;
                OnPropertyChanged("AcceptedShares");
                OnPropertyChanged("AcceptedShares_String");
                OnPropertyChanged("SubmissionRate_String");
            }
        }

        public string AcceptedShares_String
        {
            get => (_AcceptedShares > 0)
                ? (_AcceptedShares.ToString())
                : ("--");
        }

        private ulong _RejectedShares;

        public ulong RejectedShares
        {
            get => _RejectedShares;
            set
            {
                _RejectedShares = value;
                OnPropertyChanged("RejectedShares");
                OnPropertyChanged("RejectedShares_String");
                OnPropertyChanged("SubmissionRate_String");
            }
        }

        public string RejectedShares_String
        {
            get => (_RejectedShares > 0)
                ? (_RejectedShares.ToString())
                : ("--");
        }

        public string SubmissionRate_String
        {
            get => (_AcceptedShares > 0)
                ? (((decimal)_AcceptedShares / (_AcceptedShares + _RejectedShares) * 100).ToString("N1") + '%')
                : ("--%");
        }

        private decimal _CpuLoad;

        public decimal CpuLoad
        {
            get => _CpuLoad;
            set
            {
                _CpuLoad = value;
                OnPropertyChanged("CpuLoad");
                OnPropertyChanged("CpuLoad_String");
            }
        }

        public string CpuLoad_String
        {
            get => (_CpuLoad >= 0)
                ? (_CpuLoad.ToString("N2") + '%')
                : ("--%");
        }

        private int _GpuMaxTemperature;

        public int GpuMaxTemperature
        {
            get => _GpuMaxTemperature;
            set
            {
                _GpuMaxTemperature = value;
                OnPropertyChanged("GpuMaxTemperature");
                OnPropertyChanged("GpuMaxTemperature_String");
            }
        }

        public string GpuMaxTemperature_String
        {
            get => (_GpuMaxTemperature > int.MinValue)
                ? (_GpuMaxTemperature.ToString() + "°C")
                : "--°C";
        }
    }
}