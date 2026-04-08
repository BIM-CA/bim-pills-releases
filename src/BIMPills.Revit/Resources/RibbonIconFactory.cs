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

        // ══════════════════════════════════════════════════════════════════════
        // All icons use a consistent 24×24 optical area centered in 32×32.
        // Uniform stroke: 1.5 outline, 2.0 accent. macOS SF-Symbols aesthetic.
        // ══════════════════════════════════════════════════════════════════════

        public static BitmapSource CreateAuditIcon()
        {
            return RenderIcon(ctx =>
            {
                // Clipboard body — centered square 18×22
                var bodyPen = Stroke(Navy, 1.5);
                ctx.DrawRoundedRectangle(Brushes.White, bodyPen,
                    new Rect(7, 6, 18, 22), 2.5, 2.5);

                // Clipboard clip — orange
                var clipPen = Stroke(Orange, 1.5);
                var clipGeo = new StreamGeometry();
                using (var sgc = clipGeo.Open())
                {
                    sgc.BeginFigure(new Point(12.5, 6), false, false);
                    sgc.LineTo(new Point(12.5, 4.5), true, false);
                    sgc.ArcTo(new Point(19.5, 4.5), new Size(3.5, 3.5), 0, false, SweepDirection.Clockwise, true, false);
                    sgc.LineTo(new Point(19.5, 6), true, false);
                }
                ctx.DrawGeometry(null, clipPen, clipGeo);

                // Check — orange
                var checkPen = RoundStroke(Orange, 2.0);
                ctx.DrawLine(checkPen, new Point(11.5, 17), new Point(14.5, 20));
                ctx.DrawLine(checkPen, new Point(14.5, 20), new Point(21, 13.5));

                // Baseline — navy subtle
                var linePen = RoundStroke(Navy, 1.0);
                ctx.DrawLine(linePen, new Point(11.5, 24), new Point(20.5, 24));
            });
        }

        public static BitmapSource CreateAboutIcon()
        {
            return RenderIcon(ctx =>
            {
                // Circle — centered, r=11
                var pen = Stroke(Navy, 1.5);
                ctx.DrawEllipse(Brushes.White, pen, new Point(16, 16), 11, 11);

                // "i" dot — orange
                ctx.DrawEllipse(Brush(Orange), null, new Point(16, 10.5), 1.8, 1.8);

                // "i" body — orange
                var iPen = RoundStroke(Orange, 2.2);
                ctx.DrawLine(iPen, new Point(16, 14.5), new Point(16, 22));
            });
        }

        public static BitmapSource CreateExportIcon()
        {
            return RenderIcon(ctx =>
            {
                // Box/tray — navy, square proportions
                var pen = Stroke(Navy, 1.5);
                var trayGeo = new StreamGeometry();
                using (var sgc = trayGeo.Open())
                {
                    sgc.BeginFigure(new Point(5, 14), true, true);
                    sgc.LineTo(new Point(5, 26), true, false);
                    sgc.LineTo(new Point(27, 26), true, false);
                    sgc.LineTo(new Point(27, 14), true, false);
                    sgc.LineTo(new Point(22, 14), true, false);
                    sgc.LineTo(new Point(22, 18), true, false);
                    sgc.LineTo(new Point(10, 18), true, false);
                    sgc.LineTo(new Point(10, 14), true, false);
                }
                ctx.DrawGeometry(Brushes.White, pen, trayGeo);

                // Arrow up — orange (export/upload)
                var arrowPen = RoundStroke(Orange, 2.0);
                ctx.DrawLine(arrowPen, new Point(16, 15), new Point(16, 5));
                ctx.DrawLine(arrowPen, new Point(12, 9), new Point(16, 5));
                ctx.DrawLine(arrowPen, new Point(20, 9), new Point(16, 5));
            });
        }

        public static BitmapSource CreateDocumentacionIcon()
        {
            return RenderIcon(ctx =>
            {
                // Stacked pages — all same proportions (16×20), offset 2px
                var backPen  = Stroke(Color.FromArgb(50, 0x21, 0x2B, 0x37), 1.2);
                var midPen   = Stroke(Color.FromArgb(100, 0x21, 0x2B, 0x37), 1.2);
                var frontPen = Stroke(Navy, 1.5);

                // Page 3 (back)
                ctx.DrawRoundedRectangle(Brushes.White, backPen,
                    new Rect(5, 3, 16, 20), 1.5, 1.5);

                // Page 2 (mid)
                ctx.DrawRoundedRectangle(Brushes.White, midPen,
                    new Rect(8, 6, 16, 20), 1.5, 1.5);

                // Page 1 (front)
                ctx.DrawRoundedRectangle(Brushes.White, frontPen,
                    new Rect(11, 9, 16, 20), 1.5, 1.5);

                // Cajetín — orange accent at bottom-right of front page
                var cajetinFill = new SolidColorBrush(Color.FromArgb(40, 0xEF, 0x63, 0x37));
                ctx.DrawRoundedRectangle(cajetinFill, Stroke(Orange, 1.2),
                    new Rect(20, 23, 6, 5), 0.8, 0.8);
            });
        }

        public static BitmapSource CreateGestionIcon()
        {
            return RenderIcon(ctx =>
            {
                // 2×2 grid — uniform 10×10 cells, 2px gap, centered
                var pen = Stroke(Navy, 1.5);
                double s = 10, g = 2;
                double x0 = 16 - s - g / 2, y0 = 16 - s - g / 2;

                ctx.DrawRoundedRectangle(Brushes.White, pen,
                    new Rect(x0, y0, s, s), 2.5, 2.5);
                ctx.DrawRoundedRectangle(Brushes.White, pen,
                    new Rect(x0 + s + g, y0, s, s), 2.5, 2.5);
                ctx.DrawRoundedRectangle(Brushes.White, pen,
                    new Rect(x0, y0 + s + g, s, s), 2.5, 2.5);

                // Highlighted cell — orange
                var hlFill = new SolidColorBrush(Color.FromArgb(35, 0xEF, 0x63, 0x37));
                ctx.DrawRoundedRectangle(hlFill, Stroke(Orange, 1.5),
                    new Rect(x0 + s + g, y0 + s + g, s, s), 2.5, 2.5);
            });
        }

        public static BitmapSource CreateDimensionIcon()
        {
            return RenderIcon(ctx =>
            {
                // Vertical extension lines — navy, same height as other icons
                var dimPen = RoundStroke(Navy, 1.5);
                ctx.DrawLine(dimPen, new Point(7, 6), new Point(7, 26));
                ctx.DrawLine(dimPen, new Point(25, 6), new Point(25, 26));

                // Horizontal dim line — navy
                ctx.DrawLine(dimPen, new Point(7, 16), new Point(25, 16));

                // Arrowheads — navy
                ctx.DrawLine(dimPen, new Point(7, 16), new Point(10, 14));
                ctx.DrawLine(dimPen, new Point(7, 16), new Point(10, 18));
                ctx.DrawLine(dimPen, new Point(25, 16), new Point(22, 14));
                ctx.DrawLine(dimPen, new Point(25, 16), new Point(22, 18));

                // Value pill — orange
                var pillFill = new SolidColorBrush(Color.FromArgb(30, 0xEF, 0x63, 0x37));
                ctx.DrawRoundedRectangle(pillFill, Stroke(Orange, 1.2),
                    new Rect(11, 9, 10, 6), 2, 2);

                // Value line — orange
                var valPen = RoundStroke(Orange, 1.5);
                ctx.DrawLine(valPen, new Point(13.5, 12), new Point(18.5, 12));
            });
        }

        public static BitmapSource CreateConnectIcon()
        {
            return RenderIcon(ctx =>
            {
                // Two nodes — navy circles, diagonally placed
                var nodePen = Stroke(Navy, 1.5);
                ctx.DrawEllipse(Brushes.White, nodePen, new Point(10, 10), 5.5, 5.5);
                ctx.DrawEllipse(Brushes.White, nodePen, new Point(22, 22), 5.5, 5.5);

                // Dashed connection — navy
                var connPen = RoundStroke(Navy, 1.5);
                connPen.DashStyle = new DashStyle(new double[] { 3, 2 }, 0);
                ctx.DrawLine(connPen, new Point(14, 14), new Point(18, 18));

                // Center dots — orange accent
                ctx.DrawEllipse(Brush(Orange), null, new Point(10, 10), 2.5, 2.5);
                ctx.DrawEllipse(Brush(Orange), null, new Point(22, 22), 2.5, 2.5);
            });
        }

        public static BitmapSource CreateOrderingIcon()
        {
            return RenderIcon(ctx =>
            {
                var badgeBrush   = new SolidColorBrush(Orange);
                var linePen      = RoundStroke(Navy, 1.5);

                // Three orange numbers (small) + navy lines
                double[] cy     = { 9, 16, 23 };
                string[] digits = { "1", "2", "3" };
                double   cx     = 7.5;

                var boldTypeface = new Typeface(new FontFamily("Segoe UI"),
                                     FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

                for (int i = 0; i < 3; i++)
                {
                    var ft = new FormattedText(
                        digits[i],
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        boldTypeface,
                        6.5,          // smaller → no overlap
                        badgeBrush,
                        96.0);
                    ctx.DrawText(ft, new Point(cx - ft.Width / 2, cy[i] - ft.Height / 2));
                    ctx.DrawLine(linePen, new Point(14, cy[i]), new Point(28, cy[i]));
                }

            });
        }

        public static BitmapSource CreateDataManagerIcon()
        {
            return RenderIcon(ctx =>
            {
                // Table/grid — representing schedule data
                var pen = Stroke(Navy, 1.5);

                // Outer table border
                ctx.DrawRoundedRectangle(Brushes.White, pen,
                    new Rect(4, 5, 24, 22), 2.5, 2.5);

                // Header row separator
                ctx.DrawLine(Stroke(Navy, 1.2), new Point(4, 11), new Point(28, 11));

                // Column separators
                ctx.DrawLine(Stroke(Navy, 0.8), new Point(12, 5), new Point(12, 27));
                ctx.DrawLine(Stroke(Navy, 0.8), new Point(20, 5), new Point(20, 27));

                // Row separator
                ctx.DrawLine(Stroke(Color.FromArgb(80, 0x21, 0x2B, 0x37), 0.6),
                    new Point(4, 19), new Point(28, 19));

                // Arrow exchange — orange (export/import)
                var arrowPen = RoundStroke(Orange, 1.8);
                // Arrow right (export)
                ctx.DrawLine(arrowPen, new Point(13, 15), new Point(19, 15));
                ctx.DrawLine(arrowPen, new Point(17, 13.5), new Point(19, 15));
                ctx.DrawLine(arrowPen, new Point(17, 16.5), new Point(19, 15));

                // Arrow left (import)
                ctx.DrawLine(arrowPen, new Point(19, 23), new Point(13, 23));
                ctx.DrawLine(arrowPen, new Point(15, 21.5), new Point(13, 23));
                ctx.DrawLine(arrowPen, new Point(15, 24.5), new Point(13, 23));
            });
        }

        public static BitmapSource CreateTransferIcon()
        {
            return RenderIcon(ctx =>
            {
                var boxPen   = RoundStroke(Navy, 2.0);
                var arrowPen = RoundStroke(Orange, 2.2);

                // ── Open-top rounded box (x=5..27, y=11..28, r=4) ────────────
                // Gap in top center: x=13..19
                var geo = new StreamGeometry();
                using (var sgc = geo.Open())
                {
                    sgc.BeginFigure(new Point(19, 11), false, false);
                    sgc.LineTo(new Point(23, 11), true, false);
                    sgc.ArcTo(new Point(27, 15), new Size(4, 4), 0, false, SweepDirection.Clockwise, true, false);
                    sgc.LineTo(new Point(27, 24), true, false);
                    sgc.ArcTo(new Point(23, 28), new Size(4, 4), 0, false, SweepDirection.Clockwise, true, false);
                    sgc.LineTo(new Point(9, 28), true, false);
                    sgc.ArcTo(new Point(5, 24), new Size(4, 4), 0, false, SweepDirection.Clockwise, true, false);
                    sgc.LineTo(new Point(5, 15), true, false);
                    sgc.ArcTo(new Point(9, 11), new Size(4, 4), 0, false, SweepDirection.Clockwise, true, false);
                    sgc.LineTo(new Point(13, 11), true, false);
                }
                ctx.DrawGeometry(Brushes.White, boxPen, geo);

                // ── Arrow up — orange ─────────────────────────────────────────
                ctx.DrawLine(arrowPen, new Point(16, 3), new Point(16, 22));
                ctx.DrawLine(arrowPen, new Point(11, 8), new Point(16, 3));
                ctx.DrawLine(arrowPen, new Point(21, 8), new Point(16, 3));
            });
        }

        private static FormattedText CreateText(string text, Color color, double size)
        {
            return new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                size,
                new SolidColorBrush(color),
                96);
        }

        private static Pen RoundStroke(Color c, double w)
        {
            var p = new Pen(Brush(c), w);
            p.StartLineCap = PenLineCap.Round;
            p.EndLineCap = PenLineCap.Round;
            p.LineJoin = PenLineJoin.Round;
            return p;
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
