
 ____   ___   __  __    ____  ___ _     _     ____
| __ ) |_ _| |  \/  |  |  _ \|_ _| |   | |   / ___|
|  _ \  | |  | |\/| |  | |_) || || |   | |   \___ \
| |_) | | |  | |  | |  |  __/ | || |___|_|___ ___) |
|____/ |___| |_|  |_|  |_|   |___|_____|_____|____/

  Herramientas de productividad para Autodesk Revit
  Desarrollado por BIM-CA | bim-ca.com
  Version 1.0.0-beta.3.4

========================================================


NOVEDADES EN ESTA VERSION (beta 3.4)
--------------------------------------
* Importar datos movido al panel Datos (junto a Exportar)
  para un flujo mas intuitivo.

* Ventana de metodologia actualizada con referencias
  normativas: ISO 19650-1/2, AEC UK BIM Protocol y
  BIM Forum LOD Specification.

* Actualizacion automatica rediseniada: nueva ventana
  con logo, insignias de version, barra de progreso
  de descarga y soporte markdown en notas de cambio.

* Correccion de tamanio de archivo en modelos
  colaborativos BIM 360 / ACC (estrategia
  CollaborationCache para rutas en nube).

* Codigo de diagnostico interno eliminado de produccion.

* Obfuscacion integrada en el proceso de compilacion
  Release (Core, Infrastructure, Commands).


INSTALACION
-----------
1. Ejecuta "BIMPills-beta-3.4-Setup.exe"
2. Selecciona las versiones de Revit a instalar
   (2024, 2025, 2026 o 2027)
3. Opcionalmente instala PDF24 Creator (recomendado
   para exportacion PDF silenciosa — gratis)
4. Abre Revit y busca la pestana "BIM Pills" en el ribbon

   TIP: Si ya tienes PDF24 instalado, el installer lo
   detecta y configura automaticamente la impresora
   "PDF24 (BIMPills)" para exportacion silenciosa.


FUNCIONES
---------

  [ AUDITAR ]
  Auditoria de modelo BIM con puntuacion de salud.
  Analiza familias, advertencias, materiales y otros
  indicadores de calidad. Exporta reportes HTML.

  [ EXPORTAR — Familias ]
  Exportacion masiva de familias a archivos .rfa,
  organizadas por categoria.

  [ EXPORTAR — Planos y Vistas ]
  Exportacion por lotes de planos y vistas a PDF y/o DWG.
  Motor PDF configurable (PDF24 o nativo Revit).
  Conjuntos de publicacion guardables.

  [ EXPORTAR — Modelo ]
  Exportacion del modelo completo a NWC (Navisworks) con
  opciones de alcance, coordenadas y precision de
  facetado.

  [ DOCUMENTAR — Acotado automatico ]
  Dimensionamiento automatico de vanos interiores con
  esquemas de acotado personalizables.

  [ GESTIONAR — Tablas ]
  Exportar tablas a Excel, editar en lote e importar
  cambios de vuelta al modelo.

  [ GESTIONAR — Notas Clave ]
  Editor visual de notas clave con jerarquia,
  drag-and-drop e importacion/exportacion Excel.
  Compatible con Autodesk Docs.

  [ GESTIONAR — Subproyectos ]
  Crear y renombrar worksets. Generar vistas 3D por
  subproyecto con visibilidad automatica.

  [ GESTIONAR — Trasladar Estandares ]
  Transferencia selectiva de estandares desde otros
  modelos abiertos.

  [ ORDENAR ]
  Numeracion incremental interactiva con prefijos,
  pasos y sufijos configurables.

  [ CONECTAR (MCP) ]
  Integracion con servidores MCP para conectar Revit
  con herramientas de IA.


MOTOR PDF
---------

  PDF24 (recomendado)
    Exportacion completamente silenciosa, sin dialogos,
    maxima fidelidad de lineas y textos.
    Requiere PDF24 Creator (gratis en pdf24.com).

  Nativo Revit
    Sin dependencias externas. Puede perder lineas o
    texto en planos graficamente complejos.

  Se configura en: Exportar > Planos y Vistas > engranaje


LICENCIAS
---------
BIM Pills requiere una licencia activa.

  Activar: Revit > "BIM Pills" > Acerca de > Activar

  Adquirir: https://bim-ca.com


SOPORTE
-------
  Reportar problema o sugerencia:
  https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41

  Email: support@bim-ca.com


========================================================
(c) 2026 BIM-CA. Todos los derechos reservados.
