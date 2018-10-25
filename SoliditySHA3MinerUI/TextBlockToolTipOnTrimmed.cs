using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SoliditySHA3MinerUI
{
    public class TextBlockToolTipOnTrimmed : TextBlock
    {
        public TextBlockToolTipOnTrimmed()
        {
            SetBinding(ToolTipProperty, new Binding("Text") { Source = this });
        }

        protected override void OnToolTipOpening(ToolTipEventArgs e)
        {
            if (TextTrimming != TextTrimming.None)
                e.Handled = !IsTextTrimmed();
        }

        private bool IsTextTrimmed()
        {
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var formattedText = new FormattedText(Text, CultureInfo.CurrentCulture, FlowDirection, typeface, FontSize, Foreground,
                                                  VisualTreeHelper.GetDpi(this).PixelsPerDip);
            return formattedText.Width > ActualWidth;
        }
    }
}
