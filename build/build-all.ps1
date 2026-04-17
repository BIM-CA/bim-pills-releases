<#
.SYNOPSIS
    Compila BIM Pills para todas las versiones de Revit y despliega a dist/
.EXAMPLE
    .\build\build-all.ps1
    .\build\build-all.ps1 -Versions 2025,2026
    .\build\build-all.ps1 -Configuration Release
#>
param(
    [string[]] $Versions       = @("2024", "2025", "2026", "2027"),
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
    $tfm = switch ($version) {
        "2024" { "net48-windows" }
        "2027" { "net10.0-windows" }
        default { "net8.0-windows" }
    }
    $binDir = Join-Path $solutionDir "src\BIMPills.Revit\bin\$Configuration\$tfm"
    $outDir = Join-Path $solutionDir "dist\Revit$version\BIMPills"

    # Copiar flat desde bin/Release/<tfm>/ → dist/ (preserva estructura plana esperada por Revit)
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Copy-Item "$binDir\*" $outDir -Force -Recurse

    # Copiar Microsoft.Win32.Registry.dll desde NuGet cache si no está en el output.
    # El assembly BIMPills.Infrastructure.dll (netstandard2.0) tiene una referencia
    # a Microsoft.Win32.Registry 5.0.0. El runtime de Revit no resuelve automáticamente
    # esta versión; el AssemblyResolve de BIMPills busca en addinDir, por lo que debemos
    # copiarla explícitamente en TODOS los targets.
    # IMPORTANTE: copiamos tanto a binDir (para que NSIS la empaquete) como a outDir.
    # net48 usa la variante net461; net8/net10 usan netstandard2.0.
    $registrySubPath = if ($tfm -eq "net48-windows") {
        "runtimes\win\lib\net461\Microsoft.Win32.Registry.dll"
    } else {
        "runtimes\win\lib\netstandard2.0\Microsoft.Win32.Registry.dll"
    }
    $nugetSrc = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.win32.registry\5.0.0\$registrySubPath"
    if (Test-Path $nugetSrc) {
        foreach ($dest in @("$binDir\Microsoft.Win32.Registry.dll", "$outDir\Microsoft.Win32.Registry.dll")) {
            if (-not (Test-Path $dest)) {
                Copy-Item $nugetSrc $dest -Force
                Write-Host "  Copiada Microsoft.Win32.Registry.dll (5.0.0/$($tfm -replace '-windows','')) → $(Split-Path $dest -Parent | Split-Path -Leaf)" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Warning "Microsoft.Win32.Registry.dll no encontrada en NuGet cache ($registrySubPath). Puede causar errores en Revit $version."
    }

    # Copy manifest
    $manifestSrc  = Join-Path $solutionDir "manifests\Revit$version\BIMPills.addin"
    $manifestDest = Join-Path $solutionDir "dist\Revit$version\BIMPills.addin"
    Copy-Item $manifestSrc $manifestDest -Force

    Write-Host "  Output: dist\Revit$version\" -ForegroundColor Green
}

# ── Obfuscación (solo Release) ────────────────────────────────────────────────
if ($Configuration -eq "Release") {
    Write-Host "`n=== Obfuscando assemblies (Core, Infrastructure, Commands) ===" -ForegroundColor Cyan


    $obfuscarCmd = Get-Command "obfuscar.console" -ErrorAction SilentlyContinue
    $obfuscar = if ($obfuscarCmd) { $obfuscarCmd.Source } else { $null }
    if (-not $obfuscar) {
        Write-Warning "obfuscar.console no encontrado. Instalar con: dotnet tool install --global Obfuscar.GlobalTool"
    } else {
        $xmlTemplate = Join-Path $PSScriptRoot "obfuscar.xml"

        foreach ($version in $Versions) {
            $tfm = switch ($version) {
                "2024" { "net48-windows" }
                "2027" { "net10.0-windows" }
                default { "net8.0-windows" }
            }
            $binDir = Join-Path $solutionDir "src\BIMPills.Revit\bin\$Configuration\$tfm"
            $obfDir = Join-Path $binDir "Obfuscated"
            New-Item -ItemType Directory -Force -Path $obfDir | Out-Null

            # Generate XML with absolute paths (Obfuscar resolves relative to XML location)
            $xmlContent = (Get-Content $xmlTemplate -Raw) `
                -replace '\$\(InPath\)',  $binDir `
                -replace '\$\(OutPath\)', $obfDir
            $tmpXml = Join-Path $binDir "obfuscar_tmp.xml"
            Set-Content -Path $tmpXml -Value $xmlContent -Encoding UTF8

            # Run Obfuscar with resolved XML
            & $obfuscar $tmpXml 2>&1 | Out-Null
            Remove-Item $tmpXml -Force -ErrorAction SilentlyContinue

            if ($LASTEXITCODE -ne 0) {
                Write-Warning "  Obfuscar fallo para Revit $version - usando DLLs sin ofuscar."
            } else {
                # Replace originals with obfuscated versions
                foreach ($dll in @("BIMPills.Core.dll", "BIMPills.Infrastructure.dll", "BIMPills.Commands.dll")) {
                    $src = Join-Path $obfDir $dll
                    if (Test-Path $src) {
                        Copy-Item $src (Join-Path $binDir $dll) -Force
                        Copy-Item $src (Join-Path $solutionDir "dist\Revit$version\BIMPills\$dll") -Force
                    }
                }
                # Clean up temp obfuscation dir
                Remove-Item $obfDir -Recurse -Force
                Write-Host "  Revit ${version}: assemblies ofuscados OK" -ForegroundColor Green
            }
        }
    }
}

Write-Host "`nListo. Carpeta dist\ lista para distribuir." -ForegroundColor Green
