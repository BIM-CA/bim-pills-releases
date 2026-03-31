; ============================================================
;  BIMPills — InnoSetup Installer Script
;  Version: 1.0.0-beta.1
;  Empresa:  BIM-CA
;  Autor:    Rodrigo Flores + BIM-CA Team
;
;  PREREQUISITO: ejecutar build-all.ps1 antes de compilar este script.
;  Los binarios se leen desde dist\RevitXXXX\BIMPills\ (salida de build-all.ps1).
; ============================================================

#define AppName        "BIMPills"
#define AppVersion     "1.0.0-beta.1"
#define AppPublisher   "BIM-CA"
#define AppURL         "https://bim-ca.com"
#define AppSupportURL  "mailto:soporte@bim-ca.com"
#define AppCopyright   "© 2026 BIM-CA. Todos los derechos reservados."

; Binarios por versión de Revit (salida de build-all.ps1)
#define BinDir2026 "..\dist\Revit2026\BIMPills"
#define BinDir2025 "..\dist\Revit2025\BIMPills"
#define BinDir2024 "..\dist\Revit2024\BIMPills"

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
Name: "revit2026"; Description: "Revit 2026 (.NET 8)";             Types: full; Flags: disablenouninstallwarning
Name: "revit2025"; Description: "Revit 2025 (.NET 8)";             Types: full; Flags: disablenouninstallwarning
Name: "revit2024"; Description: "Revit 2024 (.NET Framework 4.8)"; Types: full; Flags: disablenouninstallwarning

; ============================================================
;  ARCHIVOS — Revit 2026  (net8.0-windows)
; ============================================================
[Files]
; Addin manifest 2026
Source: "{#ManifestDir}\Revit2026\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; \
  Flags: ignoreversion; \
  Components: revit2026

; BIMPills assemblies 2026
Source: "{#BinDir2026}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Revit.deps.json";    DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026

; Dependencias NuGet 2026
Source: "{#BinDir2026}\ClosedXML.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\ClosedXML.Parser.dll";                 DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\DocumentFormat.OpenXml.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\ExcelNumberFormat.dll";                DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\Newtonsoft.Json.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\RBush.dll";                            DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\SixLabors.Fonts.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026
Source: "{#BinDir2026}\System.IO.Packaging.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion; Components: revit2026

; Símbolos de depuración 2026
Source: "{#BinDir2026}\BIMPills.Revit.pdb";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Commands.pdb";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Core.pdb";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir2026}\BIMPills.UI.pdb";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026
Source: "{#BinDir2026}\BIMPills.Infrastructure.pdb"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2026

; ============================================================
;  ARCHIVOS — Revit 2025  (net8.0-windows — mismos binarios que 2026)
; ============================================================
Source: "{#ManifestDir}\Revit2025\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; \
  Flags: ignoreversion; \
  Components: revit2025

Source: "{#BinDir2025}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\BIMPills.Revit.deps.json";    DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\ClosedXML.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\ClosedXML.Parser.dll";                 DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\DocumentFormat.OpenXml.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\ExcelNumberFormat.dll";                DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\Newtonsoft.Json.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\RBush.dll";                            DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\SixLabors.Fonts.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025
Source: "{#BinDir2025}\System.IO.Packaging.dll";              DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\BIMPills"; Flags: ignoreversion; Components: revit2025

; ============================================================
;  ARCHIVOS — Revit 2024  (net48-windows — con polyfills .NET Framework 4.8)
; ============================================================
Source: "{#ManifestDir}\Revit2024\BIMPills.addin"; \
  DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; \
  Flags: ignoreversion; \
  Components: revit2024

; BIMPills assemblies 2024 (net48 — sin deps.json ni System.IO.Packaging)
Source: "{#BinDir2024}\BIMPills.Revit.dll";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Commands.dll";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Core.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.UI.dll";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Infrastructure.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024

; Dependencias NuGet 2024
Source: "{#BinDir2024}\ClosedXML.dll";                        DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\ClosedXML.Parser.dll";                 DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\DocumentFormat.OpenXml.dll";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\ExcelNumberFormat.dll";                DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\Newtonsoft.Json.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\RBush.dll";                            DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\SixLabors.Fonts.dll";                  DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024

; Polyfills .NET Framework 4.8 (requeridos por ClosedXML y DocumentFormat.OpenXml en net48)
Source: "{#BinDir2024}\Microsoft.Bcl.HashCode.dll";                    DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Buffers.dll";                            DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Memory.dll";                             DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Numerics.Vectors.dll";                   DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024
Source: "{#BinDir2024}\System.Runtime.CompilerServices.Unsafe.dll";    DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion; Components: revit2024

; Símbolos de depuración 2024
Source: "{#BinDir2024}\BIMPills.Revit.pdb";         DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Commands.pdb";       DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Core.pdb";           DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2024
Source: "{#BinDir2024}\BIMPills.UI.pdb";             DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2024
Source: "{#BinDir2024}\BIMPills.Infrastructure.pdb"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\BIMPills"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit2024

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
      'Las herramientas aparecerán en la pestaña "BIMPills".' + #13#10#13#10 +
      'Novedades en esta versión:' + #13#10 +
      '  • Acotado: 5 esquemas (vanos, ejes, interiores, niveles ARQ, muros exteriores)' + #13#10 +
      '  • Ordenar: categorías y parámetros filtrados por vista activa' + #13#10 +
      '  • Gestionar: diff preview antes de importar Excel' + #13#10 +
      '  • Exportar: guardar perfiles de exportación de planos' + #13#10 +
      '  • Conectar: discovery de capacidades MCP en tiempo real',
      mbInformation, MB_OK);
  end;
end;
