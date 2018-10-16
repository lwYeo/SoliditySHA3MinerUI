using System;
using System.Collections.Generic;

namespace SoliditySHA3MinerUI.API
{
    public class MinerResponse
    {
        public DateTime SystemDateTime => DateTime.Now;
        public float EffectiveHashRate { get; set; }
        public float TotalHashRate { get; set; }
        public string HashRateUnit { get; set; }
        public int LatencyMS { get; set; }
        public long Uptime { get; set; }
        public long AcceptedShares { get; set; }
        public long RejectedShares { get; set; }
        public List<Miner> Miners { get; set; }

        public MinerResponse() => Miners = new List<Miner>();

        public class Miner
        {
            public string Type { get; set; }
            public int DeviceID { get; set; }
            public uint PciBusID { get; set; }
            public string ModelName { get; set; }
            public float HashRate { get; set; }
            public bool HasMonitoringAPI { get; set; }
        }

        public class CUDA_Miner : Miner
        {
            public float SettingIntensity { get; set; }
            public int SettingMaxCoreClockMHz { get; set; }
            public int SettingMaxMemoryClockMHz { get; set; }
            public int SettingPowerLimitPercent { get; set; }
            public int SettingThermalLimitC { get; set; }
            public int SettingFanLevelPercent { get; set; }

            public int CurrentFanTachometerRPM { get; set; }
            public int CurrentTemperatureC { get; set; }
            public int CurrentCoreClockMHz { get; set; }
            public int CurrentMemoryClockMHz { get; set; }
            public int CurrentUtilizationPercent { get; set; }
            public int CurrentPState { get; set; }
            public string CurrentThrottleReasons { get; set; }
        }

        public class OpenCLMiner : Miner
        {
            public string Platform { get; set; }
            public float SettingIntensity { get; set; }
        }

        public class AMD_Miner : OpenCLMiner
        {
            public int SettingMaxCoreClockMHz { get; set; }
            public int SettingMaxMemoryClockMHz { get; set; }
            public int SettingPowerLimitPercent { get; set; }
            public int SettingThermalLimitC { get; set; }
            public int SettingFanLevelPercent { get; set; }

            public int CurrentFanTachometerRPM { get; set; }
            public int CurrentTemperatureC { get; set; }
            public int CurrentCoreClockMHz { get; set; }
            public int CurrentMemoryClockMHz { get; set; }
            public int CurrentUtilizationPercent { get; set; }
        }
    }
}