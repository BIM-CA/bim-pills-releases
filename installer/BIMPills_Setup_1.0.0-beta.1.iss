; ============================================================
;  BIMPills — InnoSetup Installer Script
;  Version: 1.0.0-beta.1
;  Empresa:  BIM-CA
;  Autor:    Rodrigo Flores + BIM-CA Team
; ============================================================

#define AppName        "BIMPills"
#define AppVersion     "1.0.0-beta.1"
#define AppPublisher   "BIM-CA"
#define AppURL         "https://bim-ca.com"
#define AppSupportURL  "mailto:soporte@bim-ca.com"
#define AppCopyright   "© 2026 BIM-CA. Todos los derechos reservados."

; Binarios sueltos en raíz del repo (hotfix compilado)
#define BinDir ".."

; Manifests de addin por versión de Revit
#define ManifestDir "..\manifests"

[Setup]
; Mismo AppId que versiones anteriores → InnoSetup detecta y desinstala automáticamente
AppId={{4A2F8C3D-E1B7-4D9A-8F6E-C2D5A7B3E091}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppSupportURL}
AppCopyright={#AppCopyright}
VersionInfoVersion=1.0.0.11
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

; Permite actualización silenciosa sobre versiones anteriores
; sin pedir al usuario que desinstale primero
CloseApplications=yes
CloseApplicationsFilter=*revit*
RestartApplications=no

; Privilegios — sólo usuario (sin admin) porque los addins van en %APPDATA%
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

ChangesAssociations=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
spanish.WelcomeLabel1=Bienvenido al instalador de [name/ver]
spanish.WelcomeLabel2=Este asistente instalará {#AppName} {#AppVersion} para Autodesk Revit.%n%nSi tienes una versión anterior instalada, será reemplazada automáticamente.%n%nCierra Revit antes de continuar.
spanish.FinishedLabel=La instalación de [name] ha concluido.%n%nAbre Revit para empezar a usar BIMPills.

; ============================================================
;  SELECCIÓN DE COMPONENTES
; ============================================================
[Components]
Name: "revit2026"; Description: "Revit 2026 (.NET 8)"; Types: full; Flags: disablenouninstallwarning
Name: "revit2025"; Description: "Revit 2025 (.NET 8)"; Types: full; Flags: disablenouninstallwarning
Name: "revit2024"; Description: "Revit 2024 (.NET Framework 4.8)"; Types: full; Flags: disablenouninstallwarning

; ============================================================
;  ARCHIVOS — Revit 2026
; ============================================================
[Files]
; Addin manifest 2026
Source: "{#ManifestDir}\Revit2026\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; \
  Flags: ignoreversion; \
  Components: revit2026

; BIMPills core assemblies 2026
Source: "{#BinDir}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\BIMPills.Revit.deps.json";    DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026

; Dependencias 2026
Source: "{#BinDir}\ClosedXML.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\ClosedXML.Parser.dll";                 DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\DocumentFormat.OpenXml.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\ExcelNumberFormat.dll";                DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\Newtonsoft.Json.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\RBush.dll";                            DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\SixLabors.Fonts.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir}\System.IO.Packaging.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026

; Símbolos de depuración 2026 (opcionales)
Source: "{#BinDir}\BIMPills.Revit.pdb";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir}\BIMPills.Commands.pdb";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir}\BIMPills.Core.pdb";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir}\BIMPills.UI.pdb";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir}\BIMPills.Infrastructure.pdb"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026

; ============================================================
;  ARCHIVOS — Revit 2025 (mismos binarios net8.0)
; ============================================================
Source: "{#ManifestDir}\Revit2025\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; \
  Flags: ignoreversion; \
  Components: revit2025

Source: "{#BinDir}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\BIMPills.Revit.deps.json";    DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\ClosedXML.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\ClosedXML.Parser.dll";                 DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\DocumentFormat.OpenXml.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\ExcelNumberFormat.dll";                DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\Newtonsoft.Json.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\RBush.dll";                            DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\SixLabors.Fonts.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir}\System.IO.Packaging.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025

; ============================================================
;  ARCHIVOS — Revit 2024 (net48 — mismos binarios por ahora)
; ============================================================
Source: "{#ManifestDir}\Revit2024\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; \
  Flags: ignoreversion; \
  Components: revit2024

Source: "{#BinDir}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\ClosedXML.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\ClosedXML.Parser.dll";                 DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\DocumentFormat.OpenXml.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\ExcelNumberFormat.dll";                DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\Newtonsoft.Json.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\RBush.dll";                            DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\SixLabors.Fonts.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir}\System.IO.Packaging.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024

; ============================================================
;  DESINSTALACIÓN — limpia carpetas del addin
; ============================================================
[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"
Type: files;          Name: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"
Type: files;          Name: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"
Type: files;          Name: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills.addin"

; ============================================================
;  CÓDIGO PASCAL
; ============================================================
[Code]

{ Detecta si Revit está abierto }
function IsRevitRunning: Boolean;
begin
  Result := FindWindowByClassName('RevitWindowClass') <> 0;
end;

{ Obtiene la cadena de desinstalación de una versión anterior por AppId }
function GetUninstallString(AppID: string): string;
var
  sUnInstPath: string;
  sUnInstallString: string;
begin
  sUnInstPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + AppID + '_is1';
  sUnInstallString := '';
  if not RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

{ Devuelve True si existe una versión anterior instalada }
function IsUpgrade: Boolean;
begin
  Result := GetUninstallString('{4A2F8C3D-E1B7-4D9A-8F6E-C2D5A7B3E091}') <> '';
end;

{ Desinstala silenciosamente la versión anterior }
procedure UninstallPreviousVersion;
var
  sUnInstallString: string;
  iResultCode: Integer;
begin
  sUnInstallString := GetUninstallString('{4A2F8C3D-E1B7-4D9A-8F6E-C2D5A7B3E091}');
  if sUnInstallString <> '' then
  begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE,
         ewWaitUntilTerminated, iResultCode);
  end;
end;

{ Verificaciones antes de iniciar la instalación }
function InitializeSetup(): Boolean;
begin
  if IsRevitRunning then
  begin
    MsgBox(
      'Autodesk Revit está abierto.' + #13#10 +
      'Por favor cierra Revit antes de continuar con la instalación.',
      mbError, MB_OK);
    Result := False;
    Exit;
  end;
  Result := True;
end;

{ Desinstala versión anterior justo antes de copiar archivos }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    if IsUpgrade then
      UninstallPreviousVersion;
  end;

  if CurStep = ssPostInstall then
  begin
    MsgBox(
      'BIMPills ' + '{#AppVersion}' + ' instalado correctamente.' + #13#10#13#10 +
      'Abre Revit para empezar a usar el plugin.' + #13#10 +
      'Las herramientas aparecerán en la pestaña "BIMPills".' + #13#10 +
      'Novedades: Ordenar (numeración incremental) y Gestionar (Excel ↔ Revit).',
      mbInformation, MB_OK);
  end;
end;
