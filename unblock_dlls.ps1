$folder = "C:\Users\guita\AppData\Roaming\Autodesk\Revit\Addins\2026\BIMPills"
Get-ChildItem $folder -Filter "*.dll" | ForEach-Object {
    Unblock-File -Path $_.FullName
    Write-Host "Desbloqueado: $($_.Name)"
}
Write-Host "Listo."
