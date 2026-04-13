$date = Get-Date -Format 'yyyy-MM-dd'
$rel  = "G:\Claude\Code\installer\releases\$date"
New-Item -ItemType Directory -Force -Path $rel | Out-Null
Copy-Item "G:\Claude\Code\installer\BIM Pills 1.0.0-beta.3.2 Setup.exe" $rel -Force
$size = [System.IO.FileInfo]"$rel\BIM Pills 1.0.0-beta.3.2 Setup.exe"
Write-Host "Installer copiado a: $rel ($([math]::Round($size.Length/1MB,1)) MB)" -ForegroundColor Green

Write-Host ""
Write-Host "=== QA: DLLs criticos ===" -ForegroundColor Cyan
$versions = @('Revit2024','Revit2025','Revit2026','Revit2027')
$dlls     = @('BIMPills.Revit.dll','BIMPills.Core.dll','BIMPills.UI.dll','BIMPills.Commands.dll','BIMPills.Infrastructure.dll')
foreach ($v in $versions) {
    $dir  = "G:\Claude\Code\dist\$v\BIMPills"
    $ok   = $true
    foreach ($d in $dlls) {
        if (-not (Test-Path (Join-Path $dir $d))) {
            Write-Host "  MISSING: $v\$d" -ForegroundColor Red
            $ok = $false
        }
    }
    if ($ok) {
        $count = (Get-ChildItem $dir -Filter '*.dll').Count
        Write-Host "  OK: $v ($count DLLs totales)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "=== QA: Manifests ===" -ForegroundColor Cyan
foreach ($v in @('2024','2025','2026','2027')) {
    $m = "G:\Claude\Code\dist\Revit$v\BIMPills.addin"
    if (Test-Path $m) {
        Write-Host "  OK: Revit$v\BIMPills.addin" -ForegroundColor Green
    } else {
        Write-Host "  MISSING: Revit$v manifest!" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== QA: Installer EXE ===" -ForegroundColor Cyan
$exePath = "$rel\BIM Pills 1.0.0-beta.3.2 Setup.exe"
if (Test-Path $exePath) {
    $info = Get-Item $exePath
    Write-Host "  OK: $($info.Name)" -ForegroundColor Green
    Write-Host "      Tamanio: $([math]::Round($info.Length/1MB,2)) MB"
    Write-Host "      Fecha:   $($info.LastWriteTime)"
} else {
    Write-Host "  ERROR: Installer no encontrado!" -ForegroundColor Red
}
Write-Host ""
Write-Host "Build completo." -ForegroundColor Green
