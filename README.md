# BIMPills

**BIMPills** es un plugin de Revit para control de calidad de modelos BIM, automatización de acotado, gestión de worksets, numeración incremental de elementos y sincronización de tablas con Excel.

**Latest Release:** v1.0.0-beta.1 (2026-03-30) — Sprint 3 complete
**Support:** support@bim-ca.com | Website Chat

---

## Features

### Sprint 1 (v1.0.0-alpha.2) ✅
- **Auditar** — Análisis de salud del modelo: warnings, elementos purgables, health score
- **Documentar (Acotado)** — Acotado automático inteligente: espacios interiores, vanos, ejes
- **Estandarizar** — Gestión y estandarización de worksets
- **Exportar Familias** — Exportación de familias cargadas
- **Multi-version** — Revit 2024, 2025, 2026

### Sprint 2 (v1.0.0-alpha.3) ✅
- **Exportar (Audit + Planos)** — Exportación de reportes de auditoría y planos a XLSX/PDF
- **Esquemas personalizados** — Creación, edición y persistencia de esquemas de acotado en JSON
- **Conectar (MCP)** — Gestión de conexiones a servicios externos vía Model Context Protocol

### Sprint 3 (v1.0.0-beta.1) ✅
- **Ordenar** — Numeración incremental interactiva de elementos con prefijo/paso/sufijo configurable
- **Gestionar (SheetLink)** — Exportación de tablas de planificación a Excel y reimportación masiva de parámetros

---

## Ribbon Layout

```
Tab: BIMPills
├── Panel: Datos
│   ├── Auditar         [ModelAudit]
│   ├── Exportar        [ExportAudit + ExportSheets]
│   ├── Conectar        [MCPIntegration]
│   ├── Ordenar         [Ordering]
│   └── Gestionar       [DataManager / SheetLink]
├── Panel: Procesos
│   ├── Documentar      [Acotado]
│   └── Estandarizar    [Gestion / Worksets]
└── Panel: Información
    └── Acerca de       [About]
```

---

## Architecture

BIMPills sigue una **arquitectura limpia de 4 capas**:

```
┌─────────────────────────────────────────────────────────┐
│ UI Layer (WPF)                                          │
│ └── *.xaml + *.xaml.cs windows                         │
├─────────────────────────────────────────────────────────┤
│ Commands Layer (Business Logic)                         │
│ └── IPluginCommand implementations (sin RevitAPI)      │
├─────────────────────────────────────────────────────────┤
│ Core Layer (Pure Abstractions)                         │
│ ├── Interfaces (IPluginCommand, IPluginModule)         │
│ ├── Models (audit results, ordering, schedules)       │
│ └── Services (IDocumentServices, ILogger)             │
├─────────────────────────────────────────────────────────┤
│ Infrastructure Layer (Persistence)                      │
│ ├── JSON repositories (profiles, schemes, connections) │
│ └── Exporters (XLSX via ClosedXML)                     │
├─────────────────────────────────────────────────────────┤
│ Revit Adapter Layer                                     │
│ ├── RevitApplication.cs (ribbon + module registration) │
│ ├── RevitCommandBase (external command bridge)         │
│ └── RevitDocumentServices (IDocumentServices impl.)    │
└─────────────────────────────────────────────────────────┘
        ↓
      RevitAPI.dll
```

### Projects

| Project | Purpose | References |
|---------|---------|-----------|
| `BIMPills.Core` | Interfaces, models, service contracts | Nothing (pure abstractions) |
| `BIMPills.Commands` | Command logic without RevitAPI | Core only |
| `BIMPills.Infrastructure` | JSON persistence, XLSX exporters | Core, Newtonsoft.Json, ClosedXML |
| `BIMPills.UI` | WPF windows and XAML | Core, Commands |
| `BIMPills.Revit` | RevitAPI adapter, ribbon builder | All above + RevitAPI.dll |
| `BIMPills.Core.Tests` | xUnit tests for Core/Commands | Core, Commands, xUnit |

---

## Building

### Single version

```bash
dotnet restore
dotnet build -p:RevitVersion=2026 -c Release
dotnet test tests/BIMPills.Core.Tests/ -c Release
```

### Multi-version (for installer)

```powershell
foreach ($v in @("2024", "2025", "2026")) {
    dotnet build src/BIMPills.Revit/ -p:RevitVersion=$v -c Release
}
```

### Version targets

| Revit | Framework | Symbol |
|-------|-----------|--------|
| 2024 | `net48-windows` (.NET 4.8) | `REVIT2024` |
| 2025 | `net8.0-windows` (.NET 8) | `REVIT2025` |
| 2026 | `net8.0-windows` (.NET 8) | `REVIT2026` |

---

## Installation

### Installer (recommended)

Run `installer/output/BIMPills_Setup_1.0.0-beta.1.exe` — selecciona las versiones de Revit a instalar. Detecta y reemplaza versiones anteriores automáticamente.

### Manual

1. Build para tu versión de Revit
2. Copia las DLLs a: `%APPDATA%\Autodesk\Revit\Addins\{Year}\BIMPills\`
3. Copia `BIMPills.addin` a: `%APPDATA%\Autodesk\Revit\Addins\{Year}\`
4. Reinicia Revit

---

## Testing

```bash
dotnet test tests/BIMPills.Core.Tests/ -c Release
# 32+ tests passing ✅
```

Tests cubiertos: ModelAudit, AcotadoVanos, About, RibbonDuplicates (todos los módulos)

---

## Data Persistence

```
%APPDATA%\Autodesk\Revit\Addins\BIMPills\
├── Profiles\reports.json          (Export profiles)
├── Schemes\dimensions.json        (Custom dimension schemas)
├── MCPConnections\connections.json (MCP service connections)
└── Exports\                        (Schedule Excel exports)
```

---

## License

Copyright © 2026 BIM-CA. All rights reserved.
