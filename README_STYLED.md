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

**Versión**: 1.0.0-beta.7.3 &nbsp;·&nbsp; **Desarrollador**: BIM-CA

---

## Novedades en beta.7.3

- **Exportar Planos — Conjuntos**: los conjuntos de publicación ahora aparecen en un ComboBox. Nuevo botón **MIX** para combinar múltiples conjuntos en una sola exportación.
- **Seleccionar — parámetros TIPO/EJEMPLAR**: las etiquetas ahora muestran el texto completo "TIPO" y "EJEMPLAR" con colores contrastantes (púrpura y azul) en lugar de solo la letra.
- **Auditar — Purga mejorada**: la purga de elementos es ahora más robusta. Si algún elemento no puede eliminarse, el diálogo muestra el nombre y la razón exacta de Revit para cada fallo en lugar de un mensaje genérico.
- **Correcciones en Seleccionar**: varios bugs en la asignación de subproyectos y el modal de valores.

## Novedades en beta.7.2

- **Tablas — Elementos de vínculos**: nuevo checkbox "Incluir elementos de vínculos" para mostrar elementos de archivos Revit vinculados. Las filas de vínculos se muestran en gris con columna Origen en el Excel exportado.
- **Dibujar Tabla**: renombrada desde "Leyenda Excel" con nota de tipo de vista compatible.
- **Notas Clave — codificación ANSI**: los archivos `.txt` se leen y guardan en Windows-1252 (ANSI), garantizando compatibilidad con Revit y soporte correcto de caracteres en español (tildes, ñ, ü).
- **Notas Clave — selección de texto**: corregido conflicto con drag-and-drop que impedía seleccionar texto en la columna Descripción. El arrastre ahora solo se activa desde el handle (≡).

---

## Funciones

### Auditar
Auditoría de modelo BIM con puntuación de salud. Analiza familias, advertencias, materiales y otros indicadores de calidad. Exporta reportes HTML.

### Exportar

Ventana unificada con tres pestañas:

- **Planos y Vistas**: exportación por lotes de planos y vistas a PDF y/o DWG. Motor PDF configurable (nativo Revit o impresora del sistema). Conjuntos de publicación guardables. Filtro por tipo (planos / vistas / todos). Navegación guiada por pasos.
- **Modelo**: exportación del modelo completo a NWC (Navisworks) con opciones de alcance, coordenadas, parámetros y precisión de facetado.
- **Familias**: exportación masiva de familias a archivos `.rfa`, organizadas por categoría.

### Documentar
- **Acotado automático**: dimensionamiento automático de vanos interiores con esquemas de acotado personalizables por disciplina.

### Gestionar
- **Tablas**: exportar tablas de planificación a Excel, editar en lote e importar cambios de vuelta al modelo.
- **Notas Clave**: editor visual de archivos de notas clave con jerarquía, drag-and-drop, importación/exportación Excel. Compatible con archivos locales y Autodesk Docs (Desktop Connector).
- **Subproyectos**: crear y renombrar subproyectos (worksets). Generar vistas 3D por subproyecto con visibilidad automática.
- **Trasladar Estándares**: transferencia selectiva de estándares desde otros modelos abiertos (estilos de cota, notas de texto, estilos de línea, tipos de muro/piso/techo, patrones de relleno, cotas puntuales).

### Soporte
Chat en vivo con el equipo de BIM-CA directamente desde Revit. Accede desde **BIM Pills → Soporte** en el ribbon.

### Conectar (MCP)
Integración con servidores MCP para conectar Revit con herramientas de IA.

### Ordenar
Numeración incremental interactiva de elementos con prefijos, pasos y sufijos configurables.

---

## Instalación

### Opción 1: Installer (recomendado)
1. Ejecuta `BIMPills-beta-7.3-Setup.exe`
2. Selecciona las versiones de Revit a instalar (2024, 2025, 2026, 2027)
3. Opcionalmente instala **PDF24 Creator** — necesario para exportación PDF silenciosa
4. **WebView2 Runtime** se instala automáticamente si no está presente (necesario para el chat de soporte)
5. Abre Revit y busca la pestaña **BIM Pills** en el ribbon

> Si ya tienes PDF24 instalado, el installer lo detecta y configura automáticamente.

### Opción 2: Manual
1. Cierra Revit completamente
2. Copia la carpeta `BIMPills/` y el archivo `BIMPills.addin` de la versión correspondiente a:
   ```
   %APPDATA%\Autodesk\Revit\Addins\{version}\
   ```
3. Abre Revit

---

## Motor PDF

BIM Pills soporta dos motores de exportación PDF:

| Motor | Ventajas | Notas |
|-------|----------|-------|
| **Nativo Revit** | Sin dependencias, tamaño de hoja siempre correcto | Recomendado como motor principal |
| **PDF24 / Impresora del sistema** | Máxima fidelidad de líneas en planos complejos | PDF24 se redirige automáticamente al motor nativo para garantizar el tamaño de hoja |

El motor se configura desde **Exportar → Planos y Vistas → ícono de configuración**.

---

## Licencias

BIM Pills requiere una licencia activa. Al abrir Revit por primera vez, activa tu licencia desde **BIM Pills → Acerca de → Activar licencia**.

Adquiere tu licencia en [bim-ca.com](https://bim-ca.com).

---

## Soporte

Usa el botón **Soporte** en el ribbon de BIM Pills para abrir el chat en vivo con el equipo de BIM-CA.

- [Reportar un problema o sugerencia](https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41?pvs=105)
- Email: support@bim-ca.com

---

© 2026 BIM-CA. Todos los derechos reservados.
