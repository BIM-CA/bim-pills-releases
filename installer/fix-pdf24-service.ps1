# Fix PDF24 auto-save: apuntar al servicio correcto (PDF, no pdf24)
# Ejecutar como Administrador

Write-Host "=== Configurando PDF24 auto-save en Services\PDF ===" -ForegroundColor Cyan

# HKLM — leído por el servicio PDF24 (SYSTEM)
$hklmPath = "HKLM:\SOFTWARE\PDF24\Services\PDF"
Write-Host "Escribiendo HKLM: $hklmPath"
Set-ItemProperty -Path $hklmPath -Name "Handler"                -Value "autoSave"
Set-ItemProperty -Path $hklmPath -Name "AutoSaveDir"            -Value "%localappdata%\BIMPills\PDFTemp"
Set-ItemProperty -Path $hklmPath -Name "AutoSaveFilename"       -Value '$fileName'
New-ItemProperty -Path $hklmPath -Name "AutoSaveShowProgress"   -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hklmPath -Name "AutoSaveUseFileChooser" -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hklmPath -Name "AutoSaveOverwriteFile"  -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hklmPath -Name "LoadInCreatorIfOpen"    -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hklmPath -Name "AutoSaveOpenDir"        -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hklmPath -Name "AutoSaveUseFileCmd"     -Value 0 -PropertyType DWord -Force | Out-Null

# HKCU — leído por la app PDF24 del usuario
$hkcuPath = "HKCU:\SOFTWARE\PDF24\Services\PDF"
if (-not (Test-Path $hkcuPath)) { New-Item -Path $hkcuPath -Force | Out-Null }
Write-Host "Escribiendo HKCU: $hkcuPath"
Set-ItemProperty -Path $hkcuPath -Name "Handler"                -Value "autoSave"
Set-ItemProperty -Path $hkcuPath -Name "AutoSaveDir"            -Value "%localappdata%\BIMPills\PDFTemp"
Set-ItemProperty -Path $hkcuPath -Name "AutoSaveFilename"       -Value '$fileName'
New-ItemProperty -Path $hkcuPath -Name "AutoSaveShowProgress"   -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hkcuPath -Name "AutoSaveUseFileChooser" -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hkcuPath -Name "AutoSaveOverwriteFile"  -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hkcuPath -Name "LoadInCreatorIfOpen"    -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hkcuPath -Name "AutoSaveOpenDir"        -Value 0 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $hkcuPath -Name "AutoSaveUseFileCmd"     -Value 0 -PropertyType DWord -Force | Out-Null

# Reiniciar servicios
Write-Host "`nReiniciando servicios..." -ForegroundColor Yellow
Stop-Service -Name "PDF24" -Force -ErrorAction SilentlyContinue
Stop-Service -Name "Spooler" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Start-Service -Name "Spooler"
Start-Sleep -Seconds 2
Start-Service -Name "PDF24"
Start-Sleep -Seconds 2

# Verificar
Write-Host "`n=== Verificación ===" -ForegroundColor Green
$check = Get-ItemProperty -Path $hklmPath -Name "Handler" -ErrorAction SilentlyContinue
Write-Host "HKLM Handler: $($check.Handler)"
$check2 = Get-ItemProperty -Path $hkcuPath -Name "Handler" -ErrorAction SilentlyContinue
Write-Host "HKCU Handler: $($check2.Handler)"

# Crear directorio PDFTemp
$tempDir = Join-Path $env:LOCALAPPDATA "BIMPills\PDFTemp"
if (-not (Test-Path $tempDir)) {
    New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
    Write-Host "Creado: $tempDir"
}

Write-Host "`n¡Listo! Reinicia Revit y prueba exportar." -ForegroundColor Green
Read-Host "Presiona Enter para cerrar"
