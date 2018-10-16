using MahApps.Metro.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace SoliditySHA3MinerUI.API
{
    public class MinerProcessor : IDisposable
    {
        private readonly List<Tuple<Process, TimeSpan, DateTime>> _ProcessList;
        private readonly MainWindow _UI;
        private readonly Timer _timer;
        private bool _isReading;

        public delegate void OnResponseDelegate(MinerReport minerReport);

        public delegate void OnRequestSettingsDelegate(ref JToken settings);

        public event OnResponseDelegate OnResponse;

        public event OnRequestSettingsDelegate OnRequestSettings;

        public MinerReport MinerReport { get; }

        public string URI { get; set; }

        public double Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public MinerProcessor(MainWindow ui)
        {
            MinerReport = new MinerReport();

            _ProcessList = new List<Tuple<Process, TimeSpan, DateTime>>();

            _timer = new Timer(5000) { AutoReset = true };
            _timer.Elapsed += _timer_Elapsed;

            _UI = ui;
        }

        public void Start()
        {
            _ProcessList.Clear();
            _ProcessList.AddRange(Helper.Processor.GetAllRelatedProcessList(includeMain: true).
                                                   Select(p => new Tuple<Process, TimeSpan, DateTime>(p, p.UserProcessorTime, DateTime.Now)));
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            _ProcessList.Clear();
            SetSummaryToPreMineState();
        }

        public void SetSummaryToPreMineState()
        {
            JToken settings = null;
            OnRequestSettings(ref settings);

            _UI.BeginInvoke(() =>
            {
                foreach (var dashboard in MinerReport.DashboardList)
                {
                    dashboard.Hashrate = -1;
                    dashboard.Intensity = -1;
                    dashboard.CoreClock = -1;
                    dashboard.MemoryClock = -1;
                    dashboard.FanTachometer = -1;
                    dashboard.FanLevelPercent = -1;
                    dashboard.PowerLevelPercent = -1;
                    dashboard.UtilizationPercent = -1;
                    dashboard.Temperature = int.MinValue;
                }

                MinerReport.Summary.Uptime = 0;
                MinerReport.Summary.CpuLoad = -1;
                MinerReport.Summary.LatencyMS = -1;
                MinerReport.Summary.LastSubmitLatencyMS = -1;
                MinerReport.Summary.CurrentDifficulty = 0;
                MinerReport.Summary.AcceptedShares = 0;
                MinerReport.Summary.RejectedShares = 0;
                MinerReport.Summary.EffectiveHashRate = -1;
                MinerReport.Summary.TotalHashRate = -1;
                MinerReport.Summary.GpuMaxTemperature = int.MinValue;
                MinerReport.Summary.CurrentChallenge = string.Empty;

                MinerReport.Summary.MinerAddress = settings["minerAddress"].ToString();
                MinerReport.Summary.MiningURL = (string.IsNullOrWhiteSpace(settings["privateKey"].ToString()))
                    ? settings["primaryPool"].ToString()
                    : settings["web3api"].ToString();
            });
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_isReading) return;
            else _isReading = true;
            try
            {
                var apiString = Helper.Network.GetHttpResponse(URI);
                if (string.IsNullOrWhiteSpace(apiString)) return;

                var jAPI = (JObject)JsonConvert.DeserializeObject(apiString);
                if (jAPI == null) return;

                _UI.Invoke(() =>
                {
                    var hashRateUnit = jAPI["HashRateUnit"]?.ToString() ?? string.Empty;
                    var maxGpuTemperature = int.MinValue;

                    MinerReport.DashboardList.Clear();

                    var miners = jAPI["Miners"].Children();
                    foreach (var miner in miners)
                    {
                        var clPlatform = miner["Platform"]?.ToString() ?? string.Empty;

                        var dashboard = new Dashboard
                        {
                            Name = miner["ModelName"]?.ToString(),
                            PciBusID = miner["PciBusID"]?.ToObject<int>().ToString("X2"),
                            Hashrate = miner["HashRate"]?.ToObject<decimal>() ?? -1,
                            HashRateUnit = hashRateUnit,
                            Intensity = miner["SettingIntensity"]?.ToObject<decimal>() ?? -1,
                            UtilizationPercent = miner["CurrentUtilizationPercent"]?.ToObject<decimal>() ?? -1,
                            Temperature = miner["CurrentTemperatureC"]?.ToObject<int>() ?? int.MinValue,
                            FanTachometer = miner["CurrentFanTachometerRPM"]?.ToObject<int>() ?? -1,
                            FanLevelPercent = miner["SettingFanLevelPercent"]?.ToObject<int>() ?? -1,
                            CoreClock = miner["CurrentCoreClockMHz"]?.ToObject<int>() ?? -1,
                            MemoryClock = miner["CurrentMemoryClockMHz"]?.ToObject<int>() ?? -1,
                            PowerLevelPercent = miner["SettingPowerLimitPercent"]?.ToObject<int>() ?? -1
                        };

                        maxGpuTemperature = (dashboard.Temperature > maxGpuTemperature)
                            ? (int)dashboard.Temperature
                            : maxGpuTemperature;

                        var deviceType = miner["Type"]?.ToString();

                        if (deviceType == "CUDA") { dashboard.Brand = "NVIDIA"; }
                        else if (dashboard.Name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) > -1)
                        {
                            dashboard.Brand = "INTEL";
                            dashboard.Name = dashboard.Name;
                        }
                        else if (clPlatform.IndexOf("AMD Accelerated Parallel Processing", StringComparison.OrdinalIgnoreCase) > -1)
                            dashboard.Brand = "AMD";
                        else
                            dashboard.Brand = "Unknown";

                        MinerReport.DashboardList.Add(dashboard);
                    }

                    MinerReport.Summary.CpuLoad = _ProcessList.Sum(p =>
                    {
                        p.Item1.Refresh();
                        var runTime = p.Item1.UserProcessorTime - p.Item2;
                        var totalTime = (DateTime.Now - p.Item3);
                        return (decimal)(runTime.TotalMilliseconds / totalTime.TotalMilliseconds * 100 / Environment.ProcessorCount);
                    });

                    var tempProcessList = _ProcessList.ToList();
                    _ProcessList.Clear();
                    tempProcessList.ForEach(p =>
                    {
                        p.Item1.Refresh();
                        var runTime = p.Item1.UserProcessorTime;
                        _ProcessList.Add(new Tuple<Process, TimeSpan, DateTime>(p.Item1, runTime, DateTime.Now));
                    });
                    tempProcessList.Clear();

                    MinerReport.Summary.HashRateUnit = hashRateUnit;
                    MinerReport.Summary.GpuMaxTemperature = maxGpuTemperature;

                    MinerReport.Summary.Uptime = jAPI["Uptime"]?.ToObject<uint>() ?? 0;
                    MinerReport.Summary.MinerAddress = jAPI["MinerAddress"]?.ToString();
                    MinerReport.Summary.MiningURL = jAPI["MiningURL"]?.ToString();
                    MinerReport.Summary.CurrentChallenge = jAPI["CurrentChallenge"]?.ToString();
                    MinerReport.Summary.CurrentDifficulty = jAPI["CurrentDifficulty"]?.ToObject<uint>() ?? 0;
                    MinerReport.Summary.AcceptedShares = jAPI["AcceptedShares"]?.ToObject<uint>() ?? 0;
                    MinerReport.Summary.RejectedShares = jAPI["RejectedShares"]?.ToObject<uint>() ?? 0;
                    MinerReport.Summary.LastSubmitLatencyMS = jAPI["LastSubmitLatencyMS"]?.ToObject<int>() ?? -1;
                    MinerReport.Summary.LatencyMS = jAPI["LatencyMS"]?.ToObject<int>() ?? -1;
                    MinerReport.Summary.EffectiveHashRate = jAPI["EffectiveHashRate"]?.ToObject<decimal>() ?? -1;
                    MinerReport.Summary.TotalHashRate = jAPI["TotalHashRate"]?.ToObject<decimal>() ?? -1;

                    OnResponse(MinerReport);
                });
            }
            catch { }
            finally { _isReading = false; }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    _timer.Dispose();
                    _ProcessList?.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MinerAPIReader() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}