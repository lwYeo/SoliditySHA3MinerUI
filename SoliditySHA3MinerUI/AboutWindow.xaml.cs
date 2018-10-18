using MahApps.Metro;
using MahApps.Metro.Controls;
using System.Reflection;

namespace SoliditySHA3MinerUI
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : MetroWindow
    {
        public AboutWindow(AppTheme theme, Accent accent)
        {
            InitializeComponent();
            ThemeManager.ChangeAppStyle(this, accent, theme);

            var scaleSize = (Properties.Settings.Default.ScaleSize < 0.5 ? 0.5 : Properties.Settings.Default.ScaleSize);
            Height *= scaleSize;
            Width *= scaleSize;

            var version = Helper.Processor.GetUIVersion;
            var copyright = Helper.Processor.GetCopyright;
            txbDescription.Text = string.Format(txbDescription.Text, version.Major, version.Minor, version.Build, copyright);
        }
    }
}