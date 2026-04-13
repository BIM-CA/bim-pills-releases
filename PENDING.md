# BIM Pills — Pendientes y Roadmap
> Actualizado: 2026-04-12 · Versión actual: v1.0.0-beta.3.2

---

## 1. Validación del fix PDF24 HKCU (crítico — recién implementado)

El fix `PdfPrinterService.EnsureBimpillsHkcuServiceConfig()` necesita validación real antes de poder considerar cerrado el issue.

### Escenarios a validar en máquina real

- [ ] **Caso base**: usuario admin instala y abre Revit → auto-save funciona sin intervención manual
- [ ] **Caso corporativo**: IT instala con cuenta admin diferente al usuario final → abrir ExportSheets por primera vez aplica el fix (verificar en `pdf-printer-diag.log`)
- [ ] **Verificar log**: confirmar que aparece `EnsureBimpillsHkcuServiceConfig @ ... HKCU written` en `%APPDATA%\Autodesk\Revit\Addins\BIMPills\pdf-printer-diag.log` solo la primera vez
- [ ] **Reentrada**: segunda apertura de ExportSheets no re-escribe (método devuelve `false`)
- [ ] **Orden en NSIS corregido**: con el installer nuevo, `Add-PrinterPort` sucede DESPUÉS del restart de PDF24 → verificar que la impresora "PDF24 (BIMPills)" queda funcional sin scripts de reparación
- [ ] **Eliminar scripts de reparación manual** `installer/fix-pdf24-service.ps1` y `installer/fix-pdf24-final.ps1` una vez validado que el fix automático es suficiente

### Pendiente de decisión

- [ ] ¿Necesita `pdf24-agent.exe` reiniciarse para leer el nuevo HKCU? Si lee config en cada job (probable), no hace falta. Si cachea al arrancar, el usuario debe abrir ExportSheets **antes** de imprimir (ya sucede normalmente). Validar con una sesión real.

---

## 2. Presets de configuración de exportación

`ExportConfigPreset` + `JsonExportConfigPresetRepository` están implementados en Core/Infrastructure y conectados en `ExportSheetsPanel.xaml.cs`, pero **falta validación end-to-end**:

- [ ] Verificar guardar/cargar preset en Revit real (incluyendo `PdfEngine`, `PrinterName`, `DwgConfig`)
- [ ] Tests unitarios para `JsonExportConfigPresetRepository` (Create / Update / Delete / SerializeForExport / DeserializeFromImport)
- [ ] Tests para serialización round-trip de `PdfExportSettings` dentro del preset

---

## 3. Tests ausentes — cobertura pendiente

La cobertura actual cubre comandos Core y licensing. Faltan:

- [ ] `PdfPrinterService.EnsureBimpillsHkcuServiceConfig()` — test que mockee el registry (o use un directorio temporal) y verifique que escribe cuando falta y es no-op cuando ya existe
- [ ] `PdfPrinterService.GetInstalledPdfPrinters()` — test de integración ligero (verifica que no explota, devuelve lista, detecta al menos una impresora en una máquina con impresoras)
- [ ] `SheetNamingConvention.GenerateFileName` — test de tokens `{Param:X}`, caracteres inválidos, valor vacío
- [ ] `JsonExportConfigPresetRepository` — tests CRUD con directorio temporal
- [ ] `JsonPublicationSetRepository` — test round-trip guardar/leer `PublicationSet` con todos los campos (incluyendo nuevos campos del Sprint 6)

---

## 4. Installer — mejoras pendientes

- [ ] **Validar nuevo installer v1.0.0-beta.3.3** (con el fix de orden restart→impresora) en máquina limpia antes de distribuir
- [ ] **Desinstalar scripts de reparación** del repo una vez validado el fix automático: `installer/fix-pdf24-service.ps1`, `installer/fix-pdf24-final.ps1`
- [ ] **`installer/vendor/`**: confirmar que `.gitkeep` es suficiente (el `.gitignore` ya excluye `*.msi`/`*.exe`) — documentar en README cómo obtener `pdf24-creator.msi` antes de compilar el installer
- [ ] **Revisar `installer/qa-check.ps1`**: la ruta hardcodeada `G:\Claude\Code\dist\...` debe ser relativa o parametrizable para que funcione en otras máquinas

---

## 5. Roadmap funcional (Sprint 7+)

Items identificados como próximas funciones según estado del backlog y conversaciones previas:

### Alta prioridad
- [ ] **Presets de exportación en UI**: el backend existe (`ExportConfigPreset`), falta el panel/dropdown en la UI de ExportSheets para guardar/cargar presets con nombre
- [ ] **Exportación NWC (Navisworks)**: `ExportModel` ya tiene panel, falta completar la integración con `NavisworksExportOptions` en Revit API

### Media prioridad
- [ ] **Acotado de vanos exteriores con múltiples esquemas**: actualmente el esquema exterior usa un solo patrón; extender a selección de esquemas igual que los interiores
- [ ] **Filtro de planos por conjunto de publicación en ExportSheets**: actualmente los conjuntos guardan la selección pero no filtran la lista visualmente
- [ ] **Notificación al usuario cuando `EnsureBimpillsHkcuServiceConfig` aplica el fix**: mostrar un toast/banner discreto indicando que la configuración PDF24 fue reparada automáticamente

### Baja prioridad / investigación
- [ ] **MCP Revit → Claude**: revisar estado de la integración MCP (tab Conectar); verificar que `tools/list` y ejecución de herramientas sigue funcionando con versiones recientes del protocol
- [ ] **Health score**: los TODO en `ModelHealthScore.cs` sugieren que la metodología de evaluación tiene ítems incompletos

---

## 6. Deuda técnica

- [ ] `WriteTempDiag()` en `ExportSheetsPanel.xaml.cs` escribe en `%TEMP%` para diagnóstico — evaluar si se puede consolidar con `pdf-printer-diag.log` o eliminarlo en producción
- [ ] Revisar `_suppressPdfEngineEvents = true` hardcoded en el constructor: el comentario indica que debe ser `false` con `//` pero hay un typo (`\ IMPORTANT` en lugar de `// IMPORTANT`) en la línea 42
- [ ] `.claude/launch.json` modificado — verificar si los cambios son intencionales o ruido
- [ ] El worktree `bimca/vibrant-hawking` (branch del agent SDK) tiene skills y agents modificados sin commitear — decidir si se integran o descartan

---

## 7. Distribución

- [ ] **Próxima release**: v1.0.0-beta.3.3 con el fix PDF24 HKCU — requiere rebuild del installer NSIS con el script actualizado
- [ ] Actualizar `installer/releases/2026-04-12/README.txt` mencionando el fix automático de HKCU
- [ ] Subir a canal de distribución de BIM-CA una vez validado en máquina real

---

_Este archivo es mantenido manualmente. Moverlo o renombrarlo requiere actualizar cualquier referencia en MEMORY.md._
