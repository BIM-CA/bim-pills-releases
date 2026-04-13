# Fix final: restaurar PDF estándar + reiniciar para activar pipe bimpills
# Ejecutar como Administrador

Write-Host "=== Fix PDF24 - Paso Final ===" -ForegroundColor Cyan

# 1. Restaurar servicio PDF estándar a "default"
Write-Host "1. Restaurando Services\PDF Handler a 'default'..."
Set-ItemProperty -Path "HKLM:\SOFTWARE\PDF24\Services\PDF" -Name "Handler" -Value "default"
if (Test-Path "HKCU:\SOFTWARE\PDF24\Services\PDF") {
    Set-ItemProperty -Path "HKCU:\SOFTWARE\PDF24\Services\PDF" -Name "Handler" -Value "default"
}

# 2. Verificar que bimpills service tiene autoSave
Write-Host "2. Verificando Services\bimpills..."
$h = (Get-ItemProperty "HKLM:\SOFTWARE\PDF24\Services\bimpills" -Name Handler -ErrorAction SilentlyContinue).Handler
Write-Host "   HKLM bimpills Handler: $h"

# 3. Verificar que la impresora existe
Write-Host "3. Verificando impresora..."
$p = Get-Printer -Name "PDF24 (BIMPills)" -ErrorAction SilentlyContinue
if ($p) {
    Write-Host "   OK: $($p.Name) -> $($p.PortName)" -ForegroundColor Green
} else {
    Write-Host "   FALTA: Creando impresora..." -ForegroundColor Yellow
    Add-PrinterPort -Name '\\.\pipe\PDFPrint - bimpills' -ErrorAction SilentlyContinue
    Add-Printer -Name 'PDF24 (BIMPills)' -DriverName 'PDF24' -PortName '\\.\pipe\PDFPrint - bimpills' -ErrorAction SilentlyContinue
}

# 4. Reiniciar servicios
Write-Host "4. Reiniciando Spooler + PDF24..."
Stop-Service -Name "PDF24" -Force -ErrorAction SilentlyContinue
Stop-Service -Name "Spooler" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Start-Service -Name "Spooler"
Start-Sleep -Seconds 2
Start-Service -Name "PDF24"
Start-Sleep -Seconds 3

# 5. Crear directorio PDFTemp
$tempDir = Join-Path $env:LOCALAPPDATA "BIMPills\PDFTemp"
if (-not (Test-Path $tempDir)) {
    New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
}
Write-Host "5. Directorio PDFTemp: $tempDir"

# 6. Verificación final
Write-Host "`n=== Verificación Final ===" -ForegroundColor Green
$pdf = Get-ItemProperty "HKLM:\SOFTWARE\PDF24\Services\PDF" -Name Handler -ErrorAction SilentlyContinue
Write-Host "Services\PDF Handler: $($pdf.Handler) (debe ser 'default')"
$bim = Get-ItemProperty "HKLM:\SOFTWARE\PDF24\Services\bimpills" -Name Handler -ErrorAction SilentlyContinue
Write-Host "Services\bimpills Handler: $($bim.Handler) (debe ser 'autoSave')"
$pr = Get-Printer -Name "PDF24 (BIMPills)" -ErrorAction SilentlyContinue
if ($pr) { Write-Host "Impresora: $($pr.Name) -> $($pr.PortName)" -ForegroundColor Green }
else { Write-Host "ERROR: Impresora no encontrada!" -ForegroundColor Red }

Write-Host "`nReinicia Revit y prueba exportar." -ForegroundColor Cyan
Read-Host "Presiona Enter para cerrar"
