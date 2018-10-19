using System;
using static SoliditySHA3MinerUI.MinerInstance;

namespace SoliditySHA3MinerUI
{
    public interface IMinerInstance
    {
        event OnLogUpdatedDelegate OnLogUpdated;

        event EventHandler Exited;

        bool IsRunning { get; }
        string Log { get; }
        uint MaxLogLines { get; set; }
        uint WatchDogInterval { get; set; }

        void ClearLogs();

        /// <summary>
        /// Returns true if instance has started, otherwise false.
        /// </summary>
        bool Start();

        /// <summary>
        /// Returns true if instance has been gracefully stopped, otherwise false.
        /// </summary>
        bool Stop();
    }
}