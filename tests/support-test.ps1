#requires -Version 5.1
# Auto-test del chat de soporte: lanza sandbox, hace click, captura pantalla

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# 1. Lanzar sandbox
$exePath = "G:\Claude\Code\tests\BIMPills.UI.Sandbox\bin\Debug\net8.0-windows\BIMPills.UI.Sandbox.exe"
Write-Host "Lanzando $exePath..."
$proc = Start-Process $exePath -PassThru
Start-Sleep -Seconds 3

# 2. Simular click en botón de Soporte usando UI Automation
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$auto = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$sandboxWin = $auto.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)

if ($null -eq $sandboxWin) {
    Write-Host "ERROR: Ventana sandbox no encontrada" -ForegroundColor Red
    exit 1
}
Write-Host "Sandbox encontrado: $($sandboxWin.Current.Name)"

# Buscar botón de Soporte (por texto que contenga "Soporte")
$btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)
$buttons = $sandboxWin.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
$supportBtn = $null
foreach ($b in $buttons) {
    if ($b.Current.Name -like "*Soporte*") { $supportBtn = $b; break }
}

if ($null -eq $supportBtn) {
    Write-Host "ERROR: Botón Soporte no encontrado. Botones disponibles:" -ForegroundColor Red
    foreach ($b in $buttons) { Write-Host "  - $($b.Current.Name)" }
    exit 1
}
Write-Host "Click en: $($supportBtn.Current.Name)"
$invoke = $supportBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$invoke.Invoke()

# 3. Esperar a que cargue Intercom (12 s)
Write-Host "Esperando 14 s..."
Start-Sleep -Seconds 14

# 4. Buscar ventana de SupportWindow (Topmost, Title "BIM Pills — Soporte")
$supportWin = $null
$allWin = $auto.FindAll([System.Windows.Automation.TreeScope]::Children,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))
foreach ($w in $allWin) {
    if ($w.Current.Name -like "*Soporte*" -and $w.Current.Name -ne $sandboxWin.Current.Name) {
        $supportWin = $w; break
    }
}

if ($null -eq $supportWin) {
    Write-Host "ERROR: SupportWindow no encontrado" -ForegroundColor Red
    # Capturar pantalla completa como fallback
    $screen = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $screen.Width, $screen.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen($screen.X, $screen.Y, 0, 0, $bmp.Size)
    $outPath = "G:\Claude\Code\tests\support-screenshot-full.png"
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Screenshot completo: $outPath"
    exit 1
}

# 5. Capturar región de la ventana de soporte
$rect = $supportWin.Current.BoundingRectangle
Write-Host "SupportWindow: $($rect.X),$($rect.Y) $($rect.Width)x$($rect.Height)"
$bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bmp)
$graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
$outPath = "G:\Claude\Code\tests\support-screenshot.png"
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Screenshot guardado: $outPath"

# Mantener sandbox abierto para inspección si hace falta
Write-Host "PID sandbox: $($proc.Id)"
