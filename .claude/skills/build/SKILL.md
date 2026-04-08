---
name: build
description: >
  Compila el plugin BIMPills para una o más versiones de Revit y despliega a la
  carpeta de addins. Usa este skill cuando el usuario diga /build, "compilar",
  "build", "construir", "hacer build", "compilar para 2025", "compilar para 2026",
  "compilar todo", o después de hacer cambios que necesitan verificar compilación.
---

# /build — Build Multi-Versión y Deploy de BIMPills

Eres el sistema de build del plugin BIMPills para Autodesk Revit. Tu trabajo es
compilar, desplegar y verificar que todo está correctamente instalado.

## Contexto del proyecto

- **Ruta**: `G:\Claude\Code`
- **Proyecto principal**: `src/BIMPills.Revit/BIMPills.Revit.csproj`
- **Versiones soportadas**: 2024, 2025, 2026 (producción: 2025 y 2026)
- **Frameworks**: 2024 → net48-windows, 2025/2026 → net8.0-windows
- **DLLs críticas** (6): BIMPills.Revit.dll, BIMPills.Core.dll, BIMPills.UI.dll, BIMPills.Commands.dll, BIMPills.Infrastructure.dll, ClosedXML.dll
- **Rutas de deployment**:
  - Debug: `C:\Users\guita\AppData\Roaming\Autodesk\Revit\Addins\{version}\BIMPills\`
  - Release: `G:\Claude\Code\dist\Revit{version}\BIMPills\`

## Paso 1 — Determinar parámetros

| Parámetro | Default | Opciones |
|-----------|---------|----------|
| Versiones | 2025, 2026 | "todo/all" = 2024+2025+2026, o versión específica |
| Configuración | Debug | Debug (auto-deploy), Release (a dist/) |
| Clean | No | Sí si el usuario lo pide |

Interpreta el mensaje del usuario:
- "compilar" / "build" → Debug, 2025+2026
- "compilar todo" → Debug, 2024+2025+2026
- "build release" / "compilar para producción" → Release, 2024+2025+2026
- "compilar para 2026" → Debug, solo 2026

## Paso 2 — Clean (opcional)

Solo si el usuario lo pide o si hay errores de build previos:

```bash
dotnet clean src/BIMPills.Revit/BIMPills.Revit.csproj -c {config} -p:RevitVersion={version}
```

## Paso 3 — Compilar

**Para Debug** (compilar cada versión individualmente):
```bash
cd "G:\Claude\Code"
dotnet build src/BIMPills.Revit/BIMPills.Revit.csproj -c Debug -p:RevitVersion={version} --verbosity minimal 2>&1
```

**Para Release** (usar build-all.ps1):
```bash
cd "G:\Claude\Code"
powershell -ExecutionPolicy Bypass -File build/build-all.ps1 -Versions {versions} -Configuration Release
```

Ejecutar builds para versiones independientes en paralelo cuando sea posible.

## Paso 4 — Verificar deployment

Para cada versión compilada, verificar que los 6 DLLs críticos existen en la carpeta destino.

**Debug** → `C:\Users\guita\AppData\Roaming\Autodesk\Revit\Addins\{version}\BIMPills\`
**Release** → `G:\Claude\Code\dist\Revit{version}\BIMPills\`

```bash
ls '{deployDir}' | grep -i '\.dll$'
```

DLLs esperados:
1. BIMPills.Revit.dll
2. BIMPills.Core.dll
3. BIMPills.UI.dll
4. BIMPills.Commands.dll
5. BIMPills.Infrastructure.dll
6. ClosedXML.dll

También verificar que existe `BIMPills.addin` en el directorio padre de la carpeta BIMPills.

## Paso 5 — Unblock DLLs (si necesario)

Si los DLLs fueron descargados o copiados desde una unidad de red/externa:
```bash
powershell -Command "Get-ChildItem '{deployDir}' -Filter '*.dll' | Unblock-File"
```

## Manejo de errores comunes

| Error | Causa | Solución |
|-------|-------|----------|
| MSB3021 / MSB3027 | Revit tiene DLLs bloqueados | Cerrar Revit antes de compilar |
| CS0234 "tipo no existe" | Falta referencia a proyecto | Verificar .csproj references |
| FileNotFoundException RevitAPI | Revit no instalado | Instalar Revit {version} o cambiar RevitInstallDir |
| NuGet restore fail | Paquetes no descargados | `dotnet restore` primero |
| NETSDK1005 | TFM incorrecto | Verificar RevitVersion está bien pasado |

## Formato del reporte

```
BUILD REPORT — BIMPills
═══════════════════════════════════════
Configuración: {Debug/Release}
Fecha: {fecha}

Revit 2025: ✅ Compilación exitosa (X advertencias)
Revit 2026: ✅ Compilación exitosa (X advertencias)

Deploy:
  Revit 2025: ✅ 6/6 DLLs en {ruta}
  Revit 2026: ✅ 6/6 DLLs en {ruta}
  BIMPills.addin: ✅ Presente
═══════════════════════════════════════
```

Si hay errores, listarlos con contexto suficiente para diagnosticar.

## Archivos de referencia

- `G:\Claude\Code\build\build-all.ps1` — Script de build multi-versión
- `G:\Claude\Code\build\common.props` — Versión, frameworks, símbolos de compilación
- `G:\Claude\Code\src\BIMPills.Revit\BIMPills.Revit.csproj` — Proyecto principal con DeployToRevit target
- `G:\Claude\Code\deploy_2026.ps1` — Script de deploy rápido para 2026
- `G:\Claude\Code\unblock_dlls.ps1` — Script para desbloquear DLLs
