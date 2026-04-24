using BIMPills.Core.LegendFromExcel;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BIMPills.UI.LegendFromExcel
{
    public partial class ExcelPreviewWindow : Window
    {
        private const double PxPerMm = 3.2;
        private const double MinCellW = 12.0;
        private const double MinCellH = 10.0;

        public ExcelPreviewWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
        }

        public void Render(ExcelTableModel table, string fileName, double cellWidthMm, double cellHeightMm)
        {
            FileNameText.Text = System.IO.Path.GetFileName(fileName);
            InfoText.Text     = $"{table.RowCount} filas × {table.ColumnCount} columnas  •  Vista previa del archivo Excel";

            PreviewCanvas.Children.Clear();

            double cellW = Math.Max(cellWidthMm  * PxPerMm, MinCellW);
            double cellH = Math.Max(cellHeightMm * PxPerMm, MinCellH);

            PreviewCanvas.Width  = table.ColumnCount * cellW;
            PreviewCanvas.Height = table.RowCount    * cellH;

            foreach (var cell in table.Cells)
            {
                double x = cell.Column * cellW;
                double y = cell.Row    * cellH;
                double w = cell.ColSpan * cellW;
                double h = cell.RowSpan * cellH;

                // Background: color exacto del Excel
                var bgColor = ParseHex(cell.BackgroundColorHex);
                var bg      = bgColor.HasValue
                    ? (Brush)new SolidColorBrush(bgColor.Value)
                    : Brushes.White;

                var rect = new Rectangle
                {
                    Width               = w,
                    Height              = h,
                    Fill                = bg,
                    Stroke              = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    StrokeThickness     = 0.6,
                    SnapsToDevicePixels = true,
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                PreviewCanvas.Children.Add(rect);

                // Texto
                if (!string.IsNullOrEmpty(cell.Text))
                {
                    // Texto oscuro o claro según el fondo
                    bool darkBg = IsDarkColor(bgColor);
                    var fg = darkBg ? Brushes.White : Brushes.Black;

                    var container = new Border
                    {
                        Width  = w,
                        Height = h,
                        Child  = new TextBlock
                        {
                            Text                = cell.Text,
                            FontSize            = 8,
                            Foreground          = fg,
                            TextWrapping        = TextWrapping.NoWrap,
                            TextTrimming        = TextTrimming.CharacterEllipsis,
                            VerticalAlignment   = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin              = new Thickness(4, 0, 4, 0),
                        }
                    };
                    Canvas.SetLeft(container, x);
                    Canvas.SetTop(container, y);
                    PreviewCanvas.Children.Add(container);
                }
            }

            Height = Math.Min(Math.Max(table.RowCount * cellH + 100, 160), 700);
        }

        private static Color? ParseHex(string? hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    if (r > 250 && g > 250 && b > 250) return null; // blanco → null
                    return Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return null;
        }

        private static bool IsDarkColor(Color? color)
        {
            if (!color.HasValue) return false;
            var c = color.Value;
            double luminance = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            return luminance < 128;
        }
    }
}
