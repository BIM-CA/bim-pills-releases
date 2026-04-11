# Downloads the PDF24 Creator MSI into installer/vendor/ so the NSIS installer
# can bundle it as an optional component. Run this before `makensis` if you
# want to ship a self-contained installer that includes PDF24.
#
# Usage:
#   powershell -File installer\download-pdf24.ps1
#
# The MSI is ~35 MB. If you don't want to bundle PDF24, skip this step — the
# NSIS script will detect the missing file and simply omit the PDF24 section
# (users will then have to install PDF24 themselves, but BIM Pills' PDF
# engine will still work with other installed PDF printers).

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$vendorDir = Join-Path $scriptDir 'vendor'
$outPath   = Join-Path $vendorDir 'pdf24-creator.msi'
$url       = 'https://tools.pdf24.org/static/pdf24-creator.msi'

if (-not (Test-Path $vendorDir))
{
    New-Item -ItemType Directory -Path $vendorDir | Out-Null
}

if (Test-Path $outPath)
{
    $size = (Get-Item $outPath).Length / 1MB
    Write-Host ("PDF24 MSI ya existe ({0:N1} MB) en: {1}" -f $size, $outPath) -ForegroundColor Green
    Write-Host "Si querés re-descargarlo, borrá el archivo y volvé a correr este script."
    exit 0
}

Write-Host "Descargando PDF24 Creator MSI desde pdf24.org..." -ForegroundColor Cyan
Write-Host "  URL:    $url"
Write-Host "  Destino: $outPath"
Write-Host ""

try
{
    # TLS 1.2 required by most modern endpoints
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $outPath -UseBasicParsing
}
catch
{
    Write-Host "ERROR: No se pudo descargar el MSI." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternativa: descargá el MSI manualmente desde:" -ForegroundColor Yellow
    Write-Host "  https://tools.pdf24.org/en/creator"
    Write-Host "y guardalo como:"
    Write-Host "  $outPath"
    exit 1
}

$size = (Get-Item $outPath).Length / 1MB
Write-Host ""
Write-Host ("OK — descargado {0:N1} MB" -f $size) -ForegroundColor Green
Write-Host "Ahora podés compilar el installer con:"
Write-Host "  cd installer"
Write-Host "  makensis BIMPills-Installer.nsi"
