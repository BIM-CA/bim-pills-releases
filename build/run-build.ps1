$ErrorActionPreference = "Stop"
$solutionDir = "G:\Claude\Code"
$dotnet = "dotnet"
$versions = @("2024","2025","2026","2027")

foreach ($version in $versions) {
    Write-Host "`n=== Revit $version ===" -ForegroundColor Cyan

    & $dotnet build "$solutionDir\src\BIMPills.Revit\BIMPills.Revit.csproj" `
        -c Release -p:RevitVersion=$version --nologo 2>&1 |
        Where-Object { $_ -match "rror|succeeded|Errores" } |
        ForEach-Object { Write-Host $_ }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED para Revit $version" -ForegroundColor Red
        exit 1
    }

    $tfm = switch ($version) {
        "2024" { "net48-windows" }
        "2027" { "net10.0-windows" }
        default { "net8.0-windows" }
    }

    $binDir = "$solutionDir\src\BIMPills.Revit\bin\Release\$tfm"
    $outDir = "$solutionDir\dist\Revit$version\BIMPills"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Copy-Item "$binDir\*" $outDir -Force -Recurse

    $manifestSrc  = "$solutionDir\manifests\Revit$version\BIMPills.addin"
    $manifestDest = "$solutionDir\dist\Revit$version\BIMPills.addin"
    Copy-Item $manifestSrc $manifestDest -Force

    Write-Host "  OK: dist\Revit$version\" -ForegroundColor Green
}

Write-Host "`nTodos los builds completados." -ForegroundColor Green
