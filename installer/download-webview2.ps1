# Downloads the Microsoft Edge WebView2 Runtime Evergreen Bootstrapper into
# installer/vendor/ so the NSIS installer can bundle it and install it
# silently when the runtime is not present on the target machine.
#
# Usage:
#   powershell -File installer\download-webview2.ps1
#
# The bootstrapper is ~1.7 MB. At install time it downloads and installs
# the full runtime (~120 MB) from Microsoft CDN — requires internet access.
# If you don't want to bundle it, skip this step — the NSIS script will
# detect the missing file and omit the section (the SupportWindow will
# then show a download prompt to the user if WebView2 is not installed).

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$vendorDir = Join-Path $scriptDir 'vendor'
$outPath   = Join-Path $vendorDir 'MicrosoftEdgeWebview2Setup.exe'
$url       = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703'

if (-not (Test-Path $vendorDir))
{
    New-Item -ItemType Directory -Path $vendorDir | Out-Null
}

if (Test-Path $outPath)
{
    $size = (Get-Item $outPath).Length / 1KB
    Write-Host ("WebView2 bootstrapper ya existe ({0:N0} KB) en: {1}" -f $size, $outPath) -ForegroundColor Green
    Write-Host "Si querés re-descargarlo, borrá el archivo y volvé a correr este script."
    exit 0
}

Write-Host "Descargando WebView2 Evergreen Bootstrapper desde Microsoft..." -ForegroundColor Cyan
Write-Host "  URL:     $url"
Write-Host "  Destino: $outPath"
Write-Host ""

try
{
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $outPath -UseBasicParsing
}
catch
{
    Write-Host "ERROR: No se pudo descargar el bootstrapper." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternativa: descargalo manualmente desde:" -ForegroundColor Yellow
    Write-Host "  https://developer.microsoft.com/en-us/microsoft-edge/webview2/"
    Write-Host "y guardalo como:"
    Write-Host "  $outPath"
    exit 1
}

$size = (Get-Item $outPath).Length / 1KB
Write-Host ""
Write-Host ("OK — descargado {0:N0} KB" -f $size) -ForegroundColor Green
Write-Host "Ahora podés compilar el installer con:"
Write-Host "  cd installer"
Write-Host "  makensis BIMPills-Installer.nsi"
