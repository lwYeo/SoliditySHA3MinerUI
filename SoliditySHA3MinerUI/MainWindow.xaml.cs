using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
        public IMinerInstance MinerInstance { get; private set; }

        private readonly VisualBrush _networkConnectedBrush;
        private readonly VisualBrush _networkDisconnectedBrush;

        private AppTheme _currentTheme;
        private Accent _currentAccent;

        private Timer _checkUiTimer;
        private Timer _checkMinerVersionTimer;
        private Timer _checkConnectionTimer;

        private JToken _savedSettings;
        private FileSystemWatcher _settingFileWatcher;

        private bool _isConnected;
        private bool _isLastConnected;
        private bool _isPreLaunchChanged;
        private bool _isSettingsChanged;
        private bool _isClosing;
        private bool _isAPIReceived;
        private string _minerDownloadURL;
        private string _uiInstallerDownloadURL;

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

        private bool _StickyTriggerLaunch;
        private bool StickyTriggerLaunch
        {
            get => _StickyTriggerLaunch;
            set
            {
                Task.Factory.StartNew(async () =>
                {
                    while (MinerInstance != null)
                        await Task.Delay(500);

                    await Task.Delay(1000);
                    _StickyTriggerLaunch = false;
                });
                _StickyTriggerLaunch = true;
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

            if (!Helper.FileSystem.LocalAppDirectory.Exists)
                Helper.FileSystem.LocalAppDirectory.Create();

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

                if (!SoliditySHA3MinerUI.MinerInstance.MinerDirectory.Exists)
                    SoliditySHA3MinerUI.MinerInstance.MinerDirectory.Create();

                InitializeSettingFileWatcher();

                _checkConnectionTimer = new Timer(Properties.Settings.Default.CheckConnectionInterval * 1000) { AutoReset = true };
                _checkConnectionTimer.Elapsed += _checkConnectionTimer_Elapsed;
                _checkConnectionTimer.Start();

                _checkMinerVersionTimer = new Timer(Properties.Settings.Default.CheckVersionInterval * 1000) { AutoReset = true };
                _checkMinerVersionTimer.Elapsed += _checkMinerVersionTimer_Elapsed;
                _checkMinerVersionTimer.Start();

                this.BeginInvoke(() => IsEnabled = true);

                _checkUiTimer_Elapsed(this, null);

                _checkUiTimer = new Timer(Properties.Settings.Default.CheckVersionInterval * 1000) { AutoReset = true };
                _checkUiTimer.Elapsed += _checkUiTimer_Elapsed;
                _checkUiTimer.Start();
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

            if (SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.Exists)
                PopulateSettings(SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.FullName);

            _settingFileWatcher = new FileSystemWatcher(SoliditySHA3MinerUI.MinerInstance.MinerDirectory.FullName)
            {
                Filter = SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.Name,
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
            controller.SetProgressBarForegroundBrush(Brushes.Orange);
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
                    this.BeginInvoke(() =>
                    {
                        StickyTriggerLaunch = true;
                        tswLaunch.IsChecked = false;
                    });
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

            Task.Factory.StartNew(() =>
            {
                this.BeginInvoke(() =>
                {
                    var brdPoolNetWork_X = brdPoolNetwork.TransformToAncestor(this).Transform(new Point(0, 0)).X;

                    foLogs.Width = ActualWidth - brdPoolNetWork_X;
                    foSettings.Width = ActualWidth - brdPoolNetWork_X;
                    foPreLaunchCmd.Width = ActualWidth - brdPoolNetWork_X;
                });
            });
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
            if (MinerInstance != null)
                MinerInstance?.ClearLogs();
            else
                rtbLogs.Document.Blocks.Clear();

            rtbErrorLogs.Document.Blocks.Clear();
            rtbTransactions.Document.Blocks.Clear();
        }

        private async void btnResetSettings_OnClick(object sender, RoutedEventArgs e)
        {
            await ResetAllSettings();
        }

        private void btnAdvancedConfiguration_Click(object sender, RoutedEventArgs e)
        {
            Helper.FileSystem.LaunchCommand("explorer", SoliditySHA3MinerUI.MinerInstance.MinerDirectory.FullName);
        }

        private void retDotnetCoreVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (retDotnetCoreVersion.Fill != Brushes.Red) return;

            Helper.FileSystem.LaunchCommand("https://www.microsoft.com/net/download/thank-you/dotnet-runtime-2.1.5-windows-x64-installer");
        }

        private async void retMinerVersion_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_minerDownloadURL)) return;

            StickyTriggerLaunch = true;

            await StopMiner();

            await SaveSettings();

            await Helper.Processor.DownloadLatestMiner(this, _minerDownloadURL, DownloadMinerCompletedHandler);
        }

        private async void retUIVersion_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_uiInstallerDownloadURL)) return;

            StickyTriggerLaunch = true;

            await StopMiner();

            await SaveSettings();

            await Helper.Processor.DownloadLatestUiInstaller(this, _uiInstallerDownloadURL, DownloadUiInstallerCompletedHandler);
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
            {
                if (StickyTriggerLaunch)
                    await LaunchMiner(false);
                else
                    await LaunchMiner(true);
            }
            else { await StopMiner(); }
              

            if (MinerInstance?.IsRunning ?? false)
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
            foSettings.Width = ActualWidth - brdPoolNetwork.TransformToAncestor(this).Transform(new Point(0, 0)).X;

            if (foSettings.IsOpen)
                InitializeSettingFileWatcher();
            else
                await SaveSettings();
        }

        private void foLogs_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            foLogs.Width = ActualWidth - brdPoolNetwork.TransformToAncestor(this).Transform(new Point(0, 0)).X;
        }

        private async void foPreLaunchCmd_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            foPreLaunchCmd.Width = ActualWidth - brdPoolNetwork.TransformToAncestor(this).Transform(new Point(0, 0)).X;

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

        private void tswLaunch_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StickyTriggerLaunch = true;
        }

        #endregion Control Events

        #region Event Handlers

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

                if (newLog.StartsWith("[") || newLog.StartsWith("***") || rtbLogs.Document.Blocks.Count < 7)
                {
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

                    if (new Brush[] { Brushes.Red, Brushes.Yellow }.Contains(newParagraph.Foreground))
                    {
                        var newErrorParagraph = new Paragraph();
                        newErrorParagraph.Inlines.Add(newLog);
                        newErrorParagraph.Foreground = newParagraph.Foreground;

                        rtbErrorLogs.Document.Blocks.Add(newErrorParagraph);

                        if ((bool)tswLogAutoScroll.IsChecked)
                        {
                            rtbErrorLogs.CaretPosition = rtbErrorLogs.Document.ContentEnd;
                            rtbErrorLogs.ScrollToEnd();
                        }
                    }

                    var txKeywords = new string[] { "transaction", "submit", "transfer", "reward" };
                    if (txKeywords.Any(k => newLog.IndexOf(k, StringComparison.OrdinalIgnoreCase) > -1))
                    {
                        var newTxParagraph = new Paragraph();
                        newTxParagraph.Inlines.Add(newLog);
                        newTxParagraph.Foreground = (newLog.IndexOf("fail", StringComparison.OrdinalIgnoreCase) > -1)
                                                  ? Brushes.Red
                                                  : newParagraph.Foreground;

                        rtbTransactions.Document.Blocks.Add(newTxParagraph);

                        if ((bool)tswLogAutoScroll.IsChecked)
                        {
                            rtbTransactions.CaretPosition = rtbTransactions.Document.ContentEnd;
                            rtbTransactions.ScrollToEnd();
                        }
                    }
                }
                else
                {
                    var lastParagraph = rtbLogs.Document.Blocks.LastBlock as Paragraph;
                    lastParagraph.Inlines.Add(Environment.NewLine);
                    lastParagraph.Inlines.Add(newLog);

                    if (new Brush[] { Brushes.Red, Brushes.Yellow }.Contains(lastParagraph.Foreground))
                    {
                        var lastErrorParagraph = rtbErrorLogs.Document.Blocks.LastBlock as Paragraph;
                        lastErrorParagraph.Inlines.Add(Environment.NewLine);
                        lastErrorParagraph.Inlines.Add(newLog);
                    }
                }
            }
            else
            {
                this.BeginInvoke(() => _minerInstance_OnLogUpdated(updatedLog, newLog, removedLogIndex));
            }
        }

        private void _minerInstance_Exited(object sender, EventArgs e)
        {
            var relaunchMiner = !StickyTriggerLaunch;
            try
            {
                MinerInstance.Exited -= _minerInstance_Exited;
                MinerInstance.OnLogUpdated += _minerInstance_OnLogUpdated;

                Helper.Processor.GetAllRelatedProcessList().ForEach(p => p.Kill()); // Kill all stray processes

                CurrentDevice = "Idle";

                if (_isClosing) this.Invoke(() => Application.Current.Shutdown());
            }
            catch { }
            finally
            {
                MinerInstance = null;
                this.BeginInvoke(() => tswLaunch.IsChecked = false);

                if (relaunchMiner)
                    Task.Factory.StartNew(() => RelaunchMiner());
            }
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

        private void DownloadMinerCompletedHandler(bool isSuccess, FileInfo archiveFilePath)
        {
            if (!isSuccess)
            {
                Helper.Processor.ShowMessageBox("Error updating miner", "Downloaded archive does not contain update");
                return;
            }
            else if (!archiveFilePath.Exists)
            {
                Helper.Processor.ShowMessageBox("Error downloading miner", "Downloaded archive missing");
                return;
            }
            else
            {
                var oldSettings = _savedSettings?.DeepClone();

                if (Helper.FileSystem.UnzipMinerArchrive(archiveFilePath.FullName, SoliditySHA3MinerUI.MinerInstance.MinerDirectory.FullName))
                {
                    _savedSettings = Helper.FileSystem.DeserializeFromFile(SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.FullName);

                    Helper.Processor.UpdateSettings(oldSettings, _savedSettings);

                    Helper.FileSystem.SerializeToFile(_savedSettings, SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.FullName);

                    InitializeSettingFileWatcher();

                    _checkMinerVersionTimer_Elapsed(this, null);
                }
            }
        }

        private void DownloadUiInstallerCompletedHandler(bool isSuccess, FileInfo installerFilePath)
        {
            if (isSuccess)
                Helper.Processor.StartUiInstallerAndExit(installerFilePath);
        }

        #endregion Events Handlers

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
                        _uiInstallerDownloadURL = string.Empty;
                        retUIVersion.ToolTip = "Your are using the latest GUI version";
                        retUIVersion.Fill = (Brush)FindResource("GrayNormalBrush");
                    }
                    else
                    {
                        _uiInstallerDownloadURL = latestUiDownloadUrl;
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

        #region Launch Process
        
        private async Task LaunchMiner(bool keepLogs)
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
                controller.SetProgressBarForegroundBrush(Brushes.Orange);
                controller.SetIndeterminate();
                await Task.Yield();
                await Task.Delay(200);
                
                if (!keepLogs)
                    rtbLogs.Document.Blocks.Clear();

                _checkConnectionTimer_Elapsed(this, null);

                MinerInstance = new MinerInstance(Properties.Settings.Default.PreLaunchScript, (uint)(rtbLogs.Document.Blocks.Count()));
                MinerInstance.Exited += _minerInstance_Exited;
                MinerInstance.OnLogUpdated += _minerInstance_OnLogUpdated;
                MinerInstance.WatchDogInterval = Properties.Settings.Default.StatusInterval;

                if (!MinerInstance.Start())
                {
                    tswLaunch.IsChecked = false;
                    return;
                }
                MinerProcessor.URI = apiUriPath;
                MinerProcessor.Interval = Properties.Settings.Default.StatusInterval;
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
            if (MinerInstance == null) return;

            ProgressDialogController controller = null;
            try
            {
                controller = await this.ShowProgressAsync("Please wait...", "Stopping miner");
                controller.SetProgressBarForegroundBrush(Brushes.Orange);
                controller.SetIndeterminate();
                await Task.Yield();
                await Task.Delay(200);

                MinerProcessor.Stop();
                MinerInstance.Stop();
                await Task.Delay(1000);

                tswLaunch.IsChecked = false;

                _checkConnectionTimer_Elapsed(this, null);
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

        private async Task RelaunchMiner()
        {
            if (Dispatcher.CheckAccess())
            {
                var isCancelled = false;
                try
                {
                    var controller = await this.ShowProgressAsync("Please wait...", "Restarting miner after cooldown");
                    controller.SetProgressBarForegroundBrush(Brushes.Orange);
                    controller.Maximum = Properties.Settings.Default.RelaunchAfterCooldown;
                    controller.SetCancelable(true);
                    controller.Canceled += (s, e) => isCancelled = true;

                    for (var i = 1; i <= Properties.Settings.Default.RelaunchAfterCooldown; i++)
                    {
                        if (isCancelled) break;

                        await Task.Delay(1000);
                        controller.SetProgress(i);
                    }
                    await controller.CloseAsync();

                    if (isCancelled) return;

                    this.BeginInvoke(() => tswLaunch.IsChecked = true);
                }
                catch { }
            }
            else { this.BeginInvoke(async () => await RelaunchMiner()); }
        }

        #endregion Launch Process

        #region Settings

        private async Task ResetAllSettings()
        {
            MessageDialogResult userResponse = await this.ShowMessageAsync("Reset all settings",
                                                                           "Press OK to reset all settings.",
                                                                           style: MessageDialogStyle.AffirmativeAndNegative);
            if (userResponse != MessageDialogResult.Affirmative) return;

            await StopMiner();

            Properties.Settings.Default.Reset();

            if (SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.Exists)
                SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.Delete();

            if (SoliditySHA3MinerUI.MinerInstance.MinerPath.Exists)
            {
                var process = Helper.FileSystem.LaunchCommand("dotnet", SoliditySHA3MinerUI.MinerInstance.MinerPath.FullName, createNoWindow:true);
                try
                {
                    for (var i = 0; i < 10; i++)
                    {
                        await Task.Delay(1000);
                        if (process.HasExited) return;
                    }

                    if (!process.HasExited)
                        Helper.Processor.GetAllRelatedProcessList().ForEach(p => p.Kill()); // Kill all stray processes
                }
                catch { }

                PopulateSettings(SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.FullName);
            }
        }

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
                        if (MinerInstance == null || !MinerInstance.IsRunning)
                            MinerProcessor.SetSummaryToPreMineState();

                        Properties.Settings.Default.Save();

                        if (trvSettings.ItemsSource == null) return;

                        Helper.Processor.TraverseSettings(trvSettings.ItemsSource as JToken, setting =>
                        {
                            Helper.Processor.NormalizeSettingsValue(setting, _savedSettings);
                        });

                        if (Helper.FileSystem.SerializeToFile(trvSettings.ItemsSource, SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.FullName))
                        {
                            if (_isClosing) (this).Invoke(() => Application.Current.Shutdown());

                            _checkConnectionTimer.Interval = Properties.Settings.Default.CheckConnectionInterval;
                            _checkMinerVersionTimer.Interval = Properties.Settings.Default.CheckVersionInterval;
                            _checkUiTimer.Interval = Properties.Settings.Default.CheckVersionInterval;

                            MinerProcessor.Interval = Properties.Settings.Default.StatusInterval;
                            if (MinerInstance != null)
                                MinerInstance.WatchDogInterval = Properties.Settings.Default.StatusInterval;

                            if ((bool)tswLaunch.IsChecked)
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
                                else
                                {
                                    retLaunch.BeginAnimation(Shape.FillProperty, null);
                                    retLaunch.Fill = tswLaunch.OnSwitchBrush;
                                }
                            }
                        }
                        else
                        {
                            foSettings.IsOpen = true;
                            throw new Exception("Failed to save " + SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.Processor.ShowMessageBox("Error saving settings.", ex.Message);
                    }
                    finally
                    {
                        PopulateSettings(SoliditySHA3MinerUI.MinerInstance.MinerSettingsPath.FullName);
                    }
                }
            }
        }

        private void PopulateSettings(string settingsPath)
        {
            if (Dispatcher.CheckAccess())
            {
                _savedSettings = Helper.FileSystem.DeserializeFromFile(settingsPath);

                trvSettings.ItemsSource = null;
                trvSettings.Items.Clear();

                if (_savedSettings != null)
                    trvSettings.ItemsSource = _savedSettings.DeepClone();

                _isSettingsChanged = false;

                if (!(bool)tswLaunch.IsChecked)
                    MinerProcessor.SetSummaryToPreMineState();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(delegate { PopulateSettings(settingsPath); }));
            }
        }

        #endregion Settings

    }
}