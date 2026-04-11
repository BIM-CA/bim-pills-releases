Add-Type -AssemblyName System.Drawing

$srcPng = "$PSScriptRoot\..\src\BIMPills.UI\Resources\pill-icon.png"
$outDir  = "$PSScriptRoot\assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$original = [System.Drawing.Image]::FromFile($srcPng)
$ratio    = $original.Width / $original.Height  # ~0.6213 (portrait)

# BIM-CA brand colors
$bimBlue  = [System.Drawing.Color]::FromArgb(21, 101, 192)    # #1565C0
$bimGray  = [System.Drawing.Color]::FromArgb(120, 120, 120)   # subtitle

# ─────────────────────────────────────────────────────────────────────────
# Draws $img fitted (aspect-ratio preserved) into a $dstW x $dstH box
function DrawFit($g, $img, $x, $y, $dstW, $dstH) {
    $srcR = $img.Width / $img.Height
    $dstR = $dstW / $dstH
    if ($srcR -gt $dstR) { $w = $dstW;  $h = [int]($dstW / $srcR) }
    else                  { $h = $dstH; $w = [int]($dstH * $srcR) }
    $ox = $x + [int](($dstW - $w) / 2)
    $oy = $y + [int](($dstH - $h) / 2)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.DrawImage($img, $ox, $oy, $w, $h)
}

# ── 1. ICO — white square bg + pill proportional ─────────────────────────
# Each size: white background, pill centered with 8% padding each side
$icoPath = "$outDir\bimpills.ico"
$sizes   = @(256, 48, 32, 16)
$blobs   = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2  = [System.Drawing.Graphics]::FromImage($bmp)
    $g2.Clear([System.Drawing.Color]::White)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $pad = [int]($s * 0.06)   # 6% padding on each side
    DrawFit $g2 $original $pad $pad ($s - 2*$pad) ($s - 2*$pad)
    $g2.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $blobs += , $ms.ToArray()
    $ms.Dispose()
}
$fs = [System.IO.File]::OpenWrite($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$dataOffset = [uint32](6 + $sizes.Count * 16)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $blobs[$i]
    if ($s -eq 256) { $dim = [byte]0 } else { $dim = [byte]$s }
    $bw.Write($dim); $bw.Write($dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$d.Length); $bw.Write($dataOffset)
    $dataOffset += $d.Length
}
foreach ($d in $blobs) { $bw.Write($d) }
$bw.Close(); $fs.Close()
Write-Host "ICO  -> $icoPath  ($([Math]::Round((Get-Item $icoPath).Length/1KB)) KB)"

# ── 2. Header BMP 150x57 — white bg, logo left, text right ──────────────
$hdr    = New-Object System.Drawing.Bitmap(150, 57)
$g      = [System.Drawing.Graphics]::FromImage($hdr)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::White)
$pillH  = 40
$pillW  = [int]($pillH * $ratio)
$pillX  = 8;  $pillY = [int]((57 - $pillH) / 2)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($original, $pillX, $pillY, $pillW, $pillH)
$txtX   = $pillX + $pillW + 8
$fnt1   = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$fnt2   = New-Object System.Drawing.Font("Segoe UI",  7)
$brBlack = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 30, 30))
$brGray  = New-Object System.Drawing.SolidBrush($bimGray)
$g.DrawString("BIM Pills", $fnt1, $brBlack, $txtX, 10)
$g.DrawString("by BIM-CA",  $fnt2, $brGray,  $txtX + 1, 33)
$fnt1.Dispose(); $fnt2.Dispose(); $brBlack.Dispose(); $brGray.Dispose(); $g.Dispose()
$hdrPath = "$outDir\header.bmp"
$hdr.Save($hdrPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$hdr.Dispose()
Write-Host "HDR  -> $hdrPath"

# ── 3. Welcome BMP 164x314 — white bg, logo centered, BIM-CA blue text ──
$wlc    = New-Object System.Drawing.Bitmap(164, 314)
$g      = [System.Drawing.Graphics]::FromImage($wlc)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::White)
$pillH2 = 145; $pillW2 = [int]($pillH2 * $ratio)
$pillX2 = [int]((164 - $pillW2) / 2); $pillY2 = 30
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($original, $pillX2, $pillY2, $pillW2, $pillH2)
$sf      = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$fnt3    = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$fnt4    = New-Object System.Drawing.Font("Segoe UI",  8)
$brBlack = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 30, 30))
$brGray2 = New-Object System.Drawing.SolidBrush($bimGray)
$g.DrawString("BIM Pills",  $fnt3, $brBlack, ([System.Drawing.RectangleF]::new(0, 192, 164, 28)), $sf)
$g.DrawString("by BIM-CA",  $fnt4, $brGray2, ([System.Drawing.RectangleF]::new(0, 222, 164, 18)), $sf)
$fnt3.Dispose(); $fnt4.Dispose(); $brBlack.Dispose(); $brGray2.Dispose(); $sf.Dispose(); $g.Dispose()
$wlcPath = "$outDir\welcome.bmp"
$wlc.Save($wlcPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$wlc.Dispose()
Write-Host "WLC  -> $wlcPath"

# Preview PNGs for inspection
$hdrI = [System.Drawing.Image]::FromFile($hdrPath)
$hdrI.Save("$outDir\header_preview.png", [System.Drawing.Imaging.ImageFormat]::Png); $hdrI.Dispose()
$wlcI = [System.Drawing.Image]::FromFile($wlcPath)
$wlcI.Save("$outDir\welcome_preview.png", [System.Drawing.Imaging.ImageFormat]::Png); $wlcI.Dispose()

# ICO 32x32 preview
$icoSrc = [System.Drawing.Icon]::ExtractAssociatedIcon($icoPath)
$icoBmp = $icoSrc.ToBitmap()
$icoBmp.Save("$outDir\icon_preview.png", [System.Drawing.Imaging.ImageFormat]::Png)
$icoBmp.Dispose()

$original.Dispose()
Write-Host "Done. Preview PNGs saved."
