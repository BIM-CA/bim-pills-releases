using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace BIMPills.UI.Shared
{
    /// <summary>
    /// Convierte bool a Visibility (true = Visible, false = Collapsed).
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    /// <summary>
    /// Convierte bool a Visibility inverso (true = Collapsed, false = Visible).
    /// </summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Convierte un conteo a color de insignia: 0 = verde (exito), mayor a 0 = rojo (acento).
    /// </summary>
    public sealed class CountToBadgeColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        private static readonly SolidColorBrush AccentBrush = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

        static CountToBadgeColorConverter()
        {
            SuccessBrush.Freeze();
            AccentBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count == 0 ? SuccessBrush : AccentBrush;

            return AccentBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
