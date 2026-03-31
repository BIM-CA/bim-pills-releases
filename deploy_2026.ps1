$addinDir  = "$env:APPDATA\Autodesk\Revit\Addins\2026"
$binDir    = "G:\Claude\Code\src\BIMPills.Revit\bin\Release\net8.0-windows"
$pluginDir = "$addinDir\BIMPills"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item "$binDir\*" $pluginDir -Force -Recurse
Copy-Item "G:\Claude\Code\manifests\Revit2026\BIMPills.addin" "$addinDir\BIMPills.addin" -Force
Write-Host "Deploy completo. Archivos en $pluginDir"
