# BIM Pills · Icon Set v1.0

A 51-icon set designed for the **BIM Pills** Revit add-in, drawn to the brand's
"Plástica" reference (orange `#EF6337` accents on navy `#212B37` line-art, on
a 32-pixel keyline grid).

This package replaces both:

1. The PNG ribbon icons that currently live next to the add-in DLL.
2. The Segoe MDL2 / Fluent UI glyphs the WPF UI fell back on for tool headers,
   tabs, action buttons, and status indicators.

---

## What's in this folder

```
icons/
├── manifest.json          ← machine-readable index (51 entries)
├── BimPillsIcons.cs       ← drop-in C# helper for WPF / Revit Ribbon
├── README.md              ← this file
├── svg/                   ← 51 master SVGs (32×32 viewBox, editable)
└── png/
    ├── 32/                ← 51 PNGs, 32×32  (Revit large button)
    ├── 64/                ← 51 PNGs, 64×64  (hi-DPI dialog)
    ├── 128/               ← 51 PNGs, 128×128
    └── 256/               ← 51 PNGs, 256×256 (settings, splash)
```

The slug filename is the stable identifier (`audit`, `export`, `settings`…).
Use it everywhere — the `n` numbers in `manifest.json` are display order only.

---

## Brand spec

| Token        | Hex       | Use                                              |
|--------------|-----------|--------------------------------------------------|
| Primary      | `#EF6337` | Accent strokes & fills (one motif per icon)      |
| Ink          | `#212B37` | Line-art base (1.6px stroke @ 32px)              |
| Yellow       | `#FECA29` | Warning triangle, P-extractor tag                |
| Green        | `#1E8A4F` | OK / success state                               |
| Paper        | `#FFFFFF` | Card background, inner fill                      |

- **Grid:** 32×32 viewBox, 28-pixel keyline (2px breathing room).
- **Stroke:** 1.6px ink, 2.0px primary accent. `stroke-linecap` & `linejoin` round.
- **Rule:** every icon has exactly **one** primary-orange element to guide the eye.
- **Style:** flat outlines, no gradients, no inner shadows.

---

## Quick start (Revit / WPF)

1.  Copy this whole folder into your add-in project as `icons/`.
2.  Drop `BimPillsIcons.cs` somewhere in your code (or include it as a linked file).
3.  In your `*.csproj`, copy the icons next to the built DLL:

    ```xml
    <Target Name="CopyBimPillsIcons" AfterTargets="Build">
      <ItemGroup>
        <BPIcons Include="$(ProjectDir)icons\**\*.*" />
      </ItemGroup>
      <Copy SourceFiles="@(BPIcons)"
            DestinationFolder="$(OutDir)icons\%(RecursiveDir)"
            SkipUnchangedFiles="true" />
    </Target>
    ```

4.  Use it in `IExternalApplication.OnStartup`:

    ```csharp
    using BimPills.Icons;

    var panel = app.CreateRibbonPanel("BIM Pills", "Datos");
    var btn   = new PushButtonData(
        "BimPills_Audit", "Auditar",
        Assembly.GetExecutingAssembly().Location,
        "BimPills.Commands.AuditCommand");

    btn.LargeImage = BimPillsIcons.Large(BimPillsIcons.Slugs.Audit);   // 32×32
    btn.Image      = BimPillsIcons.Small(BimPillsIcons.Slugs.Audit);   // 16×16
    panel.AddItem(btn);
    ```

5.  In any WPF window:

    ```xml
    <Image Source="{Binding HeaderIcon}" Width="32" Height="32" />
    ```

    ```csharp
    HeaderIcon = BimPillsIcons.GetImage("upload-arrow", 32);
    ```

---

## Replacing the Segoe MDL2 fallbacks

The current WPF UI uses `<TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE8B7;" />`
in many places. The `manifest.json` file maps each old codepoint to its BIM-Pills
replacement so a search-and-replace is mechanical.

For example — the Excel-attachment tab in `CustomDimensionSchemes.xaml`:

```xml
<!-- BEFORE -->
<TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE8A9;" FontSize="14"/>

<!-- AFTER -->
<Image Source="{Binding Source={x:Static icons:BimPillsIcons.Cache},
                Converter={StaticResource SlugToIconConverter},
                ConverterParameter=attach-excel}"
       Width="14" Height="14" />
```

…or, in code-behind:

```csharp
attachIcon.Source = BimPillsIcons.GetImage(BimPillsIcons.Slugs.AttachExcel, 16);
```

Full mapping (Segoe codepoint → slug) is in `manifest.json` under each icon's
`segoeRef` field.

---

## Editing the icons

The SVGs in `/svg/` are the master files. Each one is hand-built on the 32px
grid and uses only `<path>`, `<rect>`, `<circle>`, `<text>`, and `<g>` — no
filters, no gradients, no clip-paths. Edit them in Figma, Illustrator, Inkscape,
or by hand.

After editing, re-rasterize at the four canonical sizes. From the
project's HTML preview (`BIM Pills Icons.html`) you can re-open the export step,
or use a one-liner with ImageMagick:

```bash
for s in 32 64 128 256; do
  for f in svg/*.svg; do
    name=$(basename "$f" .svg)
    magick -background none -density 384 "$f" -resize ${s}x${s} "png/$s/$name.png"
  done
done
```

Keep `manifest.json` in sync: add new entries with a unique `slug`, keep the
numbering contiguous, and update `BimPillsIcons.Slugs` in the C# helper.

---

## License & attribution

Icons designed for **BIM Pills · Lab → Plástica** (BIM-CA, 2026). Use within
the BIM Pills product family; do not redistribute as a standalone icon font.

Generated 2026-04-24 · 51 icons · v1.0.
