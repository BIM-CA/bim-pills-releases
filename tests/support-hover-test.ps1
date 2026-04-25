Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$proc = Get-Process -Name 'BIMPills.UI.Sandbox' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Host "sandbox no corriendo"; exit 1 }

$auto = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$allWin = $auto.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
$supportWin = $null
foreach ($w in $allWin) {
    if ($w.Current.Name -like "*Soporte*" -and $w.Current.Name -notlike "*Sandbox*") { $supportWin = $w; break }
}
if (-not $supportWin) { Write-Host "SupportWindow no encontrado"; exit 1 }
$rect = $supportWin.Current.BoundingRectangle

# Mover mouse al centro de la ventana de soporte para disparar hover
$cx = [int]($rect.X + $rect.Width / 2)
$cy = [int]($rect.Y + $rect.Height / 2)
[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point($cx, $cy)
Start-Sleep -Milliseconds 500

$bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bmp)
$graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
$outPath = "G:\Claude\Code\tests\support-hover.png"
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Hover screenshot: $outPath"
