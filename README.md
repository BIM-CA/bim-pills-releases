# BIMPills

**BIMPills** is a Revit plugin for architectural model quality assurance, dimensioning automation, and external service integration.

## Features

### Sprint 1 (v1.0.0-alpha.2) ✅
- **Auditar (Model Audit)** — Comprehensive model health analysis with health score, warnings, purgeable items
- **Documentar (Acotado)** — Intelligent automatic dimensioning for architectural drawings with multi-schema support
- **Gestión** — Purgeable elements management
- **Exportar Familias** — Export loaded families to external format
- **Multi-version support** — Revit 2024, 2025, 2026

### Sprint 2 (v1.0.0 Beta) — In Progress
- **Exportar (Export Audit)** — Export audit reports to PDF/XLSX with customizable profiles
- **Esquemas (Custom Dimension Schemes)** — Create, edit, validate custom dimensioning schemas with JSON persistence
- **Conectar (MCP Integration)** — Manage connections to Model Context Protocol (MCP) external services

## Architecture

BIMPills follows a **4-layer clean architecture**:

```
┌─────────────────────────────────────────────────────────┐
│ UI Layer (WPF)                                          │
│ └── *.xaml + *.xaml.cs windows                         │
├─────────────────────────────────────────────────────────┤
│ Commands Layer (Business Logic)                         │
│ └── IPluginCommand implementations (no RevitAPI)      │
├─────────────────────────────────────────────────────────┤
│ Core Layer (Pure Abstractions)                         │
│ ├── Interfaces (IPluginCommand, IPluginModule)         │
│ ├── Models (audit results, profiles, schemas)         │
│ └── Services (IAuditReportExporter, IDimensionScheme) │
├─────────────────────────────────────────────────────────┤
│ Infrastructure Layer (Persistence)                      │
│ ├── JSON repositories (profiles, schemes, connections) │
│ └── Exporters (PDF, XLSX)                              │
├─────────────────────────────────────────────────────────┤
│ Revit Adapter Layer                                     │
│ ├── RevitApplication.cs (ribbon + module registration) │
│ ├── RevitCommandBase (external command bridge)         │
│ └── Revit-specific adapters (RevitCommandContext)      │
└─────────────────────────────────────────────────────────┘
        ↓
      RevitAPI.dll
```

### Dependency Graph

```
Core (pure abstractions)
  ↑
  ├─ Infrastructure (JSON, PDF/XLSX export)
  ├─ Commands (business logic)
  ├─ UI (WPF windows)
  └─ Revit.Adapter (RevitAPI bridge)
```

## Projects

| Project | Purpose | References |
|---------|---------|-----------|
| `BIMPills.Core` | Interfaces, models, service contracts | Nothing (pure abstractions) |
| `BIMPills.Commands` | Command logic without RevitAPI | Core only |
| `BIMPills.Infrastructure` | JSON persistence, exporters | Core, Newtonsoft.Json, ClosedXML |
| `BIMPills.UI` | WPF windows and XAML | Core, Commands |
| `BIMPills.Revit` | RevitAPI adapter, ribbon builder | All above + RevitAPI.dll |
| `BIMPills.Core.Tests` | xUnit tests for Core/Commands | Core, Commands, xUnit |

## Building

### All Versions (2024, 2025, 2026)

```bash
cd G:\Claude\Code

# Restore and build for Revit 2026
dotnet restore
dotnet build -p:RevitVersion=2026 -c Release

# Run tests
dotnet test tests/BIMPills.Core.Tests/ -c Release
```

### Multi-Version Build (for installer)

```powershell
$versions = @("2024", "2025", "2026")
foreach ($v in $versions) {
    dotnet build src/BIMPills.Revit/ -p:RevitVersion=$v -c Release
}
```

## Installation

### Manual
1. Build the project for your Revit version
2. Copy `BIMPills.Revit.dll` to: `%APPDATA%/Autodesk/Revit/Addins/{RevitYear}/`
3. Copy `BIMPills.addin` manifest to the same directory
4. Restart Revit

### Installer (InnoSetup)
```bash
# See installer/BIMPills.iss for multi-version packaging
```

## Ribbon Layout

```
Tab: BIMPills
├── Panel: Datos
│   ├── Auditar         [ModelAudit]
│   ├── Exportar        [ExportAudit]
│   ├── Esquemas        [CustomDimensionSchemes]
│   └── Conectar        [MCPIntegration]
├── Panel: Procesos
│   ├── Documentar      [Acotado]
│   └── Gestión         [Gestion]
└── Panel: Información
    └── Acerca de       [About]
```

## Data Persistence

### Locations

All user data is persisted in:
```
%APPDATA%/Autodesk/Revit/Addins/BIMPills/
├── Profiles/reports.json         (Export profiles)
├── Schemes/dimensions.json        (Custom dimension schemas)
└── MCPConnections/connections.json (MCP service connections)
```

### Format

- **JSON** — Human-readable configuration
- **Newtonsoft.Json** — Serialization/deserialization
- **DPAPI** — Credential encryption (MCP connections)

## Enum: MCPConnectionStatus

```csharp
public enum MCPConnectionStatus
{
    Connected,
    Disconnected,
    Error,
    Unknown
}
```

## Testing

```bash
# Run all Core layer tests
dotnet test tests/BIMPills.Core.Tests/ -c Release

# Results: 7 tests passing ✅
# - ModelAuditCommand tests
# - Dimension schema validation
# - Export result validation
```

## Development Notes

### Nullable Reference Types

The project uses nullable reference types (`#nullable enable`). Properties without default values trigger `CS8618` warnings — use `required` keyword or `?` nullable marker.

### Async File Operations

Repository classes use `Task.Run(() => File.ReadAllText())` for netstandard2.0 compatibility (async methods like `File.ReadAllTextAsync` require .NET Core 2.0+).

### Multi-Version Compatibility

- **Revit 2024** → .NET Framework 4.8 (`net48-windows`)
- **Revit 2025+** → .NET 8 (`net8.0-windows`)
- Conditional compilation symbols: `REVIT2024`, `REVIT2025`, `REVIT2026`

## License

Copyright © 2026 BIM-CA. All rights reserved.

---

**Latest Release:** v1.0.0-alpha.2 (2026-03-27)
**Status:** Sprint 2 in progress
