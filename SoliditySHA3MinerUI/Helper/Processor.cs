using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace SoliditySHA3MinerUI.Helper
{
    public static class Processor
    {
        public static string ProductName => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;

        public static string CompanyName => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCompanyAttribute>().Company;

        public static string GetCopyright => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;

        public static Version GetUIVersion => Assembly.GetExecutingAssembly().GetName().Version;

        public static Version MinimumDotnetCoreVersion => new Version("2.1.0");

        public static Version GetUIVersionCompat
        {
            get
            {
                var uiVersion = GetUIVersion;
                return new Version(uiVersion.Major, uiVersion.Minor, uiVersion.Build);
            }
        }

        public static bool GetLocalMinerVersion(out Version version)
        {
            version = new Version();
            try
            {
                if (MinerInstance.MinerPath.Exists)
                {
                    version = new Version(FileVersionInfo.GetVersionInfo(MinerInstance.MinerPath.FullName).ProductVersion);
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        public static bool CheckDotnetCoreVersion(out Version dotnetCoreVersion)
        {
            dotnetCoreVersion = new Version();
            try
            {
                var processOutput = FileSystem.GetProcessOutput("dotnet", "--info");
                if (string.IsNullOrWhiteSpace(processOutput)) return false;

                var rawVersions = processOutput.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).
                                                Where(o => o.TrimStart().StartsWith("Microsoft.NETCore.App")).
                                                Select(o => o.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]).
                                                ToArray();
                if (!rawVersions.Any()) return false;

                var errorMessage = string.Empty;
                var versions = rawVersions.Select(r =>
                {
                    try
                    {
                        var version = string.Empty;
                        foreach (var c in r.TrimStart())
                        {
                            if (!char.IsDigit(c) && !c.Equals('.')) break;
                            version += c;
                        }
                        return new Version(version);
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(errorMessage)) errorMessage += Environment.NewLine;
                        errorMessage += "Error parsing string to version: " + r;
                        return new Version();
                    }
                }).ToList();

                if (!string.IsNullOrWhiteSpace(errorMessage)) ShowMessageBox("Error parsing dotnet core version", errorMessage);

                var highestFoundVersion = dotnetCoreVersion;
                versions.ForEach(v => { if (v >= highestFoundVersion) highestFoundVersion = v; });

                dotnetCoreVersion = highestFoundVersion;
                return highestFoundVersion >= MinimumDotnetCoreVersion;
            }
            catch (Exception ex)
            {
                ShowMessageBox("Error getting dotnet core version", ex.Message);
                return false;
            }
        }

        public static bool GetApiUri(string rawPath, out string uriPath, out string errorMessage)
        {
            var defaultUriPath = "http://127.0.0.1:4078";
            var httpBind = rawPath.ToString();
            errorMessage = string.Empty;
            uriPath = string.Empty;

            if (string.IsNullOrWhiteSpace(httpBind)) httpBind = defaultUriPath;
            else if (httpBind == "0")
            {
                errorMessage = "JSON-API is disabled";
                return false;
            }

            if (!httpBind.StartsWith("http://") || httpBind.StartsWith("https://")) httpBind = "http://" + httpBind;
            if (!httpBind.EndsWith("/")) httpBind += "/";

            if (!int.TryParse(httpBind.Split(':')[2].TrimEnd('/'), out int port))
            {
                errorMessage = "Invalid port provided for JSON-API";
                return false;
            }

            var tempIPAddress = httpBind.Split(new string[] { "//" }, StringSplitOptions.None)[1].Split(':')[0];
            if (!IPAddress.TryParse(tempIPAddress, out IPAddress ipAddress))
            {
                errorMessage = "Invalid IP address provided for JSON-API";
                return false;
            }

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try { socket.Bind(new IPEndPoint(ipAddress, port)); }
                catch (Exception)
                {
                    errorMessage = "JSON-API failed to bind to: " + httpBind;
                    return false;
                }
            };

            uriPath = httpBind;
            return true;
        }

        public static void BuildTree(TreeView treeView, XDocument xDocument)
        {
            var treeNode = new TreeViewItem
            {
                Header = xDocument.Root.Name.LocalName,
                IsExpanded = true
            };
            treeView.Items.Add(treeNode);
            BuildNodes(treeNode, xDocument.Root);
        }

        private static void BuildNodes(TreeViewItem treeNode, XElement xElement)
        {
            foreach (var child in xElement.Nodes())
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Element:
                        var childElement = child as XElement;
                        var childTreeNode = new TreeViewItem
                        {
                            Header = childElement.Name.LocalName,
                            IsExpanded = true
                        };
                        treeNode.Items.Add(childTreeNode);
                        BuildNodes(childTreeNode, childElement);
                        break;

                    case XmlNodeType.Text:
                        var childText = child as XText;
                        treeNode.Items.Add(new TreeViewItem { Header = childText.Value ?? string.Empty });
                        break;
                }
            }
        }

        public static List<Process> GetAllRelatedProcessList(bool includeMain = false)
        {
            var processList = new List<Process>();

            var currentProcess = Process.GetCurrentProcess();
            if (includeMain) processList.Add(currentProcess);

            var query = "Select * From Win32_Process Where ParentProcessId = " + currentProcess.Id;

            using (var searcher = new ManagementObjectSearcher(query))
            {
                processList.AddRange(searcher.Get().
                                              OfType<ManagementObject>().
                                              Select(p => Process.GetProcessById(Convert.ToInt32(p.GetPropertyValue("ProcessId")))));
            }
            return processList;
        }

        public async static Task DownloadLatestMiner(MainWindow mainWindow, string url, Action<bool, FileInfo> onDownloadMinerComplete)
        {
            ProgressDialogController controller = null;
            try
            {
                var fileName = Path.GetFileName(new Uri(url).LocalPath);
                var filePath = new FileInfo(Path.Combine(FileSystem.DownloadDirectory.FullName, fileName));

                if (!filePath.Directory.Exists) filePath.Directory.Create();
                if (filePath.Exists) filePath.Delete();
                filePath.Refresh();

                controller = await mainWindow.ShowProgressAsync("Please wait...", "Downloading miner", isCancelable: true);
                controller.SetProgressBarForegroundBrush(Brushes.Orange);
                controller.SetIndeterminate();
                controller.Maximum = 100d;

                await Task.Yield();
                await Task.Delay(200);

                var downloader = Network.DownloadFromURL(url, filePath.FullName,
                    new DownloadProgressChangedEventHandler((s, progressEvent) =>
                    {
                        mainWindow.Invoke(() =>
                        {
                            var percentage = (double)progressEvent.BytesReceived / progressEvent.TotalBytesToReceive * 100;
                            controller.SetProgress(percentage);
                        });
                    }),
                    new System.ComponentModel.AsyncCompletedEventHandler(async (s, completedEvent) =>
                    {
                        await mainWindow.Invoke(async () =>
                        {
                            if (completedEvent.Error != null)
                            {
                                if (!(completedEvent.Error is WebException) || ((WebException)completedEvent.Error).Status != WebExceptionStatus.RequestCanceled)
                                {
                                    ShowMessageBox("Error downloading miner", completedEvent.Error.Message);
                                    onDownloadMinerComplete(false, null);
                                }
                            }
                            else
                            {
                                filePath.Refresh();
                                onDownloadMinerComplete(true, filePath);
                            }
                            if (controller != null && controller.IsOpen) await controller.CloseAsync();
                        });
                    }));

                controller.Canceled += (s, cancelEvent) => downloader.CancelAsync();
                controller.Closed += (s, closeEvent) => downloader.Dispose();
            }
            catch (Exception ex)
            {
                ShowMessageBox("Error downloading miner", ex.Message);
            }
        }
        
        public async static Task DownloadLatestUiInstaller(MainWindow mainWindow, string url, Action<bool, FileInfo> onDownloadUiInstallerComplete)
        {
            ProgressDialogController controller = null;
            try
            {
                var fileName = Path.GetFileName(new Uri(url).LocalPath);
                var filePath = new FileInfo(Path.Combine(FileSystem.DownloadDirectory.FullName, fileName));

                if (!filePath.Directory.Exists) filePath.Directory.Create();
                if (filePath.Exists) filePath.Delete();
                filePath.Refresh();

                controller = await mainWindow.ShowProgressAsync("Please wait...", "Downloading GUI installer", isCancelable: true);
                controller.SetProgressBarForegroundBrush(Brushes.Orange);
                controller.SetIndeterminate();
                controller.Maximum = 100d;

                await Task.Yield();
                await Task.Delay(200);

                var downloader = Network.DownloadFromURL(url, filePath.FullName,
                    new DownloadProgressChangedEventHandler((s, progressEvent) =>
                    {
                        mainWindow.Invoke(() =>
                        {
                            var percentage = (double)progressEvent.BytesReceived / progressEvent.TotalBytesToReceive * 100;
                            controller.SetProgress(percentage);
                        });
                    }),
                    new System.ComponentModel.AsyncCompletedEventHandler(async (s, completedEvent) =>
                    {
                        await mainWindow.Invoke(async () =>
                        {
                            if (completedEvent.Error != null)
                            {
                                if (!(completedEvent.Error is WebException) || ((WebException)completedEvent.Error).Status != WebExceptionStatus.RequestCanceled)
                                {
                                    ShowMessageBox("Error downloading GUI installer", completedEvent.Error.Message);
                                    onDownloadUiInstallerComplete(false, null);
                                }
                            }
                            else
                            {
                                filePath.Refresh();
                                onDownloadUiInstallerComplete(true, filePath);
                            }
                            if (controller != null && controller.IsOpen) await controller.CloseAsync();
                        });
                    }));

                controller.Canceled += (s, cancelEvent) => downloader.CancelAsync();
                controller.Closed += (s, closeEvent) => downloader.Dispose();
            }
            catch (Exception ex)
            {
                ShowMessageBox("Error downloading GUI installer", ex.Message);
            }
        }

        public static void StartUiInstallerAndExit(FileInfo installerFilePath)
        {
            if (!installerFilePath.Exists)
            {
                ShowMessageBox("Error", "GUI installer file not found");
                return;
            }

            var uiFilePath = Assembly.GetEntryAssembly().Location;
            var batchFilePath = new FileInfo(Path.Combine(FileSystem.LocalAppDirectory.FullName, "install.bat"));
            if (batchFilePath.Exists) batchFilePath.Delete();
            batchFilePath.Refresh();

            using (var batchStream = batchFilePath.Create())
            {
                using (var batchWriter = new BinaryWriter(batchStream))
                {
                    var installProcedure = new StringBuilder();
                    installProcedure.AppendLine(string.Format("SET InstallerFilePath=\"{0}\"", installerFilePath));
                    installProcedure.AppendLine(string.Format("SET UiFilePath=\"{0}\"", uiFilePath));
                    installProcedure.AppendLine(string.Format(""));
                    installProcedure.AppendLine(string.Format("msiexec /package %InstallerFilePath% /passive"));
                    installProcedure.AppendLine(string.Format("TIMEOUT /T 1 >NUL"));
                    installProcedure.AppendLine(string.Format("START /b cmd /c %UiFilePath%"));
                    installProcedure.AppendLine(string.Format("TIMEOUT /T 1 >NUL"));
                    installProcedure.AppendLine(string.Format(""));
                    installProcedure.AppendLine(string.Format("DEL \"%~f0\"")); // Deletes itself at the end of process

                    batchWriter.Write(Encoding.GetEncoding(850).GetBytes(installProcedure.ToString()));
                    batchWriter.Flush();
                    batchWriter.Close();
                }
            }

            Process.Start(new ProcessStartInfo(batchFilePath.FullName)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            })
            .Dispose();

            Application.Current.Shutdown();
        }

        public static void TraverseSettings(JToken settings, Action<JValue> action)
        {
            switch (settings.Type)
            {
                case JTokenType.Object:
                    foreach (JProperty childSettings in settings.Children<JProperty>())
                        TraverseSettings(childSettings.Value, action);
                    break;

                case JTokenType.Array:
                    foreach (JToken childSettings in settings.Children())
                        TraverseSettings(childSettings, action);
                    break;

                default:
                    action(settings as JValue);
                    break;
            }
        }

        public static void UpdateSettings(JToken oldSettings, JToken newSettings)
        {
            if (oldSettings == null || newSettings == null) return;

            TraverseSettings(oldSettings, oldSetting =>
            {
                if (!(newSettings.SelectToken(oldSetting.Path) is JValue newSetting)) return;
                try
                {
                    switch (oldSetting.Type)
                    {
                        case JTokenType.String:
                            newSetting.Value = oldSetting.ToString();
                            break;

                        case JTokenType.Boolean:
                            newSetting.Value = oldSetting.ToObject<bool>();
                            break;

                        case JTokenType.Integer:
                            newSetting.Value = oldSetting.ToObject<long>();
                            break;

                        case JTokenType.Float:
                            newSetting.Value = oldSetting.ToObject<decimal>();
                            break;
                    }
                }
                catch { }
            });
        }

        public static void NormalizeSettingsValue(JValue setting, JToken savedSettings)
        {
            JTokenType settingType = JTokenType.None;
            try
            {
                settingType = savedSettings.SelectToken(setting.Path).Type;

                switch (settingType)
                {
                    case JTokenType.String:
                        break; // Do nothing

                    case JTokenType.Boolean:
                        setting.Value = setting.ToObject<bool>();
                        break;

                    case JTokenType.Integer:
                        setting.Value = setting.ToObject<long>();
                        break;

                    case JTokenType.Float:
                        setting.Value = setting.ToObject<decimal>();
                        break;
                }
            }
            catch (Exception)
            {
                throw new Exception(string.Format("Failed to parse '{0}' into {1} at '{2}'", setting.Value, settingType, setting.Path));
            }
        }

        public static void ShowMessageBox(string title, string message, params string[] messageArgs)
        {
            if (Application.Current.MainWindow.Dispatcher.CheckAccess())
                MessageBox.Show(Application.Current.MainWindow, string.Format(message, messageArgs), title);
            else
                Application.Current.MainWindow.Invoke(() => ShowMessageBox(title, message, messageArgs));
        }
    }
}