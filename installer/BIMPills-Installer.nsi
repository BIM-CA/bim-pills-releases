; BIMPills v1.0.0-beta.6.0 Installer
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
; Optional bundled WebView2 Bootstrapper (for the Soporte chat window).
; If vendor\MicrosoftEdgeWebview2Setup.exe exists at compile time, the
; bootstrapper is bundled and installed silently when WebView2 Runtime is
; not detected on the target machine. If not bundled, the SupportWindow
; shows a download prompt to the user instead.
;
; To fetch the bootstrapper automatically before building, run:
;   powershell -File installer\download-webview2.ps1
!define WEBVIEW2_SETUP_PATH "vendor\MicrosoftEdgeWebview2Setup.exe"
!if /FileExists "${WEBVIEW2_SETUP_PATH}"
  !define INCLUDE_WEBVIEW2
!endif

;--------------------------------
; Includes
!include "MUI2.nsh"
!include "Sections.nsh"
!include "LogicLib.nsh"

;--------------------------------
; Attributes
Name "BIM Pills"
OutFile "BIMPills-beta-6.0-Setup.exe"
RequestExecutionLevel admin
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
  ; Microsoft.Win32.Registry: BIMPills.Infrastructure (netstandard2.0) referencia
  ; la versión 5.0.0.0 de este assembly. En .NET 10 el runtime de Revit no lo resuelve
  ; automáticamente, por lo que debemos incluirlo explícitamente.
  File "${BUILD_NET10}\Microsoft.Win32.Registry.dll"
  ; WebView2 managed assemblies: necesarios para SupportWindow (Intercom chat).
  ; Core = API principal; Wpf = wrapper WPF (WebView2 control).
  File "${BUILD_NET10}\Microsoft.Web.WebView2.Core.dll"
  File "${BUILD_NET10}\Microsoft.Web.WebView2.Wpf.dll"
  ; WebView2Loader.dll es la DLL nativa (win-x64) que WebView2 necesita para inicializarse.
  ; Debe estar en el mismo directorio que Microsoft.Web.WebView2.Core.dll.
  File "${BUILD_NET10}\WebView2Loader.dll"
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
  ; Microsoft.Win32.Registry: BIMPills.Infrastructure (netstandard2.0) referencia
  ; la versión 5.0.0.0 de este assembly. En .NET 8 el runtime de Revit no lo resuelve
  ; automáticamente, por lo que debemos incluirlo explícitamente.
  File "${BUILD_NET8}\Microsoft.Win32.Registry.dll"
  ; WebView2 managed assemblies: necesarios para SupportWindow (Intercom chat).
  File "${BUILD_NET8}\Microsoft.Web.WebView2.Core.dll"
  File "${BUILD_NET8}\Microsoft.Web.WebView2.Wpf.dll"
  File "${BUILD_NET8}\WebView2Loader.dll"
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
  ; Dependencias de Microsoft.Win32.Registry y System.Drawing no presentes en .NET 4.8 GAC
  File "${BUILD_NET48}\System.Security.AccessControl.dll"
  File "${BUILD_NET48}\System.Security.Principal.Windows.dll"
  File "${BUILD_NET48}\System.Drawing.Common.dll"
  ; Microsoft.Win32.Registry: BIMPills.Infrastructure (netstandard2.0) referencia
  ; la versión 5.0.0.0 de este assembly. En .NET Framework 4.8, el runtime de Revit
  ; no resuelve automáticamente esta versión desde el GAC, por lo que debemos incluirla.
  File "${BUILD_NET48}\Microsoft.Win32.Registry.dll"
  ; WebView2 managed assemblies: necesarios para SupportWindow (Intercom chat).
  File "${BUILD_NET48}\Microsoft.Web.WebView2.Core.dll"
  File "${BUILD_NET48}\Microsoft.Web.WebView2.Wpf.dll"
  File "${BUILD_NET48}\WebView2Loader.dll"
!macroend

;--------------------------------
; Sections

Section "-WriteUninstaller" SEC_UNINSTALL_REG
  WriteUninstaller "$APPDATA\Autodesk\Revit\Addins\BIMPills-Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "DisplayName" "BIM Pills"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "DisplayVersion" "beta 6.0"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "Publisher" "BIM-CA"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "UninstallString" "$APPDATA\Autodesk\Revit\Addins\BIMPills-Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills" \
    "URLInfoAbout" "https://bim-ca.com"
  ; Copiar README junto al desinstalador
  SetOutPath "$APPDATA\Autodesk\Revit\Addins"
  File "README.txt"
SectionEnd

Section "Revit 2027" SEC_2027
  ; Clean previous install to avoid stale DLLs
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2027\BIMPills"
  ; Copy the addin manifest first (Revit scans the parent Addins\20XX\ folder)
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2027"
  File "/oname=BIMPills.addin" "${MANIFEST_2027}"
  !insertmacro InstallNet10 "$APPDATA\Autodesk\Revit\Addins\2027\BIMPills"
  DetailPrint "Instalado en Revit 2027"
SectionEnd

Section "Revit 2026 (recomendado)" SEC_2026
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2026\BIMPills"
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2026"
  File "/oname=BIMPills.addin" "${MANIFEST_2026}"
  !insertmacro InstallNet8 "$APPDATA\Autodesk\Revit\Addins\2026\BIMPills"
  DetailPrint "Instalado en Revit 2026"
SectionEnd

Section "Revit 2025" SEC_2025
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2025\BIMPills"
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2025"
  File "/oname=BIMPills.addin" "${MANIFEST_2025}"
  !insertmacro InstallNet8 "$APPDATA\Autodesk\Revit\Addins\2025\BIMPills"
  DetailPrint "Instalado en Revit 2025"
SectionEnd

Section "Revit 2024" SEC_2024
  RMDir /r "$APPDATA\Autodesk\Revit\Addins\2024\BIMPills"
  SetOutPath "$APPDATA\Autodesk\Revit\Addins\2024"
  File "/oname=BIMPills.addin" "${MANIFEST_2024}"
  !insertmacro InstallNet48 "$APPDATA\Autodesk\Revit\Addins\2024\BIMPills"
  DetailPrint "Instalado en Revit 2024"
SectionEnd

;--------------------------------
; Optional: Bundle PDF24 MSI if available in vendor/
!ifdef INCLUDE_PDF24
Section /o "PDF24 Creator (recomendado para exportar PDF)" SEC_PDF24
  DetailPrint "Instalando PDF24 Creator (puede tardar 1-2 minutos)..."
  SetOutPath "$TEMP"
  File "/oname=bimpills-pdf24-creator.msi" "${PDF24_MSI_PATH}"
  ; /qb = basic UI (progress only, no wizard)
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
; Required (hidden): Install WebView2 Runtime if not already present.
; WebView2 is needed for the Soporte chat window (Intercom via WebView2).
; Silently skipped if the Runtime is already installed (most Win10/11 machines
; already have it via Windows Update or Edge). Only runs the bootstrapper when
; the runtime is genuinely missing.
Section "-InstallWebView2" SEC_WEBVIEW2
  ; Detect WebView2 Runtime via registry (GUID is fixed for all versions).
  ; pv = "0.0.0.0" or empty means not installed.
  SetRegView 64
  ReadRegStr $0 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ${If} $0 == ""
    ReadRegStr $0 HKLM "SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" "pv"
  ${EndIf}
  SetRegView 32

  ${If} $0 != ""
  ${AndIf} $0 != "0.0.0.0"
    DetailPrint "WebView2 Runtime ya instalado (v$0) — omitiendo."
    Goto webview2_done
  ${EndIf}

  ; Runtime not found — install if bootstrapper was bundled
  !ifdef INCLUDE_WEBVIEW2
    DetailPrint "Instalando Microsoft Edge WebView2 Runtime..."
    SetOutPath "$TEMP"
    File "/oname=bimpills-webview2-setup.exe" "${WEBVIEW2_SETUP_PATH}"
    ; /silent /install = sin UI, acepta EULA, instala en background
    ExecWait '"$TEMP\bimpills-webview2-setup.exe" /silent /install' $0
    Delete "$TEMP\bimpills-webview2-setup.exe"
    ${If} $0 = 0
      DetailPrint "WebView2 Runtime instalado correctamente."
    ${Else}
      DetailPrint "WebView2 Runtime: setup retornó $0 (puede requerir reinicio)."
    ${EndIf}
  !else
    DetailPrint "WebView2 Runtime no encontrado. El chat de soporte mostrará un enlace de descarga."
  !endif

  webview2_done:
SectionEnd

;--------------------------------
; Required (hidden): Configure "PDF24 (BIMPills)" silent-save printer.
; Runs always. If PDF24 driver is not installed on this machine, it is a no-op.
; This section is separate from the optional MSI bundle above so that users who
; already have PDF24 installed get the printer configured automatically.
Section "-ConfigurePDF24BIMPills" SEC_PDF24_CFG
  ; Check whether the PDF24 printer driver is present on this machine.
  ReadRegStr $0 HKLM "SYSTEM\CurrentControlSet\Control\Print\Environments\Windows x64\Drivers\Version-3\PDF24" "Driver"
  ${If} $0 == ""
    ReadRegStr $0 HKLM "SYSTEM\CurrentControlSet\Control\Print\Environments\Windows NT x86\Drivers\Version-3\PDF24" "Driver"
  ${EndIf}
  ${If} $0 == ""
    DetailPrint "Driver 'PDF24' no encontrado — omitiendo configuración."
    Goto pdf24cfg_done
  ${EndIf}

  DetailPrint "Driver PDF24 detectado. Creando impresora PDF24 (BIMPills)..."

  ; ── Crear impresora dedicada "PDF24 (BIMPills)" con auto-save ──────────────
  ; Mismo patrón que DiRoots ProSheets: impresora separada con su propio
  ; named pipe y servicio. La impresora "PDF24" estándar queda intacta.
  ;
  ; Secuencia correcta:
  ;   1. Registrar servicio "bimpills" en HKLM (leído por el servicio PDF24/SYSTEM)
  ;   2. Registrar servicio "bimpills" en HKCU (leído por pdf24-agent.exe/usuario)
  ;   3. Reiniciar PDF24 → el servicio crea \\.\pipe\PDFPrint - bimpills
  ;   4. Crear puerto/impresora → el pipe ya existe en este momento
  ;
  ; NOTA HKCU: el installer escribe HKCU del proceso elevado, que puede ser una
  ; cuenta admin diferente al usuario final en entornos corporativos. Como
  ; fallback, BIMPills escribe HKCU al cargar ExportSheets (PdfPrinterService.
  ; EnsureBimpillsHkcuServiceConfig), garantizando el usuario correcto siempre.
  ;
  ; NSIS es 32-bit. SetRegView 64 es necesario para HKLM (evita WOW6432Node).
  ; Para HKCU no hay redirección en SOFTWARE\PDF24 pero se usa 64 por simetría.
  SetRegView 64
  ;
  ; HKLM — leído por el servicio PDF24 (corre como SYSTEM) para crear el pipe
  WriteRegStr   HKLM "SOFTWARE\PDF24\Services\bimpills" "Handler"                "autoSave"
  WriteRegStr   HKLM "SOFTWARE\PDF24\Services\bimpills" "Port"                   "\\.\pipe\PDFPrint - bimpills"
  WriteRegStr   HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveDir"            "%localappdata%\BIMPills\PDFTemp"
  WriteRegStr   HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveFilename"       "$$fileName"
  WriteRegDWORD HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveShowProgress"   0
  WriteRegDWORD HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveUseFileChooser" 0
  WriteRegDWORD HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveOverwriteFile"  1
  WriteRegDWORD HKLM "SOFTWARE\PDF24\Services\bimpills" "LoadInCreatorIfOpen"    0
  WriteRegStr   HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveFileCmd"        ""
  WriteRegDWORD HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveOpenDir"        0
  WriteRegDWORD HKLM "SOFTWARE\PDF24\Services\bimpills" "AutoSaveUseFileCmd"     0
  WriteRegStr   HKLM "SOFTWARE\PDF24\Services\bimpills" "FilenameErasements"     ""
  WriteRegStr   HKLM "SOFTWARE\PDF24\Services\bimpills" "ShellCmd"               ""
  ;
  ; HKCU — leído por pdf24-agent.exe (proceso del usuario) para el auto-save
  ; Se escribe también aquí para el caso más común (usuario eleva su propia
  ; cuenta). Si la cuenta admin difiere, el C# runtime fix lo corrige al abrir
  ; ExportSheets por primera vez.
  WriteRegStr   HKCU "SOFTWARE\PDF24\Services\bimpills" "Handler"                "autoSave"
  WriteRegStr   HKCU "SOFTWARE\PDF24\Services\bimpills" "Port"                   "\\.\pipe\PDFPrint - bimpills"
  WriteRegStr   HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveDir"            "%localappdata%\BIMPills\PDFTemp"
  WriteRegStr   HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveFilename"       "$$fileName"
  WriteRegDWORD HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveShowProgress"   0
  WriteRegDWORD HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveUseFileChooser" 0
  WriteRegDWORD HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveOverwriteFile"  1
  WriteRegDWORD HKCU "SOFTWARE\PDF24\Services\bimpills" "LoadInCreatorIfOpen"    0
  WriteRegStr   HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveFileCmd"        ""
  WriteRegDWORD HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveOpenDir"        0
  WriteRegDWORD HKCU "SOFTWARE\PDF24\Services\bimpills" "AutoSaveUseFileCmd"     0
  WriteRegStr   HKCU "SOFTWARE\PDF24\Services\bimpills" "FilenameErasements"     ""
  WriteRegStr   HKCU "SOFTWARE\PDF24\Services\bimpills" "ShellCmd"               ""
  ;
  SetRegView 32

  ; ── Paso 1: Reiniciar PDF24 para que cree \\.\pipe\PDFPrint - bimpills ──────
  ; IMPORTANTE: el pipe lo crea el servicio PDF24 al arrancar, leyendo HKLM.
  ; El restart debe ocurrir ANTES de Add-PrinterPort para que el puerto apunte
  ; a un pipe que ya existe. Solo "net stop" no alcanza: la tray app y los
  ; procesos helper de PDF24 siguen corriendo y bloquean el pipe nuevo.
  ; Secuencia: matar procesos → stop servicios → start servicios → Add-Printer.
  DetailPrint "Reiniciando PDF24 (kill procesos + restart servicio)..."
  ; 1a. Matar todos los procesos pdf24* (tray app, helper, creator, etc.)
  ExecWait 'powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand UwB0AG8AcAAtAFAAcgBvAGMAZQBzAHMAIAAtAE4AYQBtAGUAIABwAGQAZgAyADQAKgAgAC0ARgBvAHIAYwBlACAALQBFAHIAcgBvAHIAQQBjAHQAaQBvAG4AIABTAGkAbABlAG4AdABsAHkAQwBvAG4AdABpAG4AdQBlAA=='
  Sleep 1000
  ; 1b. Detener servicio PDF24 y Spooler
  ExecWait '"$SYSDIR\net.exe" stop "PDF24" /y'
  ExecWait '"$SYSDIR\net.exe" stop "Spooler" /y'
  Sleep 2000
  ; 1c. Iniciar Spooler primero, luego PDF24 (PDF24 lee HKLM y crea el pipe)
  ExecWait '"$SYSDIR\net.exe" start "Spooler"'
  Sleep 2000
  ExecWait '"$SYSDIR\net.exe" start "PDF24"'
  ; 1d. Esperar a que PDF24 cree los pipes (incluyendo \\.\pipe\PDFPrint - bimpills)
  Sleep 4000

  ; ── Paso 2: Crear puerto e impresora (el pipe ya existe tras el restart) ─────
  ; Los puertos PDF24 usan "Local Monitor" (no un monitor custom), así que
  ; Add-PrinterPort y Add-Printer funcionan directamente.
  DetailPrint "Creando puerto e impresora PDF24 (BIMPills)..."
  ExecWait "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand QQBkAGQALQBQAHIAaQBuAHQAZQByAFAAbwByAHQAIAAtAE4AYQBtAGUAIAAnAFwAXAAuAFwAcABpAHAAZQBcAFAARABGAFAAcgBpAG4AdAAgAC0AIABiAGkAbQBwAGkAbABsAHMAJwAgAC0ARQByAHIAbwByAEEAYwB0AGkAbwBuACAAUwBpAGwAZQBuAHQAbAB5AEMAbwBuAHQAaQBuAHUAZQA="
  ExecWait "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand QQBkAGQALQBQAHIAaQBuAHQAZQByACAALQBOAGEAbQBlACAAJwBQAEQARgAyADQAIAAoAEIASQBNAFAAaQBsAGwAcwApACcAIAAtAEQAcgBpAHYAZQByAE4AYQBtAGUAIAAnAFAARABGADIANAAnACAALQBQAG8AcgB0AE4AYQBtAGUAIAAnAFwAXAAuAFwAcABpAHAAZQBcAFAARABGAFAAcgBpAG4AdAAgAC0AIABiAGkAbQBwAGkAbABsAHMAJwAgAC0ARQByAHIAbwByAEEAYwB0AGkAbwBuACAAUwBpAGwAZQBuAHQAbAB5AEMAbwBuAHQAaQBuAHUAZQA="
  DetailPrint "PDF24 (BIMPills) configurado. Pipe bimpills activo."

  pdf24cfg_done:
SectionEnd

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
  Delete "$APPDATA\Autodesk\Revit\Addins\README.txt"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\BIMPills"

  ; ── Eliminar impresora PDF24 (BIMPills) y su servicio ────────────────────
  ExecWait "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand UgBlAG0AbwB2AGUALQBQAHIAaQBuAHQAZQByACAALQBOAGEAbQBlACAAJwBQAEQARgAyADQAIAAoAEIASQBNAFAAaQBsAGwAcwApACcAIAAtAEUAcgByAG8AcgBBAGMAdABpAG8AbgAgAFMAaQBsAGUAbgB0AGwAeQBDAG8AbgB0AGkAbgB1AGUA"
  ExecWait "powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand UgBlAG0AbwB2AGUALQBQAHIAaQBuAHQAZQByAFAAbwByAHQAIAAtAE4AYQBtAGUAIAAnAFwAXAAuAFwAcABpAHAAZQBcAFAARABGAFAAcgBpAG4AdAAgAC0AIABiAGkAbQBwAGkAbABsAHMAJwAgAC0ARQByAHIAbwByAEEAYwB0AGkAbwBuACAAUwBpAGwAZQBuAHQAbAB5AEMAbwBuAHQAaQBuAHUAZQA="
  SetRegView 64
  DeleteRegKey HKLM "SOFTWARE\PDF24\Services\bimpills"
  SetRegView 32
  DeleteRegKey HKCU "SOFTWARE\PDF24\Services\bimpills"
  ; Limpiar residuos de versiones anteriores
  SetRegView 64
  DeleteRegKey HKLM "SOFTWARE\PDF24\Services\pdf24"
  ; Restaurar Services\PDF al valor original si fue modificado
  ReadRegStr $0 HKLM "SOFTWARE\PDF24\Services\PDF" "Handler"
  ${If} $0 == "autoSave"
    WriteRegStr HKLM "SOFTWARE\PDF24\Services\PDF" "Handler" "default"
  ${EndIf}
  SetRegView 32
  DeleteRegKey HKCU "SOFTWARE\PDF24\Services\pdf24"
  ReadRegStr $0 HKCU "SOFTWARE\PDF24\Services\PDF" "Handler"
  ${If} $0 == "autoSave"
    WriteRegStr HKCU "SOFTWARE\PDF24\Services\PDF" "Handler" "default"
  ${EndIf}

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
