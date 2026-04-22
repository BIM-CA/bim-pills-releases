$nsis  = "C:\Program Files (x86)\NSIS\makensis.exe"
$nsi   = "G:\Claude\Code\installer\BIMPills-Installer.nsi"
$exeOut = "G:\Claude\Code\installer\BIMPills-beta-4.1-Setup.exe"

# Delete old EXE so we know if build actually succeeded
if (Test-Path $exeOut) { Remove-Item $exeOut -Force }

Write-Host "Building installer..." -ForegroundColor Cyan
$result = & $nsis /V2 $nsi 2>&1
$result | Write-Host

if (Test-Path $exeOut) {
    $size = [math]::Round((Get-Item $exeOut).Length / 1MB, 2)
    Write-Host "`nOK: $([System.IO.Path]::GetFileName($exeOut)) ($size MB)" -ForegroundColor Green
} else {
    Write-Host "`nERROR: No se creo el installer!" -ForegroundColor Red
    exit 1
}
