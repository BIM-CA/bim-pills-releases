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
                // Clipboard body — navy outline, white fill
                var bodyPen = Stroke(Navy, 1.6);
                ctx.DrawRoundedRectangle(Brushes.White, bodyPen,
                    new Rect(6, 6, 20, 24), 2, 2);

                // Clipboard clip — orange accent
                var clipPen = Stroke(Orange, 1.6);
                var clipGeo = new StreamGeometry();
                using (var sgc = clipGeo.Open())
                {
                    sgc.BeginFigure(new Point(12, 6), false, false);
                    sgc.LineTo(new Point(12, 4), true, false);
                    sgc.ArcTo(new Point(20, 4), new Size(4, 4), 0, false, SweepDirection.Clockwise, true, false);
                    sgc.LineTo(new Point(20, 6), true, false);
                }
                ctx.DrawGeometry(null, clipPen, clipGeo);

                // Check mark — orange accent
                var checkPen = Stroke(Orange, 2.2);
                checkPen.StartLineCap = PenLineCap.Round;
                checkPen.EndLineCap = PenLineCap.Round;
                ctx.DrawLine(checkPen, new Point(11, 17), new Point(14, 20));
                ctx.DrawLine(checkPen, new Point(14, 20), new Point(21, 13));

                // Bottom line — navy subtle
                var linePen = Stroke(Navy, 1.0);
                linePen.StartLineCap = PenLineCap.Round;
                linePen.EndLineCap = PenLineCap.Round;
                ctx.DrawLine(linePen, new Point(11, 25), new Point(21, 25));
            });
        }

        public static BitmapSource CreateAboutIcon()
        {
            return RenderIcon(ctx =>
            {
                // Circle — navy outline, white fill
                var circlePen = Stroke(Navy, 1.6);
                ctx.DrawEllipse(Brushes.White, circlePen, new Point(16, 16), 12, 12);

                // "i" dot — orange accent
                ctx.DrawEllipse(Brush(Orange), null, new Point(16, 10), 1.8, 1.8);

                // "i" body — orange accent
                var iPen = Stroke(Orange, 2.4);
                iPen.StartLineCap = PenLineCap.Round;
                iPen.EndLineCap = PenLineCap.Round;
                ctx.DrawLine(iPen, new Point(16, 14.5), new Point(16, 22.5));
            });
        }

        public static BitmapSource CreateExportIcon()
        {
            return RenderIcon(ctx =>
            {
                // Folder — navy outline, white fill
                var folderPen = Stroke(Navy, 1.6);

                // Folder body
                var folderGeo = new StreamGeometry();
                using (var sgc = folderGeo.Open())
                {
                    sgc.BeginFigure(new Point(3, 10), true, true);
                    sgc.LineTo(new Point(3, 8), true, false);
                    sgc.LineTo(new Point(11, 8), true, false);
                    sgc.LineTo(new Point(13, 10), true, false);
                    sgc.LineTo(new Point(29, 10), true, false);
                    sgc.LineTo(new Point(29, 27), true, false);
                    sgc.LineTo(new Point(3, 27), true, false);
                }
                ctx.DrawGeometry(Brushes.White, folderPen, folderGeo);

                // Arrow down — orange accent (export/download)
                var arrowPen = Stroke(Orange, 2.2);
                arrowPen.StartLineCap = PenLineCap.Round;
                arrowPen.EndLineCap = PenLineCap.Round;

                // Vertical line
                ctx.DrawLine(arrowPen, new Point(16, 14), new Point(16, 22));
                // Arrow head
                ctx.DrawLine(arrowPen, new Point(12.5, 19), new Point(16, 22.5));
                ctx.DrawLine(arrowPen, new Point(19.5, 19), new Point(16, 22.5));
            });
        }

        public static BitmapSource CreateDocumentacionIcon()
        {
            return RenderIcon(ctx =>
            {
                // 3 planos apilados en cascada — los de atrás en gris tenue
                var backPen1 = Stroke(Color.FromArgb(30, 0x21, 0x2B, 0x37), 1.2);
                var backPen2 = Stroke(Color.FromArgb(60, 0x21, 0x2B, 0x37), 1.2);
                var frontPen = Stroke(Navy, 1.4);

                // Plano 3 (más atrás) — gris muy tenue
                ctx.DrawRoundedRectangle(Brushes.White, backPen1,
                    new Rect(2, 3, 22, 16), 1, 1);

                // Plano 2 (medio) — gris medio
                ctx.DrawRoundedRectangle(Brushes.White, backPen2,
                    new Rect(5, 6, 22, 16), 1, 1);

                // Plano 1 (frontal) — navy
                ctx.DrawRoundedRectangle(Brushes.White, frontPen,
                    new Rect(8, 9, 22, 16), 1, 1);

                // Cajetín naranja — esquina inferior derecha del plano frontal
                var cajetinFill = new SolidColorBrush(Color.FromArgb(40, 0xEF, 0x63, 0x37));
                var cajetinPen = Stroke(Orange, 1.4);
                ctx.DrawRoundedRectangle(cajetinFill, cajetinPen,
                    new Rect(22, 20, 7, 4), 0.5, 0.5);
            });
        }

        public static BitmapSource CreateGestionIcon()
        {
            return RenderIcon(ctx =>
            {
                var boxPen = Stroke(Navy, 1.4);

                // Four grid squares — navy outline, white fill
                ctx.DrawRoundedRectangle(Brushes.White, boxPen,
                    new Rect(3, 3, 11, 11), 2, 2);
                ctx.DrawRoundedRectangle(Brushes.White, boxPen,
                    new Rect(18, 3, 11, 11), 2, 2);
                ctx.DrawRoundedRectangle(Brushes.White, boxPen,
                    new Rect(3, 18, 11, 11), 2, 2);

                // Highlighted cell — orange accent with subtle fill
                var highlightFill = new SolidColorBrush(Color.FromArgb(30, 0xEF, 0x63, 0x37));
                var highlightPen = Stroke(Orange, 1.4);
                ctx.DrawRoundedRectangle(highlightFill, highlightPen,
                    new Rect(18, 18, 11, 11), 2, 2);
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
