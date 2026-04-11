; BIMPills v1.0.0-beta.3.2 Installer
; NSIS Installer Script — Revit 2024, 2025, 2026 y 2027
;
; Build: cd installer && makensis BIMPills-Installer.nsi
; Requires: build all versions first:
;   .\build\build-all.ps1 -Configuration Release

;--------------------------------
; Build directories (relative to installer/ — script location)
!define BUILD_NET10 "..\src\BIMPills.Revit\bin\Release\net10.0-windows"
!define BUILD_NET8  "..\src\BIMPills.Revit\bin\Release\net8.0-windows"
!define BUILD_NET48 "..\src\BIMPills.Revit\bin\Release\net48-windows"

; Addin manifest files (one per Revit version — goes in Addins\20XX\, not BIMPills\)
!define MANIFEST_2027 "..\manifests\Revit2027\BIMPills.addin"
!define MANIFEST_2026 "..\manifests\Revit2026\BIMPills.addin"
!define MANIFEST_2025 "..\manifests\Revit2025\BIMPills.addin"
!define MANIFEST_2024 "..\manifests\Revit2024\BIMPills.addin"

;--------------------------------
; Optional bundled PDF24 installer (for the PDF engine "Impresora del sistema").
; If vendor\pdf24-creator.msi exists at compile time, PDF24 is bundled and
; offered as an optional component. If not, the section is skipped and users
; will have to install PDF24 themselves (the feature still works with other
; PDF printers like Microsoft Print to PDF).
;
; To fetch the MSI automatically before building, run:
;   powershell -File installer\download-pdf24.ps1
!define PDF24_MSI_PATH "vendor\pdf24-creator.msi"
!if /FileExists "${PDF24_MSI_PATH}"
  !define INCLUDE_PDF24
!endif

;--------------------------------
; Includes
!include "MUI2.nsh"
!include "Sections.nsh"
!include "LogicLib.nsh"

;--------------------------------
; Attributes
Name "BIM Pills"
OutFile "BIM Pills 1.0.0-beta.3.2 Setup.exe"
RequestExecutionLevel user
InstallDir "$APPDATA\Autodesk\Revit\Addins"

;--------------------------------
; Branding assets
!define MUI_ICON "assets\bimpills.ico"
!define MUI_UNICON "assets\bimpills.ico"

; Header shown on every inner page (components, instfiles, finish header, uninstall confirm/progress)
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_BITMAP   "assets\header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "assets\header.bmp"
!define MUI_HEADERIMAGE_RIGHT

; Left-panel image on Welcome + Finish pages (installer and uninstaller)
!define MUI_WELCOMEFINISHPAGE_BITMAP   "assets\welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "assets\welcome.bmp"

;--------------------------------
; UI Settings
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "Spanish"

;--------------------------------
; Macro: instala DLLs net10 (Revit 2027)
!macro InstallNet10 DEST_DIR
  SetOutPath "${DEST_DIR}"
  File "${BUILD_NET10}\BIMPills.Commands.dll"
  File "${BUILD_NET10}\BIMPills.Core.dll"
  File "${BUILD_NET10}\BIMPills.Infrastructure.dll"
  File "${BUILD_NET10}\BIMPills.Revit.dll"
  File "${BUILD_NET10}\BIMPills.UI.dll"
  File "${BUILD_NET10}\ClosedXML.dll"
  File "${BUILD_NET10}\ClosedXML.Parser.dll"
  File "${BUILD_NET10}\DocumentFormat.OpenXml.dll"
  File "${BUILD_NET10}\DocumentFormat.OpenXml.Framework.dll"
  File "${BUILD_NET10}\ExcelNumberFormat.dll"
  File "${BUILD_NET10}\Newtonsoft.Json.dll"
  File "${BUILD_NET10}\RBush.dll"
  File "${BUILD_NET10}\SixLabors.Fonts.dll"
  File "${BUILD_NET10}\System.Management.dll"
!macroend

;--------------------------------
; Macro: instala DLLs net8 (Revit 2025/2026)
!macro InstallNet8 DEST_DIR
  SetOutPath "${DEST_DIR}"
  File "${BUILD_NET8}\BIMPills.Commands.dll"
  File "${BUILD_NET8}\BIMPills.Core.dll"
  File "${BUILD_NET8}\BIMPills.Infrastructure.dll"
  File "${BUILD_NET8}\BIMPills.Revit.dll"
  File "${BUILD_NET8}\BIMPills.UI.dll"
  File "${BUILD_NET8}\ClosedXML.dll"
  File "${BUILD_NET8}\ClosedXML.Parser.dll"
  File "${BUILD_NET8}\DocumentFormat.OpenXml.dll"
  File "${BUILD_NET8}\DocumentFormat.OpenXml.Framework.dll"
  File "${BUILD_NET8}\ExcelNumberFormat.dll"
  File "${BUILD_NET8}\Newtonsoft.Json.dll"
  File "${BUILD_NET8}\RBush.dll"
  File "${BUILD_NET8}\SixLabors.Fonts.dll"
  File "${BUILD_NET8}\System.IO.Packaging.dll"
  File "${BUILD_NET8}\System.Management.dll"
!macroend

;--------------------------------
; Macro: instala DLLs net48 (Revit 2024)
!macro InstallNet48 DEST_DIR
  SetOutPath "${DEST_DIR}"
  File "${BUILD_NET48}\BIMPills.Commands.dll"
  File "${BUILD_NET48}\BIMPills.Core.dll"
  File "${BUILD_NET48}\BIMPills.Infrastructure.dll"
  File "${BUILD_NET48}\BIMPills.Revit.dll"
  File "${BUILD_NET48}\BIMPills.UI.dll"
  File "${BUILD_NET48}\ClosedXML.dll"
  File "${BUILD_NET48}\ClosedXML.Parser.dll"
  File "${BUILD_NET48}\DocumentFormat.OpenXml.dll"
  File "${BUILD_NET48}\DocumentFormat.OpenXml.Framework.dll"
  File "${BUILD_NET48}\ExcelNumberFormat.dll"
  File "${BUILD_NET48}\Microsoft.Bcl.HashCode.dll"
  File "${BUILD_NET48}\Newtonsoft.Json.dll"
  File "${BUILD_NET48}\RBush.dll"
  File "${BUILD_NET48}\SixLabors.Fonts.dll"
  File "${BUILD_NET48}\System.Buffers.dll"
  File "${BUILD_NET48}\System.CodeDom.dll"
  File "${BUILD_NET48}\System.Memory.dll"
  File "${BUILD_NET48}\System.Numerics.Vectors.dll"
  File "${BUILD_NET48}\System.Runtime.CompilerServices.Unsafe.dll"
  File "${BUILD_NET48}\System.Security.Cryptography.ProtectedData.dll"
!macroend

;--------------------------------
; Sections

Section "-WriteUninstaller" SEC_UNINSTALL_REG
  WriteUninstaller "$APPDATA\Autodesk\Revit\Addins\BIMPills-Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "DisplayName" "BIM Pills"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "DisplayVersion" "1.0.0-beta.3.2"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "Publisher" "BIM-CA"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "UninstallString" "$APPDATA\Autodesk\Revit\Addins\BIMPills-Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "URLInfoAbout" "https://bim-ca.com"
SectionEnd

Section "Revit 2027" SEC_2027
  ; Copy the addin manifest first (Revit scans the parent Addins\20XX\ folder)
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2027"
  File "/oname=BIMPills.addin" "${MANIFEST_2027}"
  !insertmacro InstallNet10 "$APPDATA\Autodesk\Revit\Addins\2027\BIMPills"
  DetailPrint "Instalado en Revit 2027"
SectionEnd

Section "Revit 2026 (recomendado)" SEC_2026
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2026"
  File "/oname=BIMPills.addin" "${MANIFEST_2026}"
  !insertmacro InstallNet8 "$APPDATA\Autodesk\Revit\Addins\2026\BIMPills"
  DetailPrint "Instalado en Revit 2026"
SectionEnd

Section "Revit 2025" SEC_2025
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2025"
  File "/oname=BIMPills.addin" "${MANIFEST_2025}"
  !insertmacro InstallNet8 "$APPDATA\Autodesk\Revit\Addins\2025\BIMPills"
  DetailPrint "Instalado en Revit 2025"
SectionEnd

Section "Revit 2024" SEC_2024
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2024"
  File "/oname=BIMPills.addin" "${MANIFEST_2024}"
  !insertmacro InstallNet48 "$APPDATA\Autodesk\Revit\Addins\2024\BIMPills"
  DetailPrint "Instalado en Revit 2024"
SectionEnd

;--------------------------------
; Optional: PDF24 Creator (silent-printing PDF driver for "Impresora del sistema")
!ifdef INCLUDE_PDF24
Section /o "PDF24 Creator (recomendado para exportar PDF)" SEC_PDF24
  DetailPrint "Instalando PDF24 Creator (puede tardar 1-2 minutos)..."
  SetOutPath "$TEMP"
  File "/oname=bimpills-pdf24-creator.msi" "${PDF24_MSI_PATH}"
  ; /qb = basic UI (progress only, no wizard) — keeps the install minimally
  ;        interactive so the user knows something is happening.
  ; /norestart = never trigger a reboot.
  ; ADDLOCAL=ALL = install all features (driver + creator tray app).
  ExecWait 'msiexec.exe /i "$TEMP\bimpills-pdf24-creator.msi" /qb /norestart ADDLOCAL=ALL' $0
  Delete "$TEMP\bimpills-pdf24-creator.msi"
  ${If} $0 = 0
    DetailPrint "PDF24 Creator instalado correctamente."
  ${Else}
    DetailPrint "PDF24 Creator: msiexec retornó $0 (ignorado)."
  ${EndIf}
SectionEnd
!endif

;--------------------------------
; Uninstaller
Section "Uninstall"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2027\BIMPills"
  Delete "$APPDATA\Autodesk\Revit\Addins\2027\BIMPills.addin"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2026\BIMPills"
  Delete "$APPDATA\Autodesk\Revit\Addins\2026\BIMPills.addin"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2025\BIMPills"
  Delete "$APPDATA\Autodesk\Revit\Addins\2025\BIMPills.addin"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2024\BIMPills"
  Delete "$APPDATA\Autodesk\Revit\Addins\2024\BIMPills.addin"
  Delete "$APPDATA\Autodesk\Revit\Addins\BIMPills-Uninstall.exe"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills"
  MessageBox MB_OK "BIM Pills ha sido desinstalado."
SectionEnd


;--------------------------------
; Descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2027} "Instala BIMPills para Autodesk Revit 2027 (.NET 10)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2026} "Instala BIMPills para Autodesk Revit 2026 (.NET 8)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2025} "Instala BIMPills para Autodesk Revit 2025 (.NET 8)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2024} "Instala BIMPills para Autodesk Revit 2024 (.NET Framework 4.8)"
!ifdef INCLUDE_PDF24
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_PDF24} "Instala PDF24 Creator, una impresora PDF gratuita. BIM Pills la usa como motor alternativo para exportar planos cuando el motor nativo de Revit pierde líneas o textos."
!endif
!insertmacro MUI_FUNCTION_DESCRIPTION_END
