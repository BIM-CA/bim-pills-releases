<div align="center">
  <img src="assets/logo.png?v=2" alt="BIM Pills" width="160"/>
  <h3>Plugin de Productividad para Autodesk Revit</h3>
  <p>
    <a href="../../releases/latest"><img src="https://img.shields.io/badge/versi%C3%B3n-1.2.2%20%C2%ABCafe%C3%ADna%C2%BB-EF6337?style=for-the-badge" alt="Versión"/></a>
    <img src="https://img.shields.io/badge/Revit-2024%20%7C%202025%20%7C%202026%20%7C%202027-0696D7?style=for-the-badge" alt="Revit Versions"/>
    <img src="https://img.shields.io/badge/plataforma-Windows-1A1A2E?style=for-the-badge" alt="Windows"/>
  </p>
</div>

---

## ¿Qué es BIM Pills?

BIM Pills es un plugin para Autodesk Revit que reúne en un solo lugar las herramientas más útiles del día a día en proyectos BIM. Diseñado para equipos que trabajan con estándares de calidad, flujos de exportación y gestión eficiente de datos del modelo.

---

## Funcionalidades

### 🔍 Auditar
Auditoría del modelo BIM con puntuación de salud. Analiza familias, advertencias, materiales y otros indicadores de calidad. Incluye:
- **Purga segura y rápida** — detección exhaustiva de familias en uso (parámetros enteros, ElementId, uniones MEP, perfiles de barandilla); eliminación en lote en segundos, con reporte fiel de lo eliminado y el motivo de cada elemento omitido
- **Confirmación con nombres** — el diálogo muestra los elementos a eliminar antes de purgar
- **Ventana de progreso** con fases e indicadores
- **Reporte HTML** exportable

### 📤 Exportar
Ventana unificada con tres pestañas:
- **Planos y Vistas** — Exportación por lotes a PDF y/o DWG. Motor PDF configurable (nativo Revit o impresora del sistema). Conjuntos de publicación guardables con multi-selección (botón **MIX** para combinar conjuntos). PDF combinado con nombre configurable (tokens `{ProjectName}`, `{Date}`). Presets de exportación con import/export XML.
- **Modelo** — Exportación a NWC (Navisworks) con opciones de alcance, coordenadas, parámetros y precisión de facetado.
- **Familias** — Exportación masiva de familias `.rfa` organizadas por categoría, con búsqueda por nombre, filtro por categoría, selección múltiple con Shift y cancelación del proceso.

### 📐 Documentar
- **Acotado automático** — Dimensionamiento de vanos interiores y exteriores con esquemas de acotado personalizables por disciplina (guardables en JSON).
- **Dibujar Tabla** — Inserta tablas estilizadas en planos a partir de un Excel.

### 🎯 Organizar (Seleccionar)
Herramientas para selección, edición masiva y organización visual de elementos:
- **Filtros** ★ NUEVO — Creador de filtros de vista **nativos** de Revit en 4 pasos: categorías, reglas Y/O anidadas (o un filtro por cada valor de un parámetro, en lote), aplicación masiva a vistas o plantillas y colorización automática (aleatoria, gradiente o paleta) sobre proyección y corte, con patrón y visibilidad por canal. Genera leyendas de colores personalizables (estilos de texto, escala, tamaño de muestra en mm). Lee modelos vinculados y advierte parámetros no compatibles. Todo queda como filtros reales: visibles en V/G, editables y reutilizables en plantillas.
- **Encontrar y Seleccionar** — Filtra por categoría, parámetro (TIPO/EJEMPLAR) o subproyecto. Modelo completo o vista activa.
- **Asignar Valores** — Edición masiva de parámetros con vista previa y resumen de cambios.
- **Secuenciador** — Numeración incremental interactiva con prefijos, pasos y sufijos configurables.
- **Unir / Desunir** — Une o desune en lote la geometría de la selección.
- **Cuentagotas** — Copia parámetros entre elementos seleccionables.

### 🗂️ Gestionar
- **Tablas** — Exportar tablas de planificación a Excel, editar en lote e importar cambios de vuelta al modelo. Respeta los filtros de la tabla: lo que ocultas en Revit no sale en el Excel. El archivo abre con una portada con proyecto, modelo, alcance, fecha y usuario. Soporte para elementos de vínculos Revit.
- **Notas Clave** — Editor visual con jerarquía, drag-and-drop, import/export Excel. Codificación ANSI (Windows-1252) compatible con caracteres en español. Compatible con archivos locales y Autodesk Docs (Desktop Connector).
- **Subproyectos** — Crear y renombrar worksets, con selección masiva ("seleccionar todo"). Generar vistas 3D por subproyecto con visibilidad automática.
- **Trasladar Estándares** — Transferencia selectiva desde otros modelos abiertos (estilos de cota, notas de texto, estilos de línea, tipos de muro/piso/techo, patrones de relleno, cotas puntuales).
- **Gestionar Datos** — Conexión con fuentes externas y actualización masiva de parámetros.

### 🔗 Conectar (MCP)
Integración con servidores MCP para conectar Revit con herramientas de IA y servicios externos.

### 💬 Soporte
Chat en vivo con el equipo de BIM-CA directamente desde Revit. Accede desde **BIM Pills → Soporte** en el ribbon.

---

## Compatibilidad

| Revit | .NET | Estado |
|-------|------|--------|
| 2024  | .NET Framework 4.8 | ✅ Soportado |
| 2025  | .NET 8 | ✅ Soportado |
| 2026  | .NET 8 | ✅ Soportado |
| 2027  | .NET 10 | ✅ Soportado |

**Sistema operativo:** Windows 10 / 11

---

## Instalación

1. Descarga el instalador desde [**Releases →**](../../releases/latest)
2. Cierra Revit si está abierto
3. Ejecuta `BIMPills-1-0-1-Setup.exe` como administrador
4. Selecciona las versiones de Revit instaladas
5. Abre Revit — BIM Pills aparecerá en la cinta de opciones

> **PDF24 Creator** (gratuito) — necesario para la exportación de planos a PDF. El instalador verifica que esté instalado y, si falta, te lleva a la página oficial de descarga antes de continuar.

### Actualizaciones automáticas
El plugin detecta nuevas versiones al arrancar Revit y ofrece descargar e instalar la actualización sin salir de la aplicación.

---

## Licencias

BIM Pills requiere una licencia activa. Al abrir Revit por primera vez, activa tu licencia desde **BIM Pills → Acerca de → Activar licencia**.

Adquiere tu licencia en [bim-ca.com](https://bim-ca.com).

---

## Soporte

📧 support@bim-ca.com  
🌐 [bim-ca.com](https://bim-ca.com)  
🐛 [Reportar un problema](https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41?pvs=105)

---

<div align="center">
  <sub>© 2026 BIM-CA — Todos los derechos reservados</sub>
</div>
