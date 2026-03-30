<#
.SYNOPSIS
    Compila BIM Pills para todas las versiones de Revit y despliega a dist/
.EXAMPLE
    .\build\build-all.ps1
    .\build\build-all.ps1 -Versions 2025,2026
    .\build\build-all.ps1 -Configuration Release
#>
param(
    [string[]] $Versions       = @("2024", "2025", "2026"),
    [string]   $Configuration  = "Release"
)

$ErrorActionPreference = "Stop"
$solutionDir = Split-Path $PSScriptRoot -Parent
$dotnet = "dotnet"

foreach ($version in $Versions) {
    Write-Host "`n=== Compilando para Revit $version ===" -ForegroundColor Cyan

    # Build (NO usamos --output: WPF genera subcarpeta net*-windows que rompe la carga de Revit)
    & $dotnet build "$solutionDir\src\BIMPills.Revit\BIMPills.Revit.csproj" `
        -c $Configuration `
        -p:RevitVersion=$version `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build falló para Revit $version"
        exit 1
    }

    # Determinar el framework de salida según versión de Revit
    $tfm = if ($version -eq "2024") { "net48-windows" } else { "net8.0-windows" }
    $binDir = Join-Path $solutionDir "src\BIMPills.Revit\bin\$Configuration\$tfm"
    $outDir = Join-Path $solutionDir "dist\Revit$version\BIMPills"

    # Copiar flat desde bin/Release/<tfm>/ → dist/ (preserva estructura plana esperada por Revit)
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Copy-Item "$binDir\*" $outDir -Force -Recurse

    # Copy manifest
    $manifestSrc  = Join-Path $solutionDir "manifests\Revit$version\BIMPills.addin"
    $manifestDest = Join-Path $solutionDir "dist\Revit$version\BIMPills.addin"
    Copy-Item $manifestSrc $manifestDest -Force

    Write-Host "  Output: dist\Revit$version\" -ForegroundColor Green
}

Write-Host "`nListo. Carpeta dist\ lista para distribuir." -ForegroundColor Green
