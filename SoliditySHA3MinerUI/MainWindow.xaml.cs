using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SoliditySHA3MinerUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        #region Declaration & Properties

        public API.MinerProcessor MinerProcessor { get; }

        private readonly VisualBrush _networkConnectedBrush;
        private readonly VisualBrush _networkDisconnectedBrush;

        private AppTheme _currentTheme;
        private Accent _currentAccent;

        private Timer _checkUiTimer;
        private Timer _checkMinerVersionTimer;
        private Timer _checkConnectionTimer;

        private JToken _savedSettings;
        private FileSystemWatcher _settingFileWatcher;
        private IMinerInstance _minerInstance;

        private bool _isConnected;
        private bool _isLastConnected;
        private bool _isPreLaunchChanged;
        private bool _isSettingsChanged;
        private bool _isClosing;
        private bool _isAPIReceived;
        private string _minerDownloadURL;
        private string _uiDownloadURL;

        private string CurrentTheme
        {
            get => _currentTheme?.Name ?? (Properties.Settings.Default.DarkTheme ? "BaseDark" : "BaseLight");
            set
            {
                _currentTheme = ThemeManager.GetAppTheme(value);
                ThemeManager.ChangeAppStyle(this, _currentAccent ?? ThemeManager.GetAccent("Steel"), _currentTheme);
            }
        }

        private string CurrentDevice
        {
            get
            {
                var accent = _currentAccent?.Name ?? "Steel";
                switch (accent)
                {
                    case "Green":
                        return "NVIDIA";

                    case "Red":
                        return "AMD";

                    case "Blue":
                        return "INTEL";

                    default:
                        return "Unknown";
                }
            }
            set
            {
                switch (value)
                {
                    case "NVIDIA":
                        _currentAccent = ThemeManager.GetAccent("Green");
                        break;

                    case "AMD":
                        _currentAccent = ThemeManager.GetAccent("Red");
                        break;

                    case "INTEL":
                        _currentAccent = ThemeManager.GetAccent("Blue");
                        break;

                    case "Idle":
                        _currentAccent = ThemeManager.GetAccent("Steel");
                        break;

                    default:
                        _currentAccent = ThemeManager.GetAccent("Orange");
                        break;
                }
                ThemeManager.ChangeAppStyle(this, _currentAccent, _currentTheme ?? ThemeManager.GetAppTheme("BaseDark"));
            }
        }

        #endregion Declaration & Properties

        #region Initialization

        public MainWindow()
        {
            Application.Current.MainWindow = this;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.InvariantCulture.Name);

            CheckUserConfigFile();

            MinerProcessor = new API.MinerProcessor(this);
            MinerProcessor.OnResponse += MinerProcessor_OnResponse;
            MinerProcessor.OnRequestSettings += MinerProcessor_OnRequestSettings;

            _networkConnectedBrush = new VisualBrush(new PackIconModern { Kind = PackIconModernKind.Network });
            _networkDisconnectedBrush = new VisualBrush(new PackIconModern { Kind = PackIconModernKind.NetworkDisconnect });

            DataContext = this;
            InitializeComponent();

            var scaleSize = (Properties.Settings.Default.ScaleSize < 0.5 ? 0.5 : Properties.Settings.Default.ScaleSize);
            viewMain.Width = Width * scaleSize;
            MinWidth *= scaleSize;
            MinHeight *= scaleSize;
            Width *= scaleSize;
            Height *= scaleSize;

            CurrentTheme = Properties.Settings.Default.DarkTheme ? "BaseDark" : "BaseLight";
            CurrentDevice = "Idle";

            if (Properties.Settings.Default.MaximixeOnApplicationLaunch)
                WindowState = WindowState.Maximized;

            StateChanged += MainWindow_StateChanged;

            IsEnabled = false;
        }

        private void InitializeProcess()
        {
            Task.Factory.StartNew(() =>
            {
                CheckUIVersion(false, null, string.Empty);
                CheckMinerVersion(false, null, string.Empty);

                _checkConnectionTimer_Elapsed(this, null);
                _checkMinerVersionTimer_Elapsed(this, null);

                if (!Helper.FileSystem.MinerDirectory.Exists)
                    Helper.FileSystem.MinerDirectory.Create();

                InitializeSettingFileWatcher();

                _checkConnectionTimer = new Timer(Properties.Settings.Default.CheckConnectionInterval * 1000) { AutoReset = true };
                _checkConnectionTimer.Elapsed += _checkConnectionTimer_Elapsed;
                _checkConnectionTimer.Start();

                _checkMinerVersionTimer = new Timer(Properties.Settings.Default.CheckVersionInterval * 1000) { AutoReset = true };
                _checkMinerVersionTimer.Elapsed += _checkMinerVersionTimer_Elapsed;
                _checkMinerVersionTimer.Start();

                Task.Factory.StartNew(() =>
                {
                    Task.Delay(10000);
                    _checkUiTimer_Elapsed(this, null);

                    _checkUiTimer = new Timer(Properties.Settings.Default.CheckVersionInterval * 1000) { AutoReset = true };
                    _checkUiTimer.Elapsed += _checkUiTimer_Elapsed;
                    _checkUiTimer.Start();
                });

                this.BeginInvoke(() => IsEnabled = true);
            });
        }

        private void InitializeSettingFileWatcher()
        {
            if (_settingFileWatcher != null)
            {
                _settingFileWatcher.Created -= _settingFileWatcher_Created;
                _settingFileWatcher.Changed -= _settingFileWatcher_Changed;
                _settingFileWatcher.EnableRaisingEvents = false;
                _settingFileWatcher.Dispose();
            }

            if (Helper.FileSystem.MinerSettingsPath.Exists) PopulateSettings(Helper.FileSystem.MinerSettingsPath.FullName);

            _settingFileWatcher = new FileSystemWatcher(Helper.FileSystem.MinerDirectory.FullName)
            {
                Filter = Helper.FileSystem.MinerSettingsPath.Name,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _settingFileWatcher.Created += _settingFileWatcher_Created;
            _settingFileWatcher.Changed += _settingFileWatcher_Changed;
        }

        #endregion Initialization

        #region Control Events

        private async void SoliditySHA3Miner_Loaded(object sender, RoutedEventArgs e)
        {
            var controller = await this.ShowProgressAsync("Please wait...", "Initializing");
            controller.SetIndeterminate();

            InitializeProcess();
            do
            {
                await Task.Yield();
                await Task.Delay(200);
            } while (!IsEnabled);

            await controller.CloseAsync();

            if (Properties.Settings.Default.AutoLaunch)
                this.BeginInvoke(() => tswLaunch.IsChecked = true);
        }

        private void SoliditySHA3Miner_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if ((bool)tswLaunch.IsChecked)
            {
                Task.Factory.StartNew(() =>
                {
                    this.BeginInvoke(() => tswLaunch.IsChecked = false);
                });
                _isClosing = true;
                e.Cancel = true;
                return;
            }

            if (foSettings.IsOpen && _isSettingsChanged)
            {
                Task.Factory.StartNew(() =>
                {
                    this.BeginInvoke(() => foSettings.IsOpen = false);
                });
                _isClosing = true;
                e.Cancel = true;
                return;
            }

            if (foPreLaunchCmd.IsOpen && _isPreLaunchChanged)
            {
                Task.Factory.StartNew(() =>
                {
                    this.BeginInvoke(() => foPreLaunchCmd.IsOpen = false);
                });
                _isClosing = true;
                e.Cancel = true;
                return;
            }

            MinerProcessor.Dispose();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.MaximixeOnApplicationLaunch = (WindowState == WindowState.Maximized);
            Properties.Settings.Default.Save();
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow(_currentTheme, _currentAccent) { Owner = this }.Show();
        }

        private void btnSettings_Button_Click(object sender, RoutedEventArgs e)
        {
            foLogs.IsOpen = false;
            foPreLaunchCmd.IsOpen = false;
            foSettings.IsOpen = true;
        }

        private void btnPreLaunchCmd_Click(object sender, RoutedEventArgs e)
        {
            foLogs.IsOpen = false;
            foSettings.IsOpen = false;
            foPreLaunchCmd.IsOpen = true;
        }

        private void btnLogs_Click(object sender, RoutedEventArgs e)
        {
            foPreLaunchCmd.IsOpen = false;
            foSettings.IsOpen = false;
            foLogs.IsOpen = true;
        }

        private void btnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            if (_minerInstance != null)
                _minerInstance?.ClearLogs();
            else
                rtbLogs.Document.Blocks.Clear();
        }

        private void retDotnetCoreVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (retDotnetCoreVersion.Fill != Brushes.Red) return;
            System.Diagnostics.Process.Start("https://aka.ms/dotnet-download");
        }

        private async void retMinerVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_minerDownloadURL)) return;

            await StopMiner();

            await SaveSettings();

            DownloadLatestMiner();
        }

        private async void retUIVersion_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_uiDownloadURL)) return;

            await StopMiner();

            await SaveSettings();

            DownloadLatestUI();
        }

        private void txtPreLaunchCmd_TextChanged(object sender, TextChangedEventArgs e)
        {
            _isPreLaunchChanged = true;
        }

        private void txtSettingValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized || trvSettings.ItemsSource == null) return;

            ((e.Source as TextBox).DataContext as JValue).Value = (e.Source as TextBox).Text;
            _isSettingsChanged = true;
        }

        private void tswDarkTheme_IsCheckedChanged(object sender, EventArgs e)
        {
            if (!IsInitialized) return;
            _isSettingsChanged = true;
        }

        private void tswAutoLaunch_IsCheckedChanged(object sender, EventArgs e)
        {
            if (!IsInitialized) return;
            _isSettingsChanged = true;
        }

        private void numScaleSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double?> e)
        {
            if (!IsInitialized) return;
            _isSettingsChanged = true;
        }

        private async void tswLaunch_IsCheckedChanged(object sender, EventArgs e)
        {
            if ((bool)tswLaunch.IsChecked)
                await LaunchMiner();
            else
                await StopMiner();

            if (_minerInstance?.IsRunning ?? false)
            {
                if (Properties.Settings.Default.AllowAnimation)
                {
                    var animation = new BrushAnimation
                    {
                        From = tswLaunch.OffSwitchBrush,
                        To = tswLaunch.OnSwitchBrush,
                        Duration = TimeSpan.FromSeconds(1.25),
                        RepeatBehavior = RepeatBehavior.Forever,
                        AutoReverse = true
                    };
                    retLaunch.BeginAnimation(Shape.FillProperty, animation);
                }
                else { retLaunch.Fill = tswLaunch.OnSwitchBrush; }

                retLaunch.ToolTip = "Launched";
                tswLaunch.IsChecked = true;
            }
            else
            {
                if (Properties.Settings.Default.AllowAnimation)
                {
                    var animation = new BrushAnimation
                    {
                        To = tswLaunch.OffSwitchBrush,
                        Duration = TimeSpan.FromSeconds(1)
                    };
                    retLaunch.BeginAnimation(Shape.FillProperty, animation);
                }
                else { retLaunch.Fill = tswLaunch.OffSwitchBrush; }

                retLaunch.ToolTip = "Waiting for Launch";
                tswLaunch.IsChecked = false;
            }
        }

        private void dgdDashBoard_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!(e.Row.Item is API.Dashboard item)) return;

            switch (item.Brand)
            {
                case "NVIDIA":
                    e.Row.Foreground = Brushes.LimeGreen;
                    break;

                case "AMD":
                    e.Row.Foreground = Brushes.Red;
                    break;

                case "INTEL":
                    e.Row.Foreground = Brushes.SkyBlue;
                    break;

                default:
                    e.Row.Foreground = Brushes.Orange;
                    break;
            }
        }

        private async void foSettings_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            foSettings.Width = Width - (brdSummary.Width * 1.4);

            if (foSettings.IsOpen)
                InitializeSettingFileWatcher();
            else
                await SaveSettings();
        }

        private void foLogs_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            foLogs.Width = Width - (brdSummary.Width * 1.4);
        }

        private async void foPreLaunchCmd_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            foPreLaunchCmd.Width = Width - (brdSummary.Width * 1.4);

            if (foPreLaunchCmd.IsOpen || !_isPreLaunchChanged) return;

            MessageDialogResult userResponse = await this.ShowMessageAsync("Pre-launch script changed",
                                                                           "Press OK to save.",
                                                                           style: MessageDialogStyle.AffirmativeAndNegative);
            if (userResponse == MessageDialogResult.Affirmative)
                Properties.Settings.Default.Save();
            else
                Properties.Settings.Default.Reload();

            _isPreLaunchChanged = false;
        }

        #endregion Control Events

        #region Object Events

        private void MinerProcessor_OnResponse(API.MinerReport minerReport)
        {
            if (Dispatcher.CheckAccess())
            {
                if (!_isAPIReceived)
                {
                    _isAPIReceived = true;

                    if (minerReport.DashboardList.All(d => d.Brand == "NVIDIA"))
                        CurrentDevice = "NVIDIA";
                    else if (minerReport.DashboardList.All(d => d.Brand == "AMD"))
                        CurrentDevice = "AMD";
                    else if (minerReport.DashboardList.All(d => d.Brand == "INTEL"))
                        CurrentDevice = "INTEL";
                    else
                        CurrentDevice = "Unknown";
                }
            }
            else
            {
                this.BeginInvoke(() => MinerProcessor_OnResponse(minerReport));
            }
        }

        private void _minerInstance_OnLogUpdated(string updatedLog, string newLog, int removedLogIndex)
        {
            if (Dispatcher.CheckAccess())
            {
                if (string.IsNullOrEmpty(updatedLog))
                    rtbLogs.Document.Blocks.Clear();

                if (removedLogIndex > 0)
                    rtbLogs.Document.Blocks.Remove(rtbLogs.Document.Blocks.FirstBlock);

                var newParagraph = new Paragraph();
                newParagraph.Inlines.Add(newLog);
                newParagraph.Foreground = (newLog.IndexOf("[ERROR]") > -1)
                                        ? Brushes.Red
                                        : (newLog.IndexOf("[WARN]") > -1)
                                        ? Brushes.Yellow
                                        : (Brush)FindResource(SystemColors.ControlTextBrushKey);

                rtbLogs.Document.Blocks.Add(newParagraph);
                if ((bool)tswLogAutoScroll.IsChecked)
                {
                    rtbLogs.CaretPosition = rtbLogs.Document.ContentEnd;
                    rtbLogs.ScrollToEnd();
                }
            }
            else
            {
                this.BeginInvoke(() => _minerInstance_OnLogUpdated(updatedLog, newLog, removedLogIndex));
            }
        }

        private void _minerInstance_Exited(object sender, EventArgs e)
        {
            try
            {
                _minerInstance.Exited -= _minerInstance_Exited;
                _minerInstance.OnLogUpdated += _minerInstance_OnLogUpdated;

                Helper.Processor.GetAllRelatedProcessList().ForEach(p => p.Kill()); // Kill all stray processes

                CurrentDevice = "Idle";

                if (_isClosing) this.BeginInvoke(() => Application.Current.Shutdown());
            }
            catch { }
            finally { _minerInstance = null; }
        }

        private void _checkConnectionTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _isConnected = Helper.Network.IsNetworkConnected();
            UpdateNetworkConnection(_isConnected);

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
        }

        private void _checkMinerVersionTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_isConnected)
            {
                var updateLatestMinerInfo = (Helper.Network.GetLatestMinerInfo(out Version latestMinerVersion, out string latestMinerDownloadUrl));
                CheckMinerVersion(updateLatestMinerInfo, latestMinerVersion, latestMinerDownloadUrl);
            }
            else { CheckMinerVersion(false, null, string.Empty); }
        }

        private void _checkUiTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_isConnected)
            {
                var updateLatestMinerInfo = (Helper.Network.GetLatestUiInfo(out Version latestUiVersion, out string latestUiDownloadUrl));
                CheckUIVersion(updateLatestMinerInfo, latestUiVersion, latestUiDownloadUrl);
            }
            else { CheckUIVersion(false, null, string.Empty); }
        }

        private void _settingFileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            PopulateSettings(e.FullPath);
        }

        private void _settingFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            PopulateSettings(e.FullPath);
        }

        private void MinerProcessor_OnRequestSettings(ref JToken settings)
        {
            settings = _savedSettings;
        }

        #endregion Object Events

        #region Checking Processes

        private void UpdateNetworkConnection(bool isNetworkConnected)
        {
            if (Dispatcher.CheckAccess())
            {
                if (isNetworkConnected)
                {
                    retConnection.OpacityMask = _networkConnectedBrush;
                    retConnection.Fill = (Brush)FindResource("AccentColorBrush");
                    retConnection.ToolTip = "Connected";

                    if (!_isLastConnected) _checkMinerVersionTimer_Elapsed(this, null);
                }
                else
                {
                    retConnection.OpacityMask = _networkDisconnectedBrush;
                    retConnection.Fill = (Brush)FindResource("GrayNormalBrush");
                    retConnection.ToolTip = "Disconnected";
                }
                _isLastConnected = isNetworkConnected;
            }
            else
            {
                try { this.Invoke(() => UpdateNetworkConnection(isNetworkConnected)); }
                catch (TaskCanceledException) { }
            }
        }

        private void CheckMinerVersion(bool isUpdateLatestMinerInfo, Version latestMinerVersion, string latestMinerDownloadUrl)
        {
            if (Dispatcher.CheckAccess())
            {
                var localMinerFound = Helper.Processor.GetLocalMinerVersion(out Version localMinerVersion);

                txbMinerVersion.Text = localMinerVersion.ToString();

                retDotnetCoreVersion.Fill = Helper.Processor.CheckDotnetCoreVersion(out Version dotnetCoreVersion)
                                          ? (Brush)FindResource("GrayNormalBrush")
                                          : Brushes.Red;
                retDotnetCoreVersion.ToolTip = (retDotnetCoreVersion.Fill == Brushes.Red)
                                             ? "Invalid version, click here to go to Dotnet Core download webpage"
                                             : "Version sufficient to launch miner";
                txbDotnetCoreVersion.Text = dotnetCoreVersion.ToString();

                if (isUpdateLatestMinerInfo)
                {
                    if (localMinerVersion >= latestMinerVersion)
                    {
                        _minerDownloadURL = string.Empty;
                        retMinerVersion.ToolTip = "You are using the latest version";
                        retMinerVersion.Fill = (Brush)FindResource("GrayNormalBrush");
                    }
                    else
                    {
                        _minerDownloadURL = latestMinerDownloadUrl;
                        retMinerVersion.ToolTip = "New release available, click here to download";
                        retMinerVersion.Fill = Brushes.Yellow;
                    }
                }
                else if (!localMinerFound)
                {
                    _minerDownloadURL = string.Empty;
                    retMinerVersion.ToolTip = "Miner not found";
                    retMinerVersion.Fill = Brushes.Red;
                }
            }
            else
            {
                try { this.Invoke(() => CheckMinerVersion(isUpdateLatestMinerInfo, latestMinerVersion, latestMinerDownloadUrl)); }
                catch (TaskCanceledException) { }
            }
        }

        private void CheckUIVersion(bool isUpdateLatestUiInfo, Version latestUiVersion, string latestUiDownloadUrl)
        {
            if (Dispatcher.CheckAccess())
            {
                var localUiVersion = Helper.Processor.GetUIVersionCompat;
                txbUIVersion.Text = localUiVersion.ToString();

                if (isUpdateLatestUiInfo)
                {
                    if (localUiVersion >= latestUiVersion)
                    {
                        _uiDownloadURL = string.Empty;
                        retUIVersion.ToolTip = "Your are using the latest UI version";
                        retUIVersion.Fill = (Brush)FindResource("GrayNormalBrush");
                    }
                    else
                    {
                        _uiDownloadURL = latestUiDownloadUrl;
                        retUIVersion.ToolTip = "New release available, click here to download";
                        retUIVersion.Fill = Brushes.Yellow;
                    }
                }
            }
            else
            {
                try { this.Invoke(() => CheckUIVersion(isUpdateLatestUiInfo, latestUiVersion, latestUiDownloadUrl)); }
                catch (TaskCanceledException) { }
            }
        }

        private async Task<string> PreLaunchChecks()
        {
            if (!_isConnected)
            {
                if (await this.ShowMessageAsync("Network disconnected", "Press OK to continue anyway",
                    style: MessageDialogStyle.AffirmativeAndNegative) != MessageDialogResult.Affirmative)
                    return string.Empty;
            }
            if (!Helper.Processor.GetLocalMinerVersion(out Version localMinerVersion))
            {
                if (await this.ShowMessageAsync("Miner not found", "Press OK to continue anyway",
                    style: MessageDialogStyle.AffirmativeAndNegative) != MessageDialogResult.Affirmative)
                    return string.Empty;
            }
            if (retDotnetCoreVersion.Fill == Brushes.Red)
            {
                if (await this.ShowMessageAsync("Invalid dotnet core version", "Press OK to continue anyway",
                    style: MessageDialogStyle.AffirmativeAndNegative) != MessageDialogResult.Affirmative)
                    return string.Empty;
            }
            if (!Helper.Processor.GetApiUri(_savedSettings["minerJsonAPI"].ToString(),
                out string apiUriPath, out string apiErrorMessage))
            {
                Helper.Processor.ShowMessageBox("Invalid 'minerJsonAPI' parameter", apiErrorMessage);
                return string.Empty;
            }
            return apiUriPath;
        }

        #endregion Checking Processes

        #region User Action Processes

        private async void DownloadLatestMiner()
        {
            ProgressDialogController controller = null;
            try
            {
                var fileName = System.IO.Path.GetFileName(new Uri(_minerDownloadURL).LocalPath);
                var filePath = new FileInfo(System.IO.Path.Combine(Helper.FileSystem.DownloadDirectory.FullName, fileName));

                if (!filePath.Directory.Exists) filePath.Directory.Create();
                if (filePath.Exists) filePath.Delete(); filePath.Refresh();

                controller = await this.ShowProgressAsync("Please wait...", "Downloading miner", isCancelable: true);
                controller.SetIndeterminate();
                controller.Maximum = 100d;

                var downloader = Helper.Network.DownloadFromURL(_minerDownloadURL, filePath.FullName,
                    new DownloadProgressChangedEventHandler((s, progressEvent) =>
                    {
                        this.BeginInvoke(() =>
                        {
                            var percentage = (double)progressEvent.BytesReceived / progressEvent.TotalBytesToReceive * 100;
                            controller.SetProgress(percentage);
                        });
                    }),
                    new System.ComponentModel.AsyncCompletedEventHandler((s, completedEvent) =>
                    {
                        this.BeginInvoke(() =>
                        {
                            if (completedEvent.Error != null)
                            {
                                if (!(completedEvent.Error is WebException) || ((WebException)completedEvent.Error).Status != WebExceptionStatus.RequestCanceled)
                                    Helper.Processor.ShowMessageBox("Error downloading miner", completedEvent.Error.Message);
                            }
                            else
                            {
                                filePath.Refresh();

                                if (filePath.Exists)
                                    UpdateMiner(filePath.FullName);
                                else
                                    Helper.Processor.ShowMessageBox("Error downloading miner", "Downloaded archive missing");
                            }
                            if (controller.IsOpen) controller.CloseAsync();
                        });
                    }));

                controller.Canceled += (s, cancelEvent) => downloader.CancelAsync();
                controller.Closed += (s, closeEvent) => downloader.Dispose();
            }
            catch (Exception ex)
            {
                Helper.Processor.ShowMessageBox("Error downloading miner", ex.Message);
            }
        }

        private async void DownloadLatestUI()
        {
            ProgressDialogController controller = null;
            try
            {
                var fileName = System.IO.Path.GetFileName(new Uri(_uiDownloadURL).LocalPath);
                var filePath = new FileInfo(System.IO.Path.Combine(Helper.FileSystem.DownloadDirectory.FullName, fileName));

                if (!filePath.Directory.Exists) filePath.Directory.Create();
                if (filePath.Exists) filePath.Delete(); filePath.Refresh();

                controller = await this.ShowProgressAsync("Please wait...", "Downloading GUI", isCancelable: true);
                controller.SetIndeterminate();
                controller.Maximum = 100d;

                var downloader = Helper.Network.DownloadFromURL(_uiDownloadURL, filePath.FullName,
                    new DownloadProgressChangedEventHandler((s, progressEvent) =>
                    {
                        this.BeginInvoke(() =>
                        {
                            var percentage = (double)progressEvent.BytesReceived / progressEvent.TotalBytesToReceive * 100;
                            controller.SetProgress(percentage);
                        });
                    }),
                    new System.ComponentModel.AsyncCompletedEventHandler((s, completedEvent) =>
                    {
                        this.BeginInvoke(() =>
                        {
                            if (completedEvent.Error != null)
                            {
                                if (!(completedEvent.Error is WebException) || ((WebException)completedEvent.Error).Status != WebExceptionStatus.RequestCanceled)
                                    Helper.Processor.ShowMessageBox("Error downloading GUI", completedEvent.Error.Message);
                            }
                            else
                            {
                                filePath.Refresh();

                                if (filePath.Exists)
                                    UpdateUI(filePath.FullName);
                                else
                                    Helper.Processor.ShowMessageBox("Error downloading GUI", "Downloaded archive missing");
                            }
                            controller.CloseAsync();
                        });
                    }));

                controller.Canceled += (s, cancelEvent) => downloader.CancelAsync();
                controller.Closed += (s, closeEvent) => downloader.Dispose();
            }
            catch (Exception ex)
            {
                Helper.Processor.ShowMessageBox("Error downloading GUI", ex.Message);
            }
            finally { if (controller != null && controller.IsOpen) await controller.CloseAsync(); }
        }

        private void UpdateMiner(string archiveFilePath)
        {
            var oldSettings = _savedSettings?.DeepClone();

            if (Helper.FileSystem.UnzipMinerArchrive(archiveFilePath, Helper.FileSystem.MinerDirectory.FullName))
            {
                _savedSettings = Helper.FileSystem.DeserializeFromFile(Helper.FileSystem.MinerSettingsPath.FullName);

                UpdateSettings(oldSettings, _savedSettings);

                InitializeSettingFileWatcher();

                _checkMinerVersionTimer_Elapsed(this, null);
            }
            else { Helper.Processor.ShowMessageBox("Error updating miner", "Downloaded archive does not contain update"); }

            InitializeSettingFileWatcher();
        }

        private void UpdateUI(string installerFilePath)
        {
            System.Diagnostics.Process.Start(installerFilePath);
            Application.Current.Shutdown();
        }

        private async Task LaunchMiner()
        {
            ProgressDialogController controller = null;
            try
            {
                var apiUriPath = await PreLaunchChecks();
                if (string.IsNullOrWhiteSpace(apiUriPath))
                {
                    tswLaunch.IsChecked = false;
                    return;
                }
                controller = await this.ShowProgressAsync("Please wait...", "Starting miner");
                controller.SetIndeterminate();
                await Task.Yield();
                await Task.Delay(200);

                rtbLogs.Document.Blocks.Clear();

                _minerInstance = new MinerInstance(Properties.Settings.Default.PreLaunchScript);
                _minerInstance.Exited += _minerInstance_Exited;
                _minerInstance.OnLogUpdated += _minerInstance_OnLogUpdated;

                if (!_minerInstance.Start())
                {
                    tswLaunch.IsChecked = false;
                    return;
                }
                MinerProcessor.URI = apiUriPath;
                MinerProcessor.Interval = 5000;
                _isAPIReceived = false;

                MinerProcessor.Start();
            }
            catch (Exception ex)
            {
                Helper.Processor.ShowMessageBox("Error launching miner", ex.Message);
            }
            finally
            {
                var waitSeconds = 0;
                do // Wait for API to send data
                {
                    await Task.Delay(1000);
                    waitSeconds++;
                }
                while ((bool)tswLaunch.IsChecked && !_isAPIReceived && waitSeconds < 45);

                if (controller != null && controller.IsOpen) await controller.CloseAsync();

                // Stop miner if no API available after 30 seconds
                if (!_isAPIReceived) await StopMiner();
            }
        }

        private async Task StopMiner()
        {
            if (_minerInstance == null) return;

            ProgressDialogController controller = null;
            try
            {
                controller = await this.ShowProgressAsync("Please wait...", "Stopping miner");
                controller.SetIndeterminate();
                await Task.Yield();
                await Task.Delay(200);

                MinerProcessor.Stop();
                _minerInstance.Stop();
                await Task.Delay(1000);

                tswLaunch.IsChecked = false;
            }
            catch (Exception ex)
            {
                Helper.Processor.ShowMessageBox("Error stopping miner", ex.Message);
            }
            finally
            {
                if (controller != null && controller.IsOpen) await controller.CloseAsync();
            }
        }

        #endregion User Action Processes

        #region Settings

        private void CheckUserConfigFile()
        {
            var configFile = Helper.FileSystem.LocalAppDirectory.
                                               Parent.
                                               GetFiles("user.config", SearchOption.AllDirectories).
                                               FirstOrDefault();

            if (configFile != null && new Version(configFile.Directory.Name) < Helper.Processor.GetUIVersion)
            {
                try
                {
                    var currentPath = new DirectoryInfo(System.IO.Path.Combine(configFile.Directory.Parent.FullName,
                                                                               Helper.Processor.GetUIVersion.ToString()));
                    configFile.Directory.MoveTo(currentPath.FullName);

                    Properties.Settings.Default.Reload();
                }
                catch { }
            }
        }

        private async Task SaveSettings()
        {
            if (_isSettingsChanged)
            {
                MessageDialogResult userResponse = await this.ShowMessageAsync("Settings changed",
                                                                               "Press OK to save.",
                                                                               style: MessageDialogStyle.AffirmativeAndNegative);
                if (userResponse == MessageDialogResult.Affirmative)
                {
                    try
                    {
                        if (_minerInstance == null || !_minerInstance.IsRunning)
                            MinerProcessor.SetSummaryToPreMineState();

                        Properties.Settings.Default.Save();

                        TraverseSettings(trvSettings.ItemsSource as JToken, setting => NormalizeSettingsValue(setting));

                        if (Helper.FileSystem.SerializeToFile(trvSettings.ItemsSource, Helper.FileSystem.MinerSettingsPath.FullName))
                        {
                            if (_isClosing) this.BeginInvoke(() => Application.Current.Shutdown());

                            _checkConnectionTimer.Interval = Properties.Settings.Default.CheckConnectionInterval;
                            _checkMinerVersionTimer.Interval = Properties.Settings.Default.CheckVersionInterval;
                            _checkUiTimer.Interval = Properties.Settings.Default.CheckVersionInterval;
                        }
                        else
                        {
                            foSettings.IsOpen = true;
                            throw new Exception("Failed to save " + Helper.FileSystem.MinerSettingsPath.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.Processor.ShowMessageBox("Error saving settings.", ex.Message);
                    }
                }
                PopulateSettings(Helper.FileSystem.MinerSettingsPath.FullName);
            }
        }

        private void TraverseSettings(JToken settings, Action<JValue> action)
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

        private void UpdateSettings(JToken oldSettings, JToken newSettings)
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

        private void NormalizeSettingsValue(JValue settings)
        {
            JTokenType settingType = JTokenType.None;
            try
            {
                settingType = _savedSettings.SelectToken(settings.Path).Type;

                switch (settingType)
                {
                    case JTokenType.String:
                        break; // Do nothing

                    case JTokenType.Boolean:
                        settings.Value = settings.ToObject<bool>();
                        break;

                    case JTokenType.Integer:
                        settings.Value = settings.ToObject<long>();
                        break;

                    case JTokenType.Float:
                        settings.Value = settings.ToObject<decimal>();
                        break;
                }
            }
            catch (Exception)
            {
                throw new Exception(string.Format("Failed to parse '{0}' into {1} at '{2}'", settings.Value, settingType, settings.Path));
            }
        }

        private void PopulateSettings(string settingsPath)
        {
            if (Dispatcher.CheckAccess())
            {
                _savedSettings = Helper.FileSystem.DeserializeFromFile(Helper.FileSystem.MinerSettingsPath.FullName);

                MinerProcessor.SetSummaryToPreMineState();

                trvSettings.ItemsSource = null;
                trvSettings.Items.Clear();
                trvSettings.ItemsSource = _savedSettings.DeepClone();

                _isSettingsChanged = false;
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(delegate { PopulateSettings(settingsPath); }));
            }
        }

        #endregion Settings

    }
}