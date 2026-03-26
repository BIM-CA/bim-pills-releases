using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BIMPills.Revit.Resources
{
    /// <summary>
    /// Generates 32x32 BitmapSource icons for Revit ribbon buttons.
    /// Revit requires BitmapSource — XAML DrawingImage from ResourceDictionaries won't work.
    /// Replace these with professional PNG icons when available.
    /// </summary>
    internal static class RibbonIconFactory
    {
        public static BitmapSource CreateAuditIcon()
        {
            return RenderIcon(visual =>
            {
                // Document shape
                var docPen = new Pen(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)), 1.5);
                visual.DrawRoundedRectangle(Brushes.White, docPen,
                    new Rect(4, 2, 18, 24), 2, 2);

                // Checklist lines
                var lineBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
                var linePen = new Pen(lineBrush, 1.2);
                visual.DrawLine(linePen, new Point(8, 9), new Point(18, 9));
                visual.DrawLine(linePen, new Point(8, 14), new Point(18, 14));
                visual.DrawLine(linePen, new Point(8, 19), new Point(15, 19));

                // Magnifying glass
                var accentBrush = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                var glassPen = new Pen(accentBrush, 2.0);
                visual.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 0xE7, 0x4C, 0x3C)),
                    glassPen, new Point(22, 20), 6, 6);
                visual.DrawLine(new Pen(accentBrush, 2.5), new Point(26, 24), new Point(30, 28));
            });
        }

        public static BitmapSource CreateAboutIcon()
        {
            return RenderIcon(visual =>
            {
                // Blue circle
                var blueBrush = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));
                visual.DrawEllipse(blueBrush, null, new Point(16, 16), 14, 14);

                // White "i" letter
                var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var text = new FormattedText("i", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 20, Brushes.White,
                    VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
                visual.DrawText(text, new Point(12.5, 3));
            });
        }

        private static BitmapSource RenderIcon(Action<DrawingContext> draw)
        {
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                draw(ctx);
            }

            var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
