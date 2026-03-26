using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BIMPills.Revit.Resources
{
    /// <summary>
    /// Generates 32x32 BitmapSource icons for Revit ribbon buttons.
    /// Uses BIM-CA brand colors: Azul #212B37, Naranja #EF6337, Amarillo #FECA29.
    /// </summary>
    internal static class RibbonIconFactory
    {
        // BIM-CA brand palette (oficial)
        private static readonly Color Navy   = Color.FromRgb(0x21, 0x2B, 0x37);  // Azul #212B37
        private static readonly Color Orange = Color.FromRgb(0xEF, 0x63, 0x37);  // Naranja #EF6337
        private static readonly Color Yellow = Color.FromRgb(0xFE, 0xCA, 0x29);  // Amarillo #FECA29

        private static SolidColorBrush Brush(Color c) => new SolidColorBrush(c);
        private static Pen Stroke(Color c, double w) => new Pen(Brush(c), w);

        public static BitmapSource CreateAuditIcon()
        {
            return RenderIcon(ctx =>
            {
                // Document — navy outline, white fill
                var docPen = Stroke(Navy, 1.6);
                ctx.DrawRoundedRectangle(Brushes.White, docPen,
                    new Rect(4, 2, 17, 24), 2, 2);

                // Checklist lines — navy
                var linePen = Stroke(Navy, 1.2);
                ctx.DrawLine(linePen, new Point(8, 9),  new Point(17, 9));
                ctx.DrawLine(linePen, new Point(8, 14), new Point(17, 14));
                ctx.DrawLine(linePen, new Point(8, 19), new Point(14, 19));

                // Check marks — coral accent
                var checkPen = Stroke(Orange, 1.4);
                checkPen.StartLineCap = PenLineCap.Round;
                checkPen.EndLineCap = PenLineCap.Round;
                // Check 1
                ctx.DrawLine(checkPen, new Point(8, 9),  new Point(9.5, 10.5));
                ctx.DrawLine(checkPen, new Point(9.5, 10.5), new Point(12, 7.5));
                // Check 2
                ctx.DrawLine(checkPen, new Point(8, 14), new Point(9.5, 15.5));
                ctx.DrawLine(checkPen, new Point(9.5, 15.5), new Point(12, 12.5));

                // Magnifying glass — coral circle + handle
                var glassFill = new SolidColorBrush(Color.FromArgb(30, 0xEF, 0x63, 0x37));
                var glassPen = Stroke(Orange, 2.0);
                ctx.DrawEllipse(glassFill, glassPen, new Point(23, 21), 6, 6);
                var handlePen = Stroke(Orange, 2.4);
                handlePen.StartLineCap = PenLineCap.Round;
                handlePen.EndLineCap = PenLineCap.Round;
                ctx.DrawLine(handlePen, new Point(27, 25), new Point(30, 28));
            });
        }

        public static BitmapSource CreateAboutIcon()
        {
            return RenderIcon(ctx =>
            {
                // Circle — navy fill (brand primary)
                ctx.DrawEllipse(Brush(Navy), null, new Point(16, 16), 13, 13);

                // Inner ring — coral accent subtle
                var ringPen = Stroke(Orange, 1.2);
                ctx.DrawEllipse(null, ringPen, new Point(16, 16), 10.5, 10.5);

                // White "i" — dot
                ctx.DrawEllipse(Brushes.White, null, new Point(16, 9.5), 1.8, 1.8);

                // White "i" — body
                var iPen = new Pen(Brushes.White, 3.2);
                iPen.StartLineCap = PenLineCap.Round;
                iPen.EndLineCap = PenLineCap.Round;
                ctx.DrawLine(iPen, new Point(16, 14.5), new Point(16, 23));
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
