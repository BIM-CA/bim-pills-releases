// BIM Pills · Icon Set v1.0
// Loads PNG icons from /icons/png/{size}/{slug}.png next to the DLL.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BIMPills.Revit.Resources
{
    internal static class BimPillsIcons
    {
        public static readonly Color Primary = (Color)ColorConverter.ConvertFromString("#EF6337");
        public static readonly Color Ink     = (Color)ColorConverter.ConvertFromString("#212B37");
        public static readonly Color Yellow  = (Color)ColorConverter.ConvertFromString("#FECA29");
        public static readonly Color Green   = (Color)ColorConverter.ConvertFromString("#1E8A4F");
        public static readonly Color Paper   = Colors.White;

        private static string? _iconRoot;
        public static string IconRoot
        {
            get
            {
                if (_iconRoot != null) return _iconRoot;
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                _iconRoot = Path.Combine(dir, "icons");
                return _iconRoot;
            }
            set { _iconRoot = value; }
        }

        private static readonly int[] AvailableSizes = { 32, 64, 128, 256 };
        private static readonly Dictionary<string, BitmapImage> _cache =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        public static BitmapImage GetImage(string slug, int targetSize = 32)
        {
            if (string.IsNullOrWhiteSpace(slug))
                throw new ArgumentNullException(nameof(slug));

            int size = PickSize(targetSize);
            string key = slug + "@" + size;

            if (_cache.TryGetValue(key, out var cached))
                return cached;

            string path = Path.Combine(IconRoot, "png", size.ToString(), slug + ".png");
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "BIM Pills icon not found. Slug='" + slug + "' size=" + size +
                    " path='" + path + "'.",
                    path);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();

            _cache[key] = bmp;
            return bmp;
        }

        // Revit ribbon Large button (32 logical px). Revit no maneja >32 nativamente.
        public static BitmapImage Large(string slug)  => GetImage(slug, 32);

        // Revit ribbon Small button / tooltip (16 logical px).
        public static BitmapImage Small(string slug)  => GetImage(slug, 32);

        public static BitmapImage XLarge(string slug) => GetImage(slug, 128);

        private static int PickSize(int targetSize)
        {
            foreach (int s in AvailableSizes)
                if (s >= targetSize) return s;
            return AvailableSizes[AvailableSizes.Length - 1];
        }

        public static class Slugs
        {
            public const string Audit         = "audit";
            public const string Export        = "export";
            public const string Transfer      = "transfer";
            public const string DataManager   = "datamanager";
            public const string Dimension     = "dimension";
            public const string Connect       = "connect";
            public const string Ordering      = "ordering";
            public const string Extractor     = "extractor";
            public const string Documentacion = "documentacion";
            public const string Gestion       = "gestion";
            public const string Dibujar       = "dibujar";
            public const string About         = "about";
            public const string Support       = "support";
            public const string LogoBimPills  = "logo-bim-pills";
        }
    }
}
