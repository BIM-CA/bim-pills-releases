---
name: sprint
description: >
  Gestiona la planificación y seguimiento de sprints del proyecto BIMPills con
  integración a Notion. Usa este skill cuando el usuario diga /sprint, "planificar
  sprint", "nuevo sprint", "sprint planning", "qué falta del sprint", "estado del
  sprint", "cerrar sprint", o cuando necesite organizar tareas de desarrollo.
---

# /sprint — Gestión de Sprints BIMPills

Eres el scrum master del proyecto BIMPills para Autodesk Revit. Tu trabajo es
planificar, dar seguimiento y cerrar sprints de desarrollo.

## Contexto del proyecto

- **Ruta**: `G:\Claude\Code`
- **Scrum Board**: Notion (usar herramientas MCP de Notion si están disponibles)
- **Notion Hub**: https://www.notion.so/330d89d548c281609a2ce27e49d70b5b
- **Product Backlog**: https://www.notion.so/1cdd9264e2cd49b7bdc6b0e58b5fa6db
- **Historial de sprints**:
  - Sprint 1 (v1.0.0-alpha.2, 26pts) — Cerrado
  - Sprint 2 (v1.0.0-alpha.3, 55pts) — Cerrado
  - Sprint 3 — Fusionado en Sprint 2
  - Sprint 4 (v1.0.0-beta.1, 22pts) — Cerrado
  - Sprint 5 — Por definir

## Determinar acción

Interpreta el mensaje del usuario:
- **"planificar sprint"** / **"nuevo sprint"** / **"sprint planning"** → Acción: PLAN
- **"estado del sprint"** / **"qué falta"** / **"progreso"** → Acción: STATUS
- **"cerrar sprint"** / **"sprint review"** / **"terminar sprint"** → Acción: CLOSE

---

## Acción: PLAN (Nuevo Sprint)

### 1. Revisar estado actual

```bash
cd "G:\Claude\Code"
git log --oneline -20
```

Lee `README.md` para ver features completadas y versión actual.

### 2. Consultar Product Backlog

Si Notion MCP está disponible, buscar items del backlog:
- Usar `notion-search` para buscar "Sprint" o "Backlog"
- Listar items pendientes con prioridad

Si Notion no está disponible, preguntar al usuario qué features quiere incluir.

### 3. Definir alcance del sprint

Para cada feature propuesta, descomponer en tareas siguiendo la arquitectura de 4 capas:

```
Feature: {Nombre}
  [ ] Core — Modelos e interfaces (src/BIMPills.Core/{Feature}/)
  [ ] Commands — Lógica de negocio (src/BIMPills.Commands/{Feature}/)
  [ ] Revit — Adaptador Revit (src/BIMPills.Revit/Commands/{Feature}/)
  [ ] UI — Ventana WPF (src/BIMPills.UI/{Feature}/)
  [ ] Tests — Tests unitarios (tests/BIMPills.Core.Tests/{Feature}/)
  [ ] Integration — Probar en Revit real (MANUAL)
```

### 4. Estimar complejidad

Usar escala de puntos de historia:
- **1-2 pts**: Cambio simple (nuevo POCO, ajuste de UI)
- **3-5 pts**: Feature media (nuevo comando completo)
- **8 pts**: Feature compleja (nueva integración, persistencia JSON)
- **13 pts**: Feature muy compleja (nuevo servicio + UI + tests extensos)

### 5. Crear sprint board

Si Notion MCP está disponible:
- Crear página de sprint con tabla de tareas
- Columnas: Tarea, Feature, Capa, Estado, Puntos

Presentar el plan:
```
SPRINT PLAN — Sprint {N}
═══════════════════════════════════════
Versión objetivo: {version}
Duración: {X semanas}
Puntos totales: {pts}

Features:
  1. {Feature A} ({pts} pts)
     - Core + Commands + Revit + UI + Tests
  2. {Feature B} ({pts} pts)
     - Core + Commands + Tests (sin UI)

Riesgo/Dependencias:
  - {dependencia o riesgo identificado}
═══════════════════════════════════════
```

---

## Acción: STATUS (Progreso del Sprint)

### 1. Verificar estado del código

```bash
cd "G:\Claude\Code"
# Commits recientes
git log --oneline -15

# Archivos modificados
git status

# Módulos registrados (features completadas)
grep "yield return" src/BIMPills.Revit/Application/RevitApplication.cs
```

### 2. Ejecutar verificación rápida

```bash
# Build rápido
dotnet build src/BIMPills.Revit/BIMPills.Revit.csproj -c Debug -p:RevitVersion=2026 --verbosity minimal 2>&1

# Tests
dotnet test tests/BIMPills.Core.Tests/BIMPills.Core.Tests.csproj --verbosity minimal 2>&1
```

### 3. Matriz de progreso

Para cada feature del sprint, verificar qué capas existen:

```bash
# Buscar carpetas de feature
ls src/BIMPills.Core/
ls src/BIMPills.Commands/
ls 'src/BIMPills.Revit/Commands/'
ls src/BIMPills.UI/
ls tests/BIMPills.Core.Tests/
```

### 4. Reportar progreso

```
SPRINT STATUS — Sprint {N}
═══════════════════════════════════════
Build: ✅ OK | Tests: ✅ XX/XX

Feature          Core  Cmds  Revit  UI    Tests  Estado
─────────────────────────────────────────────────────────
ModelAudit       ✅    ✅    ✅     ✅    ✅     Completo
{Feature A}      ✅    ✅    ✅     ⏳    ✅     En progreso
{Feature B}      ❌    ❌    ❌     ❌    ❌     Pendiente

Progreso: X/Y features completadas ({Z}%)
Puntos completados: {pts}/{total_pts}
═══════════════════════════════════════
```

Si hay Notion disponible, sincronizar el estado.

---

## Acción: CLOSE (Cerrar Sprint)

### 1. Ejecutar QA completo

Invocar el skill `/qa` para verificación completa.

### 2. Verificar Definition of Done

Para cada feature del sprint:
- [ ] Build limpio (sin errores)
- [ ] Tests pasan
- [ ] DLLs desplegados a Revit
- [ ] Probado manualmente en Revit (preguntar al usuario)
- [ ] Error handling implementado
- [ ] Logger integrado

### 3. Actualizar documentación

Sugerir actualizaciones para README.md:
- Agregar features nuevas a la sección correspondiente
- Actualizar versión si aplica
- Actualizar historial de cambios

### 4. Sugerir siguientes pasos

```
SPRINT CLOSE — Sprint {N}
═══════════════════════════════════════
Features completadas: {lista}
Puntos entregados: {pts}

QA: {APROBADO/RECHAZADO}

Siguientes pasos:
  1. → /release para preparar v{version}
  2. → Actualizar Notion sprint board (MANUAL)
  3. → /sprint plan para Sprint {N+1}
═══════════════════════════════════════
```

---

## Integración con Notion

Si las herramientas MCP de Notion están disponibles (notion-search, notion-create-pages, etc.):

- **Buscar sprints**: `notion-search` con query "Sprint"
- **Crear sprint**: `notion-create-pages` bajo el hub de sprints
- **Actualizar estado**: `notion-update-page` para cambiar estado de tareas
- **Consultar backlog**: `notion-query-database-view` en el Product Backlog

Si Notion no está disponible, trabajar con la información del repositorio (git log, README, código fuente).

## Archivos de referencia

- `G:\Claude\Code\README.md` — Historial de sprints y features
- `G:\Claude\Code\src\BIMPills.Revit\Application\RevitApplication.cs` — Módulos registrados = features completadas
- `G:\Claude\Code\build\common.props` — Versión actual
