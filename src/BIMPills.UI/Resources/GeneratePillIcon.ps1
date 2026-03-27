Add-Type -AssemblyName System.Drawing

$Size = 128
$OutputPath = Join-Path $PSScriptRoot "pill-icon.png"

# Canvas más alto que ancho para la cápsula alargada
$bmpW = $Size
$bmpH = [int]($Size * 1.6)
$bmp = New-Object System.Drawing.Bitmap($bmpW, $bmpH)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'HighQuality'
$g.InterpolationMode = 'HighQualityBicubic'
$g.PixelOffsetMode = 'HighQuality'
$g.Clear([System.Drawing.Color]::Transparent)

# BIM-CA colors
$navy = [System.Drawing.Color]::FromArgb(255, 26, 26, 46)
$coral = [System.Drawing.Color]::FromArgb(255, 231, 76, 60)
$white = [System.Drawing.Color]::White

# Capsule - elongated like the real BIMPills logo
$strokeW = [math]::Max(4, $Size / 14)
$capW = $bmpW * 0.70
$capH = $bmpH * 0.88
$x = ($bmpW - $capW) / 2
$y = ($bmpH - $capH) / 2
$midY = $y + $capH / 2
$arcH = $capW  # semicircle arcs at top and bottom

# Build full capsule path
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
# Top semicircle
$path.AddArc($x, $y, $capW, $arcH, 180, 180)
# Right side down
$path.AddLine(($x + $capW), ($y + $arcH/2), ($x + $capW), ($y + $capH - $arcH/2))
# Bottom semicircle
$path.AddArc($x, ($y + $capH - $arcH), $capW, $arcH, 0, 180)
# Left side up
$path.AddLine($x, ($y + $capH - $arcH/2), $x, ($y + $arcH/2))
$path.CloseFigure()

# Fill white
$g.FillPath((New-Object System.Drawing.SolidBrush($white)), $path)

# Fill bottom half coral
$g.SetClip((New-Object System.Drawing.RectangleF(0, $midY, $bmpW, $bmpH)))
$g.FillPath((New-Object System.Drawing.SolidBrush($coral)), $path)
$g.ResetClip()

# Thick outline
$pen = New-Object System.Drawing.Pen($navy, $strokeW)
$pen.LineJoin = 'Round'
$g.DrawPath($pen, $path)

# Center dividing line
$g.DrawLine($pen, ($x + $strokeW/2), $midY, ($x + $capW - $strokeW/2), $midY)

# Dark highlight line on white top half (like exclamation mark in the original)
$hlPen = New-Object System.Drawing.Pen($navy, ([math]::Max(2.5, $strokeW * 0.45)))
$hlPen.StartCap = 'Round'
$hlPen.EndCap = 'Round'
$hlX = $x + $capW * 0.30
$hlTop = $y + $arcH/2 + $strokeW * 1.5
$hlBot = $midY - $strokeW * 1.5
$g.DrawLine($hlPen, $hlX, $hlTop, $hlX, $hlBot)

# White reflection line on coral bottom half
$hlPen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100, 255, 255, 255), ([math]::Max(2.5, $strokeW * 0.45)))
$hlPen2.StartCap = 'Round'
$hlPen2.EndCap = 'Round'
$hlTop2 = $midY + $strokeW * 1.5
$hlBot2 = $y + $capH - $arcH/2 - $strokeW * 1.5
$g.DrawLine($hlPen2, $hlX, $hlTop2, $hlX, $hlBot2)

$bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
Write-Host "Created: $OutputPath ($bmpW x $bmpH)"
