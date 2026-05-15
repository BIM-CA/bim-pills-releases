
 ____   ___   __  __    ____  ___ _     _     ____
| __ ) |_ _| |  \/  |  |  _ \|_ _| |   | |   / ___|
|  _ \  | |  | |\/| |  | |_) || || |   | |    \___ \
| |_) | | |  | |  | |  |  __/ | || |___|_|___ ___) |
|____/ |___| |_|  |_|  |_|   |___|_____|_____|____/

  Herramientas de productividad para Autodesk Revit
  Desarrollado por BIM-CA | bim-ca.com
  Version 1.0.0-beta.7.4

========================================================


NOVEDADES EN ESTA VERSION (beta 7.4)
--------------------------------------
* Auditoria: purga mas eficiente con estrategia batch+
  binary-split — una transaccion por lote en vez de una
  por elemento. Mas rapido en modelos grandes.

* Auditoria: nuevos tipos de elementos purgables
  detectados: plantillas de vista, filtros de vista,
  estilos de texto, tipos de cota y regiones rellenas.

* Auditoria: ventana de progreso durante el analisis
  con indicador de fase y barra de porcentaje.

* Auditoria: lista de exclusion ampliada para tipos
  de sistema internos de Revit (menos falsos positivos
  en elementos huerfanos).

* Conjuntos (Exportar Planos): boton MIX para combinar
  multiples conjuntos en una sola exportacion.

* Seleccionar: etiquetas TIPO/EJEMPLAR con texto completo
  y colores diferenciados.


NOVEDADES EN beta.7.3
---------------------
* Purga: si un elemento no puede eliminarse, el dialogo
  muestra nombre y razon exacta de Revit.

* Seleccionar: correcciones en asignacion de subproyectos.


INSTALACION
-----------
1. Ejecuta "BIMPills-beta-7.4-Setup.exe"
2. Selecciona las versiones de Revit a instalar
   (2024, 2025, 2026 o 2027)
3. Opcionalmente instala PDF24 Creator (recomendado
   para exportacion PDF silenciosa -- gratis)
4. Abre Revit y busca la pestana "BIM Pills" en el ribbon

   TIP: Si ya tienes PDF24 instalado, el installer lo
   detecta y configura automaticamente la impresora
   "PDF24 (BIMPills)" para exportacion silenciosa.


FUNCIONES
---------

  [ AUDITAR ]
  Auditoria de modelo BIM con puntuacion de salud.
  Analiza familias, advertencias, materiales y otros
  indicadores de calidad. Exporta reportes HTML y CSV.
  Purga elementos no usados directamente desde la ventana.

  [ EXPORTAR -- Familias ]
  Exportacion masiva de familias a archivos .rfa,
  organizadas por categoria.

  [ EXPORTAR -- Planos y Vistas ]
  Exportacion por lotes de planos y vistas a PDF y/o DWG.
  Motor PDF configurable (PDF24 o nativo Revit).
  Conjuntos de publicacion guardables. Boton MIX.

  [ EXPORTAR -- Modelo ]
  Exportacion del modelo completo a NWC (Navisworks) con
  opciones de alcance, coordenadas y precision de
  facetado.

  [ DOCUMENTAR -- Acotado automatico ]
  Dimensionamiento automatico de vanos interiores con
  esquemas de acotado personalizables.

  [ GESTIONAR -- Tablas ]
  Exportar tablas a Excel, editar en lote e importar
  cambios de vuelta al modelo. Compatible con vinculos.

  [ GESTIONAR -- Notas Clave ]
  Editor visual de notas clave con jerarquia,
  drag-and-drop e importacion/exportacion Excel.
  Compatible con Autodesk Docs.

  [ GESTIONAR -- Subproyectos ]
  Crear y renombrar worksets. Generar vistas 3D por
  subproyecto con visibilidad automatica.

  [ GESTIONAR -- Trasladar Estandares ]
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
