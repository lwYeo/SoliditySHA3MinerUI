using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SoliditySHA3MinerUI
{
    public class MinerInstance : Process, IMinerInstance
    {
        public delegate void OnLogUpdatedDelegate(string updatedLog, string newLog, int removedLogIndex);

        public static FileInfo MinerPath => new FileInfo(Path.Combine(Helper.FileSystem.MinerDirectory.FullName, "SoliditySHA3Miner.dll"));
        private static FileInfo PreLaunchPath => new FileInfo(Path.Combine(Helper.FileSystem.MinerDirectory.FullName, "prelaunch.bat"));

        private object _loggingLock;
        private uint _loggedLinesCount;

        #region P/Invoke

        private enum CtrlTypes : uint
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

        private delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GenerateConsoleCtrlEvent(CtrlTypes dwCtrlEvent, uint dwProcessGroupId);

        #endregion P/Invoke

        #region IMinerInstance

        public event OnLogUpdatedDelegate OnLogUpdated;

        public bool IsRunning { get; private set; }

        public string Log { get; private set; }

        public uint MaxLogLines { get; set; }

        public void ClearLogs()
        {
            lock (_loggingLock)
            {
                _loggedLinesCount = 0;
                Log = string.Empty;

                Task.Factory.StartNew(() => OnLogUpdated(Log, string.Empty, 0));
            }
        }

        public new bool Start()
        {
            try
            {
                base.Start();
                BeginOutputReadLine();
                Thread.Yield();
                Thread.Sleep(0);
                Refresh();

                IsRunning = !HasExited;
                return IsRunning;
            }
            catch { return false; }
        }

        public bool Stop()
        {
            var hasExited = false;
            try
            {
                if (!IsRunning && HasExited) return true;
                else if (MainWindowHandle != IntPtr.Zero)
                {
                    CloseMainWindow();
                    WaitForExit(1000 * 15);
                }
                else
                {
                    if (AttachConsole((uint)Id))
                    {
                        SetConsoleCtrlHandler(null, true); // Disable console Ctrl-C handling
                        try
                        {
                            GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0);
                            WaitForExit(1000 * 15);
                            FreeConsole();
                        }
                        finally { SetConsoleCtrlHandler(null, false); } // Re-enable console Ctrl-C handling
                    }
                }
                Thread.Yield();
                Thread.Sleep(0);
                Refresh();

                if (!HasExited)
                {
                    Kill();
                    return false;
                }
                return HasExited;
            }
            catch (Exception ex)
            {
                try { hasExited = HasExited; }
                catch { hasExited = true; }
                if (hasExited) return true;

                Helper.Processor.ShowMessageBox("Error stopping miner", ex.Message);
                return false;
            }
        }

        #endregion IMinerInstance

        public MinerInstance(string preLaunchScript)
        {
            _loggingLock = new object();
            MaxLogLines = 1000;
            OutputDataReceived += MinerInstance_OutputDataReceived;
            ErrorDataReceived += MinerInstance_ErrorDataReceived;
            try
            {
                if (PreLaunchPath.Exists) PreLaunchPath.Delete();

                if (!string.IsNullOrWhiteSpace(preLaunchScript))
                {
                    File.WriteAllText(PreLaunchPath.FullName, preLaunchScript);

                    using (var preLaunchProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            WorkingDirectory = PreLaunchPath.DirectoryName,
                            FileName = PreLaunchPath.Name,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    })
                    {
                        preLaunchProcess.Start();
                        preLaunchProcess.WaitForExit();
                        PreLaunchPath.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.Processor.ShowMessageBox("Error launching Pre-launch script", ex.Message);
            }

            StartInfo = new ProcessStartInfo
            {
                WorkingDirectory = MinerPath.DirectoryName,
                FileName = "dotnet",
                Arguments = MinerPath.Name,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Exited += MinerInstance_Exited;
        }

        private void UpdateLog(string newLog)
        {
            lock (_loggingLock)
            {
                if (MaxLogLines.Equals(0)) return;

                var removedLogIndex = 0;

                if (_loggedLinesCount >= MaxLogLines)
                {
                    var firstNewLinePosition = Log.IndexOf(Environment.NewLine);
                    if (firstNewLinePosition > -1)
                    {
                        removedLogIndex = firstNewLinePosition + Environment.NewLine.Length;
                        Log = Log.Substring(removedLogIndex);
                    }
                    _loggedLinesCount--;
                }
                Log += newLog + Environment.NewLine;
                _loggedLinesCount++;

                Task.Factory.StartNew(() => OnLogUpdated(Log, newLog, removedLogIndex));
            }
        }

        private void MinerInstance_Exited(object sender, EventArgs e)
        {
            IsRunning = false;
            Dispose();
        }

        private void MinerInstance_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            UpdateLog(e.Data);
        }

        private void MinerInstance_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            UpdateLog(e.Data);
        }

        protected override void Dispose(bool disposing)
        {
            Exited -= MinerInstance_Exited;
            base.Dispose(disposing);
        }
    }
}