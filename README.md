```
  ─────────────────────────────────────────────────────────────────
  ██████╗ ██╗███╗   ███╗    ██████╗ ██╗██╗     ██╗     ███████╗
  ██╔══██╗██║████╗ ████║    ██╔══██╗██║██║     ██║     ██╔════╝
  ██████╔╝██║██╔████╔██║    ██████╔╝██║██║     ██║     ███████╗
  ██╔══██╗██║██║╚██╔╝██║    ██╔═══╝ ██║██║     ██║     ╚════██║
  ██████╔╝██║██║ ╚═╝ ██║    ██║     ██║███████╗███████╗███████║
  ╚═════╝ ╚═╝╚═╝     ╚═╝    ╚═╝     ╚═╝╚══════╝╚══════╝╚══════╝
  Plugin de Productividad para Autodesk Revit  ·  BIM-CA
  Revit 2024 · 2025 · 2026 · 2027
  ─────────────────────────────────────────────────────────────────
```

**Versión**: 1.0.0-beta.7.7 &nbsp;·&nbsp; **Desarrollador**: BIM-CA

---

## Novedades en beta.7.7

- **Exportar Planos — PDF combinado**: el nombre del archivo combinado se define ahora en el Paso 3 (Exportar), no en el Paso 2. Soporta tokens de proyecto: `{ProjectName}` y `{Date}`.
- Al activar «Combinar en un solo PDF», el nombramiento por plano individual se oculta automáticamente — no aplica cuando todo va en un solo archivo.
- Al desactivar PDF con «Combinar» activo, la sección de nombre combinado desaparece correctamente.

## Novedades en beta.7.6

- **Iconos HiDPI**: el ribbon carga iconos a mayor resolución en pantallas de alta densidad — 64×64 px a 125–175% de escala y 128×128 px a 200%+. Los iconos se ven nítidos en monitores 4K y pantallas Retina vía Parallels.
- **Impartir — Plantillas de vista**: al reemplazar una plantilla existente, las vistas que la tenían asignada conservan su plantilla automáticamente. Antes quedaban sin plantilla tras el reemplazo.

## Novedades en beta.7.5

- **Auditar — Purga segura**: detección exhaustiva de familias en uso. Escanea parámetros de tipo entero Y ElementId en todos los tipos de elemento — cubre secciones, alzados, cotas, barandillas, muros cortina y cualquier tipo presente o futuro en Revit.
- **Auditar — Uniones MEP**: las familias de uniones de tubería, ducto, bandeja y conduit configuradas en preferencias de enrutamiento ya no aparecen como purgables aunque no tengan instancias colocadas.
- **Auditar — Perfiles de barandilla**: las familias de perfil están excluidas permanentemente — sus referencias son internas de Revit.
- **Auditar — Confirmación mejorada**: el diálogo de purga muestra los nombres de los elementos a eliminar (máx. 8). Ctrl+Z en Revit funciona mientras el modelo no se haya guardado.
- **Exportar Planos — Nomenclatura**: los parámetros de proyecto están disponibles como tokens en la nomenclatura de archivos exportados.

## Novedades en beta.7.4

- **Auditar — Purga batch+binary-split**: una transacción por lote en vez de una por elemento. Más rápido en modelos grandes.
- **Auditar — Nuevos purgables**: plantillas de vista, filtros de vista, estilos de texto, tipos de cota y regiones rellenas.
- **Auditar — Ventana de progreso** durante el análisis con indicador de fase y barra de porcentaje.

## Novedades en beta.7.3

- **Exportar Planos — Conjuntos MIX**: los conjuntos de publicación aparecen en ComboBox. Nuevo botón **MIX** para combinar múltiples conjuntos en una sola exportación.
- **Seleccionar — badges TIPO/EJEMPLAR**: etiquetas con texto completo y colores contrastantes (púrpura y azul).
- **Auditar — Purga mejorada**: el diálogo muestra el nombre y razón exacta de Revit para cada fallo.

## Novedades en beta.7.2

- **Tablas — Elementos de vínculos**: nuevo checkbox para mostrar elementos de archivos Revit vinculados. Filas en gris con columna Origen en Excel.
- **Dibujar Tabla**: renombrada desde «Leyenda Excel» con nota de tipo de vista compatible.
- **Notas Clave — codificación ANSI**: archivos `.txt` en Windows-1252 para compatibilidad con Revit y caracteres en español.
- **Notas Clave — selección de texto**: corregido conflicto con drag-and-drop en columna Descripción.

---

## Funciones

### Auditar
Auditoría de modelo BIM con puntuación de salud. Analiza familias, advertencias, materiales y otros indicadores de calidad. Exporta reportes HTML.

### Exportar

Ventana unificada con tres pestañas:

- **Planos y Vistas**: exportación por lotes de planos y vistas a PDF y/o DWG. Motor PDF configurable (nativo Revit o impresora del sistema). Conjuntos de publicación guardables. Filtro por tipo. Navegación guiada por pasos.
- **Modelo**: exportación del modelo completo a NWC (Navisworks).
- **Familias**: exportación masiva de familias a archivos `.rfa`, organizadas por categoría.

### Documentar
- **Acotado automático**: dimensionamiento automático de vanos interiores con esquemas personalizables por disciplina.

### Gestionar
- **Tablas**: exportar tablas de planificación a Excel, editar en lote e importar cambios de vuelta al modelo.
- **Notas Clave**: editor visual con jerarquía, drag-and-drop, importación/exportación Excel.
- **Subproyectos**: crear y renombrar worksets. Generar vistas 3D por subproyecto.
- **Trasladar Estándares**: transferencia selectiva de estándares desde otros modelos abiertos.

### Soporte
Chat en vivo con el equipo de BIM-CA directamente desde Revit.

### Conectar (MCP)
Integración con servidores MCP para conectar Revit con herramientas de IA.

### Ordenar
Numeración incremental interactiva de elementos con prefijos, pasos y sufijos configurables.

---

## Instalación

### Opción 1: Installer (recomendado)
1. Descarga el instalador desde [Releases](https://github.com/BIM-CA/bim-pills-releases/releases/latest)
2. Selecciona las versiones de Revit a instalar (2024, 2025, 2026, 2027)
3. Opcionalmente instala **PDF24 Creator** — necesario para exportación PDF silenciosa
4. **WebView2 Runtime** se instala automáticamente si no está presente
5. Abre Revit y busca la pestaña **BIM Pills** en el ribbon

### Opción 2: Manual
1. Cierra Revit completamente
2. Copia la carpeta `BIMPills/` y el archivo `BIMPills.addin` a:
   ```
   %APPDATA%\Autodesk\Revit\Addins\{version}\
   ```
3. Abre Revit

---

## Motor PDF

| Motor | Ventajas |
|-------|----------|
| **Nativo Revit** | Sin dependencias, tamaño de hoja siempre correcto. Recomendado. |
| **PDF24 / Impresora del sistema** | Máxima fidelidad de líneas en planos complejos. |

---

## Licencias

BIM Pills requiere una licencia activa. Activa tu licencia desde **BIM Pills → Acerca de → Activar licencia**.

Adquiere tu licencia en [bim-ca.com](https://bim-ca.com).

---

## Soporte

- Chat en vivo desde **BIM Pills → Soporte** en el ribbon
- [Reportar un problema](https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41?pvs=105)
- Email: support@bim-ca.com

---

© 2026 BIM-CA. Todos los derechos reservados.
