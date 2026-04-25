using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BIMPills.UI.Resources
{
    /// <summary>
    /// Renders a BIM Pills icon by slug. PNGs are embedded as Resource in BIMPills.UI.
    /// Usage: <bimca:Icon Slug="audit" Size="16"/>
    /// </summary>
    public class Icon : Image
    {
        private static readonly Dictionary<string, BitmapImage> _cache =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        public static readonly DependencyProperty SlugProperty =
            DependencyProperty.Register(nameof(Slug), typeof(string), typeof(Icon),
                new PropertyMetadata(null, OnSlugChanged));

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(double), typeof(Icon),
                new PropertyMetadata(16.0, OnSizeChanged));

        public string? Slug
        {
            get => (string?)GetValue(SlugProperty);
            set => SetValue(SlugProperty, value);
        }

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public Icon()
        {
            Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            ApplySize();
        }

        private static void OnSlugChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((Icon)d).Reload();

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((Icon)d).ApplySize();

        private void ApplySize()
        {
            Width  = Size;
            Height = Size;
        }

        private void Reload()
        {
            if (string.IsNullOrWhiteSpace(Slug))
            {
                Source = null;
                return;
            }

            try
            {
                Source = LoadFromResources(Slug!);
            }
            catch
            {
                Source = null;
            }
        }

        private static BitmapImage LoadFromResources(string slug)
        {
            if (_cache.TryGetValue(slug, out var cached))
                return cached;

            var uri = new Uri($"pack://application:,,,/BIMPills.UI;component/Resources/Icons/{slug}.png", UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource = uri;
            bmp.EndInit();
            bmp.Freeze();

            _cache[slug] = bmp;
            return bmp;
        }
    }
}
