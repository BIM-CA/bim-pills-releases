$addinDir  = "$env:APPDATA\Autodesk\Revit\Addins\2024"
$binDir    = "G:\Claude\Code\src\BIMPills.Revit\bin\Release\net48-windows"
$pluginDir = "$addinDir\BIMPills"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item "$binDir\*" $pluginDir -Force -Recurse
Copy-Item "G:\Claude\Code\manifests\Revit2024\BIMPills.addin" "$addinDir\BIMPills.addin" -Force
Write-Host "Deploy 2024 completo. Archivos en $pluginDir"
