
  -----------------------------------------------------------------
  ██████╗ ██╗███╗   ███╗    ██████╗ ██╗██╗     ██╗     ███████╗
  ██╔══██╗██║████╗ ████║    ██╔══██╗██║██║     ██║     ██╔════╝
  ██████╔╝██║██╔████╔██║    ██████╔╝██║██║     ██║     ███████╗
  ██╔══██╗██║██║╚██╔╝██║    ██╔═══╝ ██║██║     ██║     ╚════██║
  ██████╔╝██║██║ ╚═╝ ██║    ██║     ██║███████╗███████╗███████║
  ╚═════╝ ╚═╝╚═╝     ╚═╝    ╚═╝     ╚═╝╚══════╝╚══════╝╚══════╝
  Plugin de Productividad para Autodesk Revit  ·  BIM-CA
  Revit 2024 · 2025 · 2026 · 2027  ·  Version 1.0.0-beta.3.2
  -----------------------------------------------------------------


NOVEDADES EN ESTA VERSION
--------------------------

* Tamano de hoja correcto al exportar con PDF24: los PDFs ahora
  respetan exactamente las dimensiones del plano (A1, A0, etc.).
  El motor detecta PDF24 y redirige al motor nativo de Revit
  para garantizar el tamano correcto.

* Deteccion de Navisworks mejorada: la herramienta Exportar Modelo
  detecta correctamente el plugin NWC en Revit 2024.

* Ventanas siempre en el monitor correcto: los dialogos de
  notificacion aparecen en el mismo monitor donde esta Revit.

* Navegacion por pasos con boton "Siguiente": en herramientas de
  multiples pasos los pasos 1 y 2 muestran el boton Siguiente y
  el ultimo paso muestra el boton de accion directamente.

* Carpeta de destino pre-cargada: todas las herramientas de
  exportacion inician con el Escritorio como carpeta por defecto.


INSTALACION
-----------
1. Ejecuta "BIM Pills 1.0.0-beta.3.2 Setup.exe"
2. Selecciona las versiones de Revit a instalar
   (2024, 2025, 2026 o 2027)
3. Opcionalmente instala PDF24 Creator (recomendado
   para exportacion PDF silenciosa — gratis)
4. Abre Revit y busca la pestana "BIM Pills" en el ribbon

   TIP: Si ya tienes PDF24 instalado, el installer lo detecta
   y configura todo automaticamente.


FUNCIONES
---------

  [ AUDITAR ]
  Auditoria de modelo BIM con puntuacion de salud.
  Analiza familias, advertencias, materiales y otros
  indicadores de calidad. Exporta reportes HTML.

  [ EXPORTAR — Planos y Vistas ]
  Exportacion por lotes de planos y vistas a PDF y/o DWG.
  Motor PDF configurable. Conjuntos de publicacion guardables.
  Navegacion guiada por pasos.

  [ EXPORTAR — Modelo ]
  Exportacion del modelo completo a NWC (Navisworks) con
  opciones de alcance, coordenadas y precision de facetado.

  [ EXPORTAR — Familias ]
  Exportacion masiva de familias a archivos .rfa,
  organizadas por categoria.

  [ DOCUMENTAR — Acotado automatico ]
  Dimensionamiento automatico de vanos interiores con
  esquemas de acotado personalizables por disciplina.

  [ GESTIONAR — Tablas ]
  Exportar tablas a Excel, editar en lote e importar
  cambios de vuelta al modelo.

  [ GESTIONAR — Notas Clave ]
  Editor visual de notas clave con jerarquia, drag-and-drop
  e importacion/exportacion Excel. Compatible con Autodesk Docs.

  [ GESTIONAR — Subproyectos ]
  Crear y renombrar worksets. Generar vistas 3D por subproyecto
  con visibilidad automatica.

  [ GESTIONAR — Trasladar Estandares ]
  Transferencia selectiva de estandares desde otros modelos
  abiertos (cotas, texto, lineas, muros, pisos, patrones).

  [ ORDENAR ]
  Numeracion incremental interactiva con prefijos,
  pasos y sufijos configurables.

  [ CONECTAR (MCP) ]
  Integracion con servidores MCP para conectar Revit
  con herramientas de IA.


MOTOR PDF
---------

  Nativo Revit (recomendado)
    Tamano de hoja siempre correcto. Sin dependencias externas.

  PDF24 / Impresora del sistema
    Maxima fidelidad de lineas en planos complejos. PDF24 se
    redirige automaticamente al motor nativo para garantizar
    el tamano de hoja correcto.

  Se configura en: Exportar > Planos y Vistas > engranaje


LICENCIAS
---------
BIM Pills requiere una licencia activa.

  Activar: Revit > "BIM Pills" > Acerca de > Activar licencia
  Adquirir: https://bim-ca.com


SOPORTE
-------
  Reportar problema o sugerencia:
  https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41

  Email: support@bim-ca.com


  -----------------------------------------------------------------
  (c) 2026 BIM-CA. Todos los derechos reservados.
  -----------------------------------------------------------------
