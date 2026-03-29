; ============================================================
;  BIMPills — InnoSetup Installer Script
;  Versión: 1.0.0-alpha.3
;  Empresa:  BIM-CA
;  Autor:    Rodrigo Flores + BIM-CA Team
; ============================================================

#define AppName        "BIMPills"
#define AppVersion     "1.0.0-alpha.3"
#define AppPublisher   "BIM-CA"
#define AppURL         "https://bim-ca.com"
#define AppSupportURL  "mailto:soporte@bim-ca.com"
#define AppCopyright   "© 2026 BIM-CA. Todos los derechos reservados."

; Rutas a los binarios compilados (Release)
#define BinDir2026 "..\src\BIMPills.Revit\bin\Release\net8.0-windows"
#define BinDir2025 "..\src\BIMPills.Revit\bin\Release\net8.0-windows"
#define BinDir2024 "..\src\BIMPills.Revit\bin\Release\net48-windows"
#define ManifestDir "..\manifests"

[Setup]
AppId={{4A2F8C3D-E1B7-4D9A-8F6E-C2D5A7B3E091}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppSupportURL}
AppCopyright={#AppCopyright}
VersionInfoVersion=1.0.0.3
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Plugin BIM para Autodesk Revit

; Salida
DefaultDirName={userappdata}\{#AppPublisher}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=.\output
OutputBaseFilename=BIMPills_Setup_{#AppVersion}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
DisableProgramGroupPage=yes
DisableDirPage=yes

; Privilegios — sólo usuario (sin admin) porque los addins van en %APPDATA%
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

; Idioma
ChangesAssociations=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
spanish.WelcomeLabel1=Bienvenido al instalador de [name/ver]
spanish.WelcomeLabel2=Este asistente instalará {#AppName} {#AppVersion} para Autodesk Revit.%n%nSe instalarán los addins para las versiones de Revit que selecciones.%n%nCierra Revit antes de continuar.
spanish.FinishedLabel=La instalación de [name] ha concluido.%n%nAbre Revit para empezar a usar BIMPills.

; ============================================================
;  SELECCIÓN DE COMPONENTES
; ============================================================
[Components]
Name: "revit2026"; Description: "Revit 2026 (Autodesk Revit 2026)"; Types: full; Flags: disablenouninstallwarning
Name: "revit2025"; Description: "Revit 2025 (Autodesk Revit 2025)"; Types: full; Flags: disablenouninstallwarning
Name: "revit2024"; Description: "Revit 2024 (Autodesk Revit 2024)"; Types: full; Flags: disablenouninstallwarning

; ============================================================
;  ARCHIVOS — Revit 2026 (net8.0)
; ============================================================
[Files]
; Addin manifest 2026
Source: "{#ManifestDir}\Revit2026\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; \
  Flags: ignoreversion; \
  Components: revit2026

; DLLs 2026
Source: "{#BinDir2026}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026

; Dependencias 2026 (net8.0)
Source: "{#BinDir2026}\ClosedXML.dll";                    DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\ClosedXML.Parser.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\DocumentFormat.OpenXml.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\ExcelNumberFormat.dll";            DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\Newtonsoft.Json.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\RBush.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\SixLabors.Fonts.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\System.IO.Packaging.dll";          DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026

; ============================================================
;  ARCHIVOS — Revit 2025 (net8.0 — mismos binarios que 2026)
; ============================================================
; Addin manifest 2025
Source: "{#ManifestDir}\Revit2025\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; \
  Flags: ignoreversion; \
  Components: revit2025

; DLLs 2025
Source: "{#BinDir2025}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\ClosedXML.dll";                    DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\ClosedXML.Parser.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\DocumentFormat.OpenXml.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\ExcelNumberFormat.dll";            DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\Newtonsoft.Json.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\RBush.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\SixLabors.Fonts.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\System.IO.Packaging.dll";          DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025

; ============================================================
;  ARCHIVOS — Revit 2024 (net48)
; ============================================================
; Addin manifest 2024
Source: "{#ManifestDir}\Revit2024\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; \
  Flags: ignoreversion; \
  Components: revit2024

; DLLs 2024
Source: "{#BinDir2024}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024

; Dependencias 2024 (net48)
Source: "{#BinDir2024}\ClosedXML.dll";                    DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\ClosedXML.Parser.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\DocumentFormat.OpenXml.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\ExcelNumberFormat.dll";            DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\Newtonsoft.Json.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\RBush.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\SixLabors.Fonts.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\Microsoft.Bcl.HashCode.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Buffers.dll";               DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Memory.dll";                DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Numerics.Vectors.dll";      DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Runtime.CompilerServices.Unsafe.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024

; ============================================================
;  DESINSTALACIÓN — limpia las carpetas del addin
; ============================================================
[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"
Type: files;          Name: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"
Type: files;          Name: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"
Type: files;          Name: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills.addin"

; ============================================================
;  CÓDIGO PASCAL — verifica que Revit esté cerrado
; ============================================================
[Code]
function IsRevitRunning: Boolean;
begin
  Result := FindWindowByClassName('RevitWindowClass') <> 0;
end;

function InitializeSetup(): Boolean;
begin
  if IsRevitRunning then
  begin
    MsgBox(
      'Autodesk Revit está abierto.' + #13#10 +
      'Por favor cierra Revit antes de continuar con la instalación.',
      mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox(
      'BIMPills ' + '{#AppVersion}' + ' instalado correctamente.' + #13#10#13#10 +
      'Abre Revit para empezar a usar el plugin.' + #13#10 +
      'Las herramientas aparecerán en la pestaña "BIMPills".',
      mbInformation, MB_OK);
  end;
end;
