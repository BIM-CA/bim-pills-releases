# Generate BIMPills pill icon PNG using System.Drawing
Add-Type -AssemblyName System.Drawing

function New-PillIcon {
    param([string]$OutputPath, [int]$Size = 64)
    
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'HighQuality'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::Transparent)
    
    # Colors from BIM-CA branding
    $darkNavy = [System.Drawing.Color]::FromArgb(255, 26, 26, 46)    # #1A1A2E
    $coral    = [System.Drawing.Color]::FromArgb(255, 231, 76, 60)   # #E74C3C
    $white    = [System.Drawing.Color]::White
    
    $penWidth = [math]::Max(2, $Size / 16)
    $pen = New-Object System.Drawing.Pen($darkNavy, $penWidth)
    $pen.StartCap = 'Round'
    $pen.EndCap = 'Round'
    
    # Capsule dimensions
    $margin = $Size * 0.15
    $capW = $Size * 0.5
    $capH = $Size * 0.8
    $x = ($Size - $capW) / 2
    $y = ($Size - $capH) / 2
    $radius = $capW / 2
    
    # Fill bottom half (coral)
    $coralBrush = New-Object System.Drawing.SolidBrush($coral)
    $midY = $y + $capH / 2
    
    # Bottom rounded rect
    $pathBottom = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathBottom.AddArc($x, $midY - $radius, $capW, $capW, 0, 180)
    $pathBottom.AddLine($x, $midY, $x, $midY)
    $pathBottom.CloseFigure()
    
    # Fill top half (white)
    $whiteBrush = New-Object System.Drawing.SolidBrush($white)
    $pathTop = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathTop.AddArc($x, $y, $capW, $capW, 180, 180)
    $pathTop.AddLine($x + $capW, $y + $radius, $x + $capW, $midY)
    $pathTop.AddLine($x + $capW, $midY, $x, $midY)
    $pathTop.AddLine($x, $midY, $x, $y + $radius)
    $pathTop.CloseFigure()
    
    # Full capsule path for outline
    $pathFull = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathFull.AddArc($x, $y, $capW, $capW, 180, 180)
    $pathFull.AddLine($x + $capW, $y + $radius, $x + $capW, $y + $capH - $radius)
    $pathFull.AddArc($x, $y + $capH - $capW, $capW, $capW, 0, 180)
    $pathFull.AddLine($x, $y + $capH - $radius, $x, $y + $radius)
    $pathFull.CloseFigure()
    
    # Draw fills
    $g.FillPath($whiteBrush, $pathFull)
    # Fill bottom coral
    $clipBottom = New-Object System.Drawing.RectangleF($x, $midY, $capW, $capH/2 + $penWidth)
    $g.SetClip($clipBottom)
    $g.FillPath($coralBrush, $pathFull)
    $g.ResetClip()
    
    # Draw outline
    $g.DrawPath($pen, $pathFull)
    
    # Draw center dividing line
    $g.DrawLine($pen, $x, $midY, $x + $capW, $midY)
    
    # Highlight line on left side
    $highlightPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 255, 255, 255), [math]::Max(1.5, $penWidth * 0.6))
    $highlightPen.StartCap = 'Round'
    $highlightPen.EndCap = 'Round'
    $hlX = $x + $capW * 0.25
    $g.DrawLine($highlightPen, $hlX, $y + $radius + $penWidth, $hlX, $midY - $penWidth * 2)
    
    # Coral side highlight
    $highlightPen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 255, 255, 255), [math]::Max(1.5, $penWidth * 0.6))
    $highlightPen2.StartCap = 'Round'
    $highlightPen2.EndCap = 'Round'
    $g.DrawLine($highlightPen2, $hlX, $midY + $penWidth * 2, $hlX, $y + $capH - $radius - $penWidth)
    
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    
    Write-Host "Created: $OutputPath"
}

$resDir = Split-Path $MyInvocation.MyCommand.Path
New-PillIcon -OutputPath "$resDir\pill-icon-64.png" -Size 64
New-PillIcon -OutputPath "$resDir\pill-icon-32.png" -Size 32
Write-Host "Done!"
