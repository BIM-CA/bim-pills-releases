---
name: new-command
description: >
  Genera el scaffolding completo para un nuevo comando del plugin BIMPills
  siguiendo la arquitectura de 4 capas. Usa este skill SIEMPRE que el usuario
  diga /new-command, "nuevo comando", "agregar comando", "crear comando",
  "nueva funcionalidad", "scaffold", o cuando describa una nueva feature
  que necesita un comando Revit.
---

# /new-command — Scaffold de Nuevo Comando BIMPills

Eres el arquitecto de comandos del plugin BIMPills para Autodesk Revit. Tu trabajo
es generar la estructura completa de un nuevo comando siguiendo estrictamente la
arquitectura de 4 capas establecida.

## Arquitectura del proyecto

```
src/
  BIMPills.Core/           ← Interfaces, POCOs, modelos (netstandard2.0, SIN RevitAPI)
  BIMPills.Commands/       ← Lógica de negocio (netstandard2.0, SIN RevitAPI)
  BIMPills.Infrastructure/ ← ServiceLocator, FileLogger, persistencia JSON
  BIMPills.Revit/          ← Adaptadores Revit, entry point (net48/net8.0)
  BIMPills.UI/             ← Ventanas WPF (usa /new-window para crear UI)
tests/
  BIMPills.Core.Tests/     ← Tests unitarios xUnit
```

## Paso 0 — Recopilar información

Antes de generar código, obtén del usuario:

1. **Nombre de la feature** (PascalCase, ej: `WallAnalysis`)
2. **Panel del ribbon**: `Datos`, `Procesos`, o `Informacion`
3. **Etiqueta del botón** (español, ej: `Analizar Muros`)
4. **Tooltip** (1 frase en español)
5. **¿Necesita datos de Revit?** (la mayoría sí → nuevos métodos en IDocumentServices)
6. **¿Necesita ventana UI?** (la mayoría sí → delegar a /new-window)
7. **iconKey** (string para RibbonIconFactory, ej: `wall-analysis`)

Si el usuario ya proporcionó contexto suficiente en su mensaje, no preguntes de nuevo.

## Paso 1 — Core Layer: Modelos y datos

Crear en `src/BIMPills.Core/{Feature}/`:

**Modelos de datos** (POCOs para los datos que el comando producirá):
```csharp
// src/BIMPills.Core/{Feature}/{Feature}Data.cs (o nombre descriptivo)
namespace BIMPills.Core.{Feature}
{
    public sealed class {Feature}Info
    {
        // Propiedades del dominio
    }
}
```

**Si el comando necesita datos de Revit**, agregar métodos a `IDocumentServices`:
- Archivo: `src/BIMPills.Core/Services/IDocumentServices.cs`
- Solo agregar métodos nuevos al final de la interfaz
- Los métodos retornan `IReadOnlyList<T>`, `int`, `long`, `bool`, o `string`

**IMPORTANTE**: Core NO debe referenciar RevitAPI.dll ni ningún paquete externo.

## Paso 2 — Commands Layer: Comando + Módulo

### 2a. Comando

Crear `src/BIMPills.Commands/{Feature}/{Feature}Command.cs`:

```csharp
using BIMPills.Core.Commands;
using BIMPills.Core.{Feature}; // si hay modelos en Core

namespace BIMPills.Commands.{Feature}
{
    public sealed class {Feature}Command : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            var doc = context.Document;
            context.Logger.Info("Iniciando {descripción}...");

            // Lógica de negocio usando doc.GetXxx()
            // NUNCA usar RevitAPI directamente aquí

            var result = new {Feature}Result
            {
                // Poblar con datos del documento
            };

            context.Logger.Info("{Feature} completado.");
            LastResult = result;
            return CommandResult.Ok("{Mensaje de éxito}");
        }

        public static {Feature}Result? LastResult { get; private set; }
    }

    public sealed class {Feature}Result
    {
        // Propiedades que la UI necesitará mostrar
        // Usar IReadOnlyList<T> para colecciones
    }
}
```

**Patrón obligatorio**:
- Implementar `IPluginCommand`
- Propiedad estática `LastResult` (la usa el RevitCommand para pasar datos a la UI)
- Retornar `CommandResult.Ok(msg)` o `CommandResult.Fail(msg)`
- Usar `context.Document` para acceder a datos, `context.Logger` para logging

### 2b. Módulo

Crear `src/BIMPills.Commands/{Feature}/{Feature}Module.cs`:

```csharp
using BIMPills.Core.Modules;
using System.IO;
using System.Reflection;

namespace BIMPills.Commands.{Feature}
{
    public sealed class {Feature}Module : IPluginModule
    {
        public string TabName   => "BIMPills";
        public string PanelName => "{Panel}";  // "Datos", "Procesos", o "Informacion"

        public void BuildRibbon(IRibbonBuilder builder)
        {
            var revitDll = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "BIMPills.Revit.dll");

            builder.AddPushButton(
                tabName:             TabName,
                panelName:           PanelName,
                buttonName:          "{Etiqueta del botón}",
                tooltip:             "{Tooltip en español}",
                commandTypeFullName: "BIMPills.Revit.Commands.{Feature}.{Feature}RevitCommand",
                assemblyPath:        revitDll,
                iconKey:             "{iconKey}");
        }
    }
}
```

## Paso 3 — Revit Adapter Layer

Crear `src/BIMPills.Revit/Commands/{Feature}/{Feature}RevitCommand.cs`:

```csharp
using Autodesk.Revit.DB;
using BIMPills.Commands.{Feature};
using BIMPills.Core.Commands;
using BIMPills.Revit.Commands;

namespace BIMPills.Revit.Commands.{Feature}
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class {Feature}RevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new {Feature}Command();

        protected override void OnSuccess(IPluginCommand command)
        {
            if ({Feature}Command.LastResult == null) return;

            // Opción A: Sin ventana UI — mostrar TaskDialog simple
            Autodesk.Revit.UI.TaskDialog.Show(
                "BIMPills — {Feature}",
                {Feature}Command.LastResult.ToString());

            // Opción B: Con ventana UI (implementar con /new-window)
            // new {Feature}Window({Feature}Command.LastResult).ShowDialog();
        }
    }
}
```

**Si el comando necesita Transaction de Revit** (crear/modificar elementos), usar:
```csharp
var doc = CommandData?.Application.ActiveUIDocument.Document;
using (var trans = new Transaction(doc, "BIMPills - {Descripción}"))
{
    trans.Start();
    // ... operaciones Revit ...
    trans.Commit();
}
```

**Si el comando necesita implementar métodos nuevos de IDocumentServices**, agregar
la implementación en `src/BIMPills.Revit/Context/RevitDocumentServices.cs`. Usar
`#if REVIT2024` / `#if REVIT2025` / `#if REVIT2026` para diferencias entre versiones.

## Paso 4 — Registrar módulo

En `src/BIMPills.Revit/Application/RevitApplication.cs`, método `GetModules()`:

```csharp
// Agregar el using al inicio del archivo:
using BIMPills.Commands.{Feature};

// En GetModules(), bajo el panel correspondiente:
yield return new {Feature}Module();
```

**Ubicación en GetModules() según panel**:
- Panel "Datos" → después de `DataManagerModule`
- Panel "Procesos" → después de `GestionModule`
- Panel "Información" → después de `AboutModule`

## Paso 5 — Tests unitarios

Crear `tests/BIMPills.Core.Tests/{Feature}/{Feature}CommandTests.cs`:

```csharp
using BIMPills.Commands.{Feature};
using BIMPills.Core.Tests.ModelAudit; // FakeCommandContext, FakeDocumentServices, NullLogger
using Xunit;

namespace BIMPills.Core.Tests.{Feature}
{
    public class {Feature}CommandTests
    {
        [Fact]
        public void Execute_ReturnsSuccess()
        {
            // Arrange — si el comando necesita datos específicos,
            // agregar propiedades a FakeDocumentServices
            var context = new FakeCommandContext(new FakeDocumentServices());

            // Act
            var command = new {Feature}Command();
            var result = command.Execute(context);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull({Feature}Command.LastResult);
        }

        [Fact]
        public void Execute_EmptyData_HandlesGracefully()
        {
            var context = new FakeCommandContext(new FakeDocumentServices());
            var result = new {Feature}Command().Execute(context);

            Assert.True(result.Success);
        }
    }
}
```

**Si se agregaron métodos nuevos a IDocumentServices**, actualizar `FakeDocumentServices`
en `tests/BIMPills.Core.Tests/ModelAudit/ModelAuditCommandTests.cs` con implementaciones
fake de los métodos nuevos.

## Paso 6 — Verificación

Ejecutar en secuencia:

```bash
# 1. Build para Revit 2026
dotnet build src/BIMPills.Revit/BIMPills.Revit.csproj -c Debug -p:RevitVersion=2026 --verbosity minimal

# 2. Tests
dotnet test tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj --verbosity minimal
```

Ambos deben pasar sin errores. Advertencias son aceptables.

## Paso 7 — Output

Al finalizar, presenta un resumen:

```
NUEVO COMANDO: {Feature}
═══════════════════════════════════════
Archivos creados:
  ✅ src/BIMPills.Core/{Feature}/{Feature}Info.cs
  ✅ src/BIMPills.Commands/{Feature}/{Feature}Command.cs
  ✅ src/BIMPills.Commands/{Feature}/{Feature}Module.cs
  ✅ src/BIMPills.Revit/Commands/{Feature}/{Feature}RevitCommand.cs
  ✅ tests/BIMPills.Core.Tests/{Feature}/{Feature}CommandTests.cs

Archivos modificados:
  ✅ src/BIMPills.Revit/Application/RevitApplication.cs (GetModules)
  ✅ src/BIMPills.Core/Services/IDocumentServices.cs (si aplica)
  ✅ tests/.../FakeDocumentServices (si aplica)

Build: ✅ Compilación exitosa
Tests: ✅ X/X tests pasaron

Siguiente paso:
  → Usa /new-window para crear la ventana UI de {Feature}
```

## Reglas estrictas

1. **NUNCA** agregar `using Autodesk.Revit.*` en BIMPills.Core o BIMPills.Commands
2. **NUNCA** crear la ventana UI aquí — delegar a `/new-window`
3. **SIEMPRE** seguir el patrón `static LastResult` para pasar datos a la UI
4. **SIEMPRE** escribir al menos 2 tests unitarios
5. **SIEMPRE** registrar el módulo en `GetModules()`
6. **SIEMPRE** verificar build y tests al final
