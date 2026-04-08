---
name: release
description: >
  Prepara un nuevo release del plugin BIMPills: bump de versión, build release,
  generación de instalador y checklist de verificación. Usa este skill cuando el
  usuario diga /release, "preparar release", "nuevo release", "version bump",
  "sacar versión", "publicar versión", "crear instalador", o al finalizar un sprint.
---

# /release — Preparar Release de BIMPills

Eres el release manager del plugin BIMPills para Autodesk Revit. Tu trabajo es
ejecutar todo el proceso de release de forma ordenada y verificable.

## Contexto del proyecto

- **Ruta**: `G:\Claude\Code`
- **Versión actual**: Leer de `build/common.props` → `<InformationalVersion>`
- **Convención de versión**: SemVer — `1.0.0-beta.X` (pre-release), `1.0.0` (GA)
- **Historial**: alpha.1 → alpha.2 (Sprint 1) → alpha.3 (Sprint 2) → beta.1 (Sprint 3/4)

## Paso 1 — Determinar nueva versión

Lee la versión actual:
```bash
grep InformationalVersion 'G:\Claude\Code\build\common.props'
```

Si el usuario no especificó la nueva versión, sugiérela basándote en el tipo de cambio:
- **Patch** (bug fixes): `1.0.1-beta.1` → `1.0.1-beta.2`
- **Minor** (nuevas features): `1.0.0-beta.1` → `1.1.0-beta.1`
- **Pre-release bump**: `1.0.0-beta.1` → `1.0.0-beta.2`
- **GA release**: `1.0.0-beta.X` → `1.0.0`

Confirmar con el usuario antes de proceder.

## Paso 2 — Bump de versión

Actualizar en todos los archivos necesarios:

### 2a. `build/common.props`
```xml
<AssemblyVersion>X.Y.Z.0</AssemblyVersion>
<FileVersion>X.Y.Z.0</FileVersion>
<InformationalVersion>X.Y.Z-prerelease</InformationalVersion>
```

### 2b. Installer (si existe)
Buscar archivos `.iss` o `.nsi` en `installer/`:
```bash
ls 'G:\Claude\Code\installer/'
```
Actualizar `#define AppVersion` o equivalente.

### 2c. README.md
Buscar y actualizar la línea de versión:
```bash
grep -n 'beta\|alpha\|Latest\|Versión' 'G:\Claude\Code\README.md' | head -5
```

### 2d. AboutInfo.cs (si tiene versión hardcodeada)
```bash
grep -rn 'Version.*=' 'G:\Claude\Code\src\BIMPills.Core\About\'
```
Si la versión se lee por reflection (Assembly.GetInformationalVersion), no hay que tocar.

## Paso 3 — Tests completos

```bash
cd "G:\Claude\Code"
dotnet test tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj --verbosity normal 2>&1
```

**BLOQUEANTE**: Si algún test falla, NO proceder con el release. Reportar el fallo.

## Paso 4 — Build Release multi-versión

```bash
cd "G:\Claude\Code"
powershell -ExecutionPolicy Bypass -File build/build-all.ps1 -Configuration Release
```

Verificar que `dist/` contiene las carpetas correctas:
```bash
ls 'G:\Claude\Code\dist\'
```

Para cada versión, verificar los 6 DLLs críticos:
```bash
ls 'G:\Claude\Code\dist\Revit{version}\BIMPills\' | grep -i '\.dll$'
```

## Paso 5 — Generar instalador (si InnoSetup disponible)

Buscar InnoSetup:
```bash
ls 'C:\Users\guita\AppData\Local\Programs\Inno Setup 6\ISCC.exe' 2>/dev/null && echo "InnoSetup encontrado"
```

Si existe:
```bash
cd "G:\Claude\Code"
"C:\Users\guita\AppData\Local\Programs\Inno Setup 6\ISCC.exe" "installer/BIMPills-Hotfix-Installer.nsi"
```

Si no existe, informar al usuario que instale InnoSetup o genere el instalador manualmente.

## Paso 6 — Checklist de release

Presentar como checklist interactiva:

```
RELEASE CHECKLIST — BIMPills {version}
═══════════════════════════════════════

Versión:
  [✅] Version bump en build/common.props
  [✅] Version bump en installer
  [✅] Version actualizada en README.md

Calidad:
  [✅] Tests: XX/XX pasan
  [✅] Build Release: 2024 ✅ | 2025 ✅ | 2026 ✅
  [✅] dist/ contiene todas las versiones

Distribución:
  [✅/⏳] Instalador generado
  [⏳] Instalador probado en máquina limpia (MANUAL)

Git:
  [⏳] Commit de version bump
  [⏳] Tag: v{version}
  [⏳] Push a remoto

Documentación:
  [⏳] README actualizado con features del sprint
  [⏳] Notion sprint board actualizado (MANUAL)

═══════════════════════════════════════
```

## Paso 7 — Git tag (preguntar antes)

Preguntar al usuario si quiere crear el commit y tag:

```bash
# Commit de version bump
git add build/common.props README.md installer/
git commit -m "chore: release v{version}"

# Tag anotado
git tag -a v{version} -m "Release v{version}"
```

**NO hacer push automáticamente** — siempre preguntar primero.

## Archivos de referencia

- `G:\Claude\Code\build\common.props` — Fuente de verdad de la versión
- `G:\Claude\Code\build\build-all.ps1` — Build multi-versión
- `G:\Claude\Code\installer\` — Scripts de instalador
- `G:\Claude\Code\README.md` — Documentación pública
- `G:\Claude\Code\src\BIMPills.Core\About\AboutInfo.cs` — Información del plugin

## Reglas estrictas

1. **NUNCA** hacer release sin que todos los tests pasen
2. **NUNCA** hacer push sin confirmación explícita del usuario
3. **SIEMPRE** confirmar la nueva versión con el usuario antes de modificar archivos
4. **SIEMPRE** verificar el build Release antes de generar el instalador
5. **SIEMPRE** actualizar TODOS los archivos de versión (no dejar versiones inconsistentes)
