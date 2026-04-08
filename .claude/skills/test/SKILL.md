---
name: test
description: >
  Ejecuta los tests unitarios del plugin BIMPills y reporta los resultados.
  Usa este skill cuando el usuario diga /test, "correr tests", "ejecutar tests",
  "probar", "run tests", "verificar tests", o cuando quiera saber si los tests pasan.
---

# /test — Ejecutar Tests Unitarios de BIMPills

Eres el runner de tests del plugin BIMPills para Autodesk Revit. Tu trabajo es
ejecutar la suite de tests, interpretar los resultados y sugerir correcciones si
hay fallos.

## Contexto del proyecto

- **Ruta**: `G:\Claude\Code`
- **Proyecto de tests**: `tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj`
- **Framework**: xUnit, net8.0
- **Infraestructura de testing**:
  - `FakeCommandContext` — implementa ICommandContext con fakes
  - `FakeDocumentServices` — implementa IDocumentServices con datos configurables
  - `NullLogger` — ILogger no-op para tests
  - Ubicadas en: `tests/BIMPills.Core.Tests/ModelAudit/ModelAuditCommandTests.cs`

## Paso 1 — Ejecutar tests

**Todos los tests:**
```bash
cd "G:\Claude\Code"
dotnet test tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj --verbosity normal 2>&1
```

**Filtrar por feature** (si el usuario especifica):
```bash
dotnet test tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj --filter "FullyQualifiedName~{Feature}" --verbosity normal 2>&1
```

**Filtrar por nombre de test específico:**
```bash
dotnet test tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj --filter "DisplayName~{NombreTest}" --verbosity normal 2>&1
```

## Paso 2 — Parsear resultados

Extraer del output:
- Total de tests
- Tests pasados (Passed)
- Tests fallidos (Failed)
- Tests omitidos (Skipped)
- Tiempo de ejecución

Para cada test fallido, extraer:
- Nombre completo del test
- Mensaje de error (Expected vs Actual)
- Stack trace (primera línea relevante: archivo + línea)

## Paso 3 — Diagnosticar fallos (si hay)

Si hay tests fallidos:

1. **Leer el archivo del test** que falló para entender qué se espera
2. **Leer el archivo del comando** correspondiente para ver la implementación
3. **Identificar la causa**: ¿cambió la interfaz? ¿cambió la lógica? ¿los fakes están desactualizados?
4. **Sugerir la corrección** específica

**Causas comunes de fallos:**
- Se agregó un método a `IDocumentServices` pero no se implementó en `FakeDocumentServices`
- Se cambió la firma de un modelo/POCO pero los tests usan la firma anterior
- Se cambió la lógica del comando y los asserts ya no son válidos
- `ServiceLocator` no fue reseteado entre tests (usar `ServiceLocator.Reset()`)

## Paso 4 — Reportar

```
TEST REPORT — BIMPills
═══════════════════════════════════════
Total: XX | Passed: XX | Failed: XX | Skipped: XX
Tiempo: X.Xs
═══════════════════════════════════════

✅ ModelAuditCommandTests (4 tests)
✅ AboutCommandTests (2 tests)
✅ OrderingCommandTests (X tests)
✅ DataManagerCommandTests (X tests)
✅ AcotadoVanosTests (X tests)
✅ RibbonDuplicatesTests (X tests)

═══════════════════════════════════════
RESULTADO: ✅ TODOS LOS TESTS PASAN
═══════════════════════════════════════
```

**Si hay fallos:**
```
❌ FAILED: {Namespace}.{TestClass}.{TestMethod}
   Expected: {valor esperado}
   Actual:   {valor actual}
   at {archivo}:{linea}

   Sugerencia: {descripción de qué corregir y dónde}
```

## Archivos de referencia

- `tests/BIMPills.Core.Tests/ModelAudit/ModelAuditCommandTests.cs` — Tests + test doubles (FakeCommandContext, FakeDocumentServices, NullLogger)
- `tests/BIMPills.Core.Tests/About/AboutCommandTests.cs`
- `tests/BIMPills.Core.Tests/Ordering/OrderingCommandTests.cs`
- `tests/BIMPills.Core.Tests/DataManager/DataManagerCommandTests.cs`
- `tests/BIMPills.Core.Tests/Documentacion/AcotadoVanosTests.cs`
- `tests/BIMPills.Core.Tests/Ribbon/RibbonDuplicatesTests.cs`
