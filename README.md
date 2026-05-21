<div align="center">
  <img src="assets/logo.png" alt="BIM Pills" width="200"/>
  <h3>Plugin de Productividad para Autodesk Revit</h3>
  <p>
    <a href="../../releases/latest"><img src="https://img.shields.io/github/v/release/BIM-CA/bim-pills-releases?label=versión&color=EF6337" alt="Latest Release"/></a>
    <img src="https://img.shields.io/badge/Revit-2024%20%7C%202025%20%7C%202026%20%7C%202027-0696D7" alt="Revit Versions"/>
    <img src="https://img.shields.io/badge/plataforma-Windows-informational" alt="Windows"/>
  </p>
</div>

---

## ¿Qué es BIM Pills?

BIM Pills es un plugin para Autodesk Revit que reúne en un solo lugar las herramientas más útiles del día a día en proyectos BIM. Diseñado para equipos que trabajan con estándares de calidad, flujos de exportación y gestión eficiente de datos del modelo.

---

## Funcionalidades

### 🔍 Auditar
Auditoría completa del modelo BIM con puntuación de salud. Analiza familias, advertencias, materiales y otros indicadores de calidad del modelo. Incluye:
- Purga segura con detección exhaustiva de familias en uso
- Ventana de progreso con fases e indicadores
- Reporte HTML exportable

### 📤 Exportar
Ventana unificada con tres pestañas:
- **Planos y Vistas** — Exportación por lotes a PDF y/o DWG. Conjuntos de publicación guardables. PDF combinado con nombre configurable (tokens `{ProjectName}`, `{Date}`)
- **Modelo** — Exportación a NWC (Navisworks) con opciones de alcance y coordenadas
- **Familias** — Exportación masiva de familias `.rfa` organizadas por categoría

### 📐 Documentar
- **Acotado automático** — Dimensionamiento de vanos interiores con esquemas personalizables por disciplina

### 🗂️ Gestionar
- **Tablas** — Exportar tablas de planificación a Excel, editar en lote e importar cambios al modelo
- **Notas Clave** — Editor visual con jerarquía, drag-and-drop, importación/exportación Excel. Compatible con Autodesk Docs
- **Subproyectos** — Crear y renombrar worksets. Generar vistas 3D por subproyecto
- **Trasladar Estándares** — Transferencia selectiva de estándares desde otros modelos abiertos

### 🔢 Ordenar
Numeración incremental interactiva con prefijos, pasos y sufijos configurables.

### 💬 Soporte
Chat en vivo con el equipo de BIM-CA directamente desde Revit.

### 🔗 Conectar (MCP)
Integración con servidores MCP para conectar Revit con herramientas de IA.

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
3. Ejecuta `BIMPills-beta-X.X-Setup.exe` como administrador
4. Selecciona las versiones de Revit instaladas
5. Abre Revit — BIM Pills aparecerá en la cinta de opciones

> **PDF24 Creator** — necesario para exportación PDF silenciosa. El installer lo detecta y configura automáticamente si ya está instalado.

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
