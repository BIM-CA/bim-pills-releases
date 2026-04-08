; BIMPills v1.0.0-beta.2 Installer
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

;--------------------------------
; Includes
!include "MUI2.nsh"
!include "Sections.nsh"

;--------------------------------
; Attributes
Name "BIMPills v1.0.0-beta.2"
OutFile "BIMPills-1.0.0-beta.2-Setup.exe"
RequestExecutionLevel user
InstallDir "$APPDATA\Autodesk\Revit\Addins"

;--------------------------------
; UI Settings
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

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

Section "Revit 2027" SEC_2027
  !insertmacro InstallNet10 "$APPDATA\Autodesk\Revit\Addins\2027\BIMPills"
  DetailPrint "Instalado en Revit 2027"
SectionEnd

Section "Revit 2026 (recomendado)" SEC_2026
  !insertmacro InstallNet8 "$APPDATA\Autodesk\Revit\Addins\2026\BIMPills"
  DetailPrint "Instalado en Revit 2026"
SectionEnd

Section "Revit 2025" SEC_2025
  !insertmacro InstallNet8 "$APPDATA\Autodesk\Revit\Addins\2025\BIMPills"
  DetailPrint "Instalado en Revit 2025"
SectionEnd

Section "Revit 2024" SEC_2024
  !insertmacro InstallNet48 "$APPDATA\Autodesk\Revit\Addins\2024\BIMPills"
  DetailPrint "Instalado en Revit 2024"
SectionEnd

;--------------------------------
; Uninstaller
Section "Uninstall"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2027\BIMPills"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2026\BIMPills"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2025\BIMPills"
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2024\BIMPills"
  MessageBox MB_OK "BIMPills ha sido desinstalado."
SectionEnd

;--------------------------------
; Descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2027} "Instala BIMPills para Autodesk Revit 2027 (.NET 10)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2026} "Instala BIMPills para Autodesk Revit 2026 (.NET 8)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2025} "Instala BIMPills para Autodesk Revit 2025 (.NET 8)"
  !insertmacro MUI_DESCRIPTION_TEXT ${SEC_2024} "Instala BIMPills para Autodesk Revit 2024 (.NET Framework 4.8)"
!insertmacro MUI_FUNCTION_DESCRIPTION_END
