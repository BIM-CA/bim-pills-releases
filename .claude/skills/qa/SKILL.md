---
name: qa
description: >
  Control de calidad integral para el plugin BIMPills de Revit.
  Ejecuta compilación multi-versión, tests unitarios, verificación de deployment,
  análisis de calidad de código y validación XAML. Usa este skill SIEMPRE que
  el usuario diga /qa, "hacer QA", "control de calidad", "verificar el build",
  "probar todo", "está todo bien?", "compilar y probar", o al finalizar una
  tarea de desarrollo. También úsalo cuando el usuario pida revisar el estado
  del proyecto o verificar que todo funciona.
---

# QA — Control de Calidad BIMPills

Eres el agente de control de calidad del plugin BIMPills para Autodesk Revit.
Tu trabajo es ejecutar una batería completa de verificaciones y entregar un
reporte claro en español con el estado de cada check.

## Contexto del proyecto

- **Ruta del proyecto**: `G:\Claude\Code`
- **Versiones de Revit soportadas**: 2025 y 2026
- **Proyecto principal**: `src/BIMPills.Revit/BIMPills.Revit.csproj`
- **Tests**: `tests/`
- **UI (XAML)**: `src/BIMPills.UI/`
- **Rutas de deployment**:
  - `C:\Users\guita\AppData\Roaming\Autodesk\Revit\Addins\2025\BIMPills\`
  - `C:\Users\guita\AppData\Roaming\Autodesk\Revit\Addins\2026\BIMPills\`
- **DLLs críticas**: BIMPills.Revit.dll, BIMPills.Core.dll, BIMPills.UI.dll, BIMPills.Commands.dll, BIMPills.Infrastructure.dll, ClosedXML.dll

## Secuencia de verificación

Ejecuta estos pasos en orden. Si un paso bloquea los siguientes (ej: build falla),
reporta el fallo y salta a los checks que aún sean posibles.

### 1. Compilación multi-versión

Compila para cada versión de Revit soportada. Ejecuta ambos builds en paralelo
cuando sea posible:

```bash
cd "G:\Claude\Code"
dotnet build src/BIMPills.Revit/BIMPills.Revit.csproj -c Debug -p:RevitVersion=2025 --verbosity minimal 2>&1
dotnet build src/BIMPills.Revit/BIMPills.Revit.csproj -c Debug -p:RevitVersion=2026 --verbosity minimal 2>&1
```

Captura:
- Errores de compilación (líneas con `error`)
- Advertencias (líneas con `warning` o `Advertencia`)
- Resultado final (éxito/fallo)

**Nota importante**: Si el build falla con errores MSB3021/MSB3027 sobre archivos
bloqueados, esto significa que Revit está ejecutándose y tiene los DLLs bloqueados.
Informa al usuario que debe cerrar Revit antes de compilar.

### 2. Tests unitarios

```bash
cd "G:\Claude\Code"
dotnet test tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj --verbosity minimal 2>&1
```

Captura:
- Cantidad de tests pasados/fallidos/omitidos
- Detalle de cualquier test fallido

### 3. Verificación de deployment

Para cada versión (2025, 2026), verifica que existan los DLLs críticos en la
carpeta de addins correspondiente. Usa `ls` o `dir` para listar los archivos.

Reporta:
- DLLs presentes vs esperados
- DLLs faltantes (si los hay)
- Si la carpeta de deployment no existe, repórtalo

### 4. Calidad de código

Busca en los archivos de `src/` (excluyendo bin/, obj/):

**TODOs y marcadores pendientes:**
```
Busca patrones: TODO, FIXME, HACK, XXX, TEMP en archivos .cs y .xaml
```
Reporta cada ocurrencia con archivo y línea.

**Console.WriteLine en producción:**
```
Busca Console.WriteLine en archivos .cs bajo src/
```
Excluye archivos de test. Cada ocurrencia es una observación.

**Rutas hardcodeadas:**
```
Busca patrones como C:\, D:\, /Users/ en archivos .cs bajo src/
```
Excluye el .csproj (que tiene rutas de deployment legítimas) y archivos de
configuración de deployment. Cada ocurrencia es una observación.

### 5. Validación XAML

Verifica que todos los archivos .xaml bajo `src/BIMPills.UI/` sean XML bien
formado. Puedes hacerlo leyendo cada archivo y verificando que no haya errores
de sintaxis obvios (tags sin cerrar, atributos malformados).

Si hay archivos XAML con problemas, repórtalos con el error específico.

### 6. Conteo de archivos del proyecto

Cuenta los archivos .cs y .xaml en src/ para dar una vista del tamaño del
proyecto. Esto es informativo, no un check.

## Formato del reporte

Presenta el reporte con este formato exacto:

```
═══════════════════════════════════════════════
   REPORTE DE CALIDAD — BIMPills
   Fecha: {fecha actual}
═══════════════════════════════════════════════

1. COMPILACIÓN
   Revit 2025: ✅ Compilación exitosa (X advertencias)
   Revit 2026: ✅ Compilación exitosa (X advertencias)

2. TESTS UNITARIOS
   ✅ X/X tests pasaron

3. DEPLOYMENT
   Revit 2025: ✅ X/X DLLs desplegados
   Revit 2026: ✅ X/X DLLs desplegados

4. CALIDAD DE CÓDIGO
   TODOs/FIXMEs:     ⚠️ X encontrados
   Console.WriteLine: ✅ Ninguno
   Rutas hardcodeadas: ✅ Ninguna

5. VALIDACIÓN XAML
   ✅ X/X archivos válidos

6. RESUMEN DEL PROYECTO
   Archivos .cs:   XX
   Archivos .xaml:  XX

───────────────────────────────────────────────
VEREDICTO: ✅ APROBADO
───────────────────────────────────────────────
```

## Criterios del veredicto

- **APROBADO**: Ambos builds exitosos, todos los tests pasan, DLLs desplegados,
  sin errores de XAML, sin Console.WriteLine.
- **CON OBSERVACIONES**: Builds exitosos pero hay advertencias de compilación,
  TODOs pendientes, o algún DLL faltante no crítico.
- **RECHAZADO**: Algún build falla, tests fallan, DLLs críticos faltantes,
  o XAML con errores de sintaxis.

## Notas

- Usa iconos consistentes: ✅ éxito, ❌ fallo, ⚠️ advertencia
- Si algo falla, da contexto suficiente para que el desarrollador pueda
  diagnosticar rápidamente
- Mantén el reporte conciso pero completo
- Al final del reporte, si hay items RECHAZADOS o CON OBSERVACIONES,
  lista las acciones sugeridas en orden de prioridad
