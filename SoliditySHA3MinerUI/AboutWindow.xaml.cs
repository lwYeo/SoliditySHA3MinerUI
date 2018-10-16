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

            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            var copyright = (assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), true) as AssemblyCopyrightAttribute[])[0].Copyright;
            txbDescription.Text = string.Format(txbDescription.Text, assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build, copyright);
        }
    }
}