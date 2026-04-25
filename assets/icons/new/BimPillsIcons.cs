// BimPillsIcons.cs
// ----------------------------------------------------------------------------
// BIM Pills · Icon Set v1.0
// Helper for loading the icon set into WPF / Revit Ribbon.
//
// Drop this file into your add-in project, copy the /icons/ folder next to
// your built DLL (or embed as Resources, see notes at bottom), and use:
//
//     var img = BimPillsIcons.GetImage("audit", 32);     // ImageSource for a Button
//     var lg  = BimPillsIcons.GetImage("audit", 32);     // 32x32 ribbon large button
//     var sm  = BimPillsIcons.GetImage("audit", 16);     // ribbon small button (auto-resampled)
//
// Slug list — see ICONS dictionary at the bottom of this file (51 icons).
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BimPills.Icons
{
    /// <summary>
    /// Loads BIM Pills icon assets (PNG) as WPF <see cref="ImageSource"/> values
    /// for use in Revit Ribbon buttons, dialogs, and other WPF UI.
    /// </summary>
    public static class BimPillsIcons
    {
        // ----- Brand colors (Lab → Plástica reference) ------------------------
        public static readonly Color Primary = (Color)ColorConverter.ConvertFromString("#EF6337");
        public static readonly Color Ink     = (Color)ColorConverter.ConvertFromString("#212B37");
        public static readonly Color Yellow  = (Color)ColorConverter.ConvertFromString("#FECA29");
        public static readonly Color Green   = (Color)ColorConverter.ConvertFromString("#1E8A4F");
        public static readonly Color Paper   = Colors.White;

        // ----- Icon root resolver ---------------------------------------------
        // By default we look for an "icons" folder next to the executing
        // assembly. Override by setting BimPillsIcons.IconRoot at startup.
        private static string _iconRoot;
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

        /// <summary>
        /// Returns a cached <see cref="BitmapImage"/> for the named icon.
        /// Picks the closest available raster size ≥ <paramref name="targetSize"/>
        /// and lets WPF downsample to fit (sharper than upscaling).
        /// </summary>
        /// <param name="slug">Icon slug, e.g. "audit", "export", "settings".</param>
        /// <param name="targetSize">Desired display size in DIPs (16, 32, 48...).</param>
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
                    " path='" + path + "'. " +
                    "Make sure the /icons/ folder is deployed next to the DLL or set BimPillsIcons.IconRoot.",
                    path);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;        // detach from disk handle
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();                                       // safe to share across threads

            _cache[key] = bmp;
            return bmp;
        }

        /// <summary>Convenience: 16×16 (Revit small button).</summary>
        public static BitmapImage Small(string slug)  => GetImage(slug, 16);

        /// <summary>Convenience: 32×32 (Revit large button).</summary>
        public static BitmapImage Large(string slug)  => GetImage(slug, 32);

        /// <summary>Convenience: 64×64 (hi-DPI dialog).</summary>
        public static BitmapImage XLarge(string slug) => GetImage(slug, 64);

        private static int PickSize(int targetSize)
        {
            foreach (int s in AvailableSizes)
                if (s >= targetSize) return s;
            return AvailableSizes[AvailableSizes.Length - 1]; // 256
        }

        // ----- Slug catalog ----------------------------------------------------
        // Keep this list in sync with manifest.json. Useful for IDE auto-complete:
        //
        //     BimPillsIcons.Slugs.Audit        // "audit"
        //     BimPillsIcons.Slugs.Settings     // "settings"
        //
        public static class Slugs
        {
            // Ribbon — Datos
            public const string Audit        = "audit";
            public const string Export       = "export";
            public const string Transfer     = "transfer";        // Importar
            public const string DataManager  = "datamanager";     // Gestionar
            public const string Dimension    = "dimension";       // Esquemas
            public const string Connect      = "connect";
            public const string Ordering     = "ordering";        // Numerador
            public const string Extractor    = "extractor";

            // Ribbon — Procesos
            public const string Documentacion = "documentacion";
            public const string Gestion       = "gestion";        // Estandarizar
            public const string Dibujar       = "dibujar";

            // Ribbon — Información
            public const string About        = "about";
            public const string Support      = "support";

            // UI — Marca
            public const string LogoBimPills = "logo-bim-pills";

            // UI — Tool headers
            public const string UploadArrow  = "upload-arrow";
            public const string Table        = "table";
            public const string Filter       = "filter";
            public const string PageRight    = "pageright";
            public const string BarChart     = "barchart";
            public const string NumberedList = "numberedlist";
            public const string Cube3D       = "3d-cube";

            // UI — Tabs
            public const string Ruler            = "ruler";
            public const string Sort             = "sort";
            public const string Sync             = "sync";
            public const string Pin              = "location-pin";
            public const string Person           = "person";
            public const string StatusCircleInfo = "statuscircleinfo";
            public const string AttachExcel      = "attach-excel";
            public const string Sheet            = "page-sheet";
            public const string Link             = "link-range";
            public const string Eye              = "view-eye";

            // UI — Action buttons
            public const string Accept     = "accept";
            public const string Delete     = "delete-x";
            public const string Backspace  = "backspace";
            public const string Add        = "add-new";
            public const string Trash      = "trash";
            public const string Refresh    = "refresh";
            public const string RotateSync = "rotate-sync";
            public const string Settings   = "settings";
            public const string OpenFolder = "open-folder";
            public const string Search     = "search-zoom";
            public const string Feedback   = "feedbackapp";
            public const string Handle     = "handle";
            public const string ArrowUp    = "arrow-up";
            public const string Play       = "play-expand";

            // UI — Status
            public const string CheckOk        = "check-ok";
            public const string Warning        = "warning";
            public const string ReportWarning  = "reportwarning";

            // UI — Scroll
            public const string ScrollUp     = "scroll-up";
            public const string ScrollDown   = "scroll-down";
            public const string ArrowsUpDown = "arrows-up-down";
        }
    }
}

/* ---------------------------------------------------------------------------
   USAGE — Revit Ribbon (IExternalApplication.OnStartup)
   ---------------------------------------------------------------------------

   using BimPills.Icons;

   public Result OnStartup(UIControlledApplication app)
   {
       string tab = "BIM Pills";
       app.CreateRibbonTab(tab);

       var datos = app.CreateRibbonPanel(tab, "Datos");

       var auditData = new PushButtonData(
           "BimPills_Audit", "Auditar",
           Assembly.GetExecutingAssembly().Location,
           "BimPills.Commands.AuditCommand")
       {
           LargeImage = BimPillsIcons.Large(BimPillsIcons.Slugs.Audit),    // 32x32
           Image      = BimPillsIcons.Small(BimPillsIcons.Slugs.Audit),    // 16x16
           ToolTip    = "Revisar el modelo contra el catálogo BIM Pills."
       };
       datos.AddItem(auditData);

       // ...repeat for Exportar, Importar, Gestionar, etc.

       return Result.Succeeded;
   }

   ---------------------------------------------------------------------------
   USAGE — WPF Window (XAML code-behind)
   ---------------------------------------------------------------------------

   <Image x:Name="HeaderIcon" Width="32" Height="32" />

   public MainWindow()
   {
       InitializeComponent();
       HeaderIcon.Source = BimPillsIcons.GetImage("audit", 32);
   }

   ---------------------------------------------------------------------------
   DEPLOYMENT
   ---------------------------------------------------------------------------
   The simplest approach: copy the /icons/ folder (svg + png subfolders) next
   to your built DLL. Mark each PNG as Content / Copy if newer in your csproj,
   or post-build copy:

       <Target Name="CopyIcons" AfterTargets="Build">
         <ItemGroup>
           <IconFiles Include="$(ProjectDir)icons\**\*.*" />
         </ItemGroup>
         <Copy SourceFiles="@(IconFiles)"
               DestinationFolder="$(OutDir)icons\%(RecursiveDir)" />
       </Target>

   For embedded resources, set BimPillsIcons.IconRoot at startup or rewrite
   GetImage() to use pack:// URIs:

       new Uri($"pack://application:,,,/MyAddin;component/icons/png/{size}/{slug}.png")
   --------------------------------------------------------------------------- */
