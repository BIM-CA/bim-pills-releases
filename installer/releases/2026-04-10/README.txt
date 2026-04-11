================================================================================

            ____   ___  __  __  ____   ___  _      _       ____
           | __ ) |_ _||  \/  ||  _ \ |_ _|| |    | |     / ___|
           |  _ \  | | | |\/| || |_) | | | | |    | |     \___ \
           | |_) | | | | |  | ||  __/  | | | |___ | |___   ___) |
           |____/ |___||_|  |_||_|   |___||_____||_____|  |____/

                         [ REVIT ADDON -- BETA 3 ]

================================================================================
  BIM Pills v1.0.0-beta.3
  Revit 2024 / 2025 / 2026 / 2027  |  Gestion de Licencias Incluida
================================================================================

  VERSION        : 1.0.0-beta.3
  FECHA          : 10 Abril 2026
  ESTADO         : Beta estable
  OBJETIVO       : Revit 2024 (.NET 4.8) | 2025/2026 (.NET 8) | 2027 (.NET 10)
  SOPORTE        : support@bim-ca.com  |  https://bim-ca.com
  FEEDBACK       : https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41

--------------------------------------------------------------------------------
  NOVEDADES EN BETA.3
--------------------------------------------------------------------------------

  Motor PDF configurable (Exportar > Planos y Vistas):
  [+] Selector de motor PDF: "Impresora del sistema" o "Exportacion nativa
      de Revit"
  [+] Deteccion automatica de impresoras PDF instaladas (PDF24, Microsoft
      Print to PDF, Adobe PDF, Bullzip, etc.) ordenadas por preferencia
  [+] Etiquetas "(silencioso)" junto a impresoras que soportan impresion
      100% sin dialogos (PDF24, Bullzip)
  [+] Tip visual cuando PDF24 no esta instalado con enlace de descarga
      directa a pdf24.org
  [+] PDF24 Creator incluido como seccion opcional en el installer
      (instalacion silenciosa con ADDLOCAL=ALL)
  [+] Preferencia global persistida por usuario (se recuerda entre sesiones)
  [*] El motor del sistema preserva textos y lineas cuando la exportacion
      nativa de Revit pierde elementos en modelos complejos

  Rebranding "BIM Pills":
  [*] Nombre visible al usuario estandarizado a "BIM Pills" (con espacio)
      en titulos de ventana, dialogos, TaskDialog y ribbon. El identificador
      interno del tab en la API de Revit se mantiene como "BIMPills".

  Calidad interna:
  [*] PdfPrinterService purgado de codigo muerto (4 metodos sin uso
      eliminados)
  [*] Fix TOCTOU en PrintViewViaSystemPrinter: borrado de archivo previo
      sin pre-check
  [*] Cache de deteccion PDF24 para evitar enumerar impresoras de Windows
      en cada evento SelectionChanged
  [*] Fix race condition en tests: AcotadoVanosCommand.LastResult con
      [Collection] de xUnit para serializar ejecucion
  [*] Todas las funciones verificadas compatibles con Revit 2024, 2025,
      2026 y 2027 (multi-target: net48 / net8.0 / net10.0)

--------------------------------------------------------------------------------
  NOVEDADES PREVIAS (BETA.2.2)
--------------------------------------------------------------------------------

  [*] Suprimido dialogo "Actualizar recursos" durante exportacion batch
      (keynotes y vinculos cloud desactualizados ya no interrumpen)

--------------------------------------------------------------------------------
  FUNCIONES COMPLETAS
--------------------------------------------------------------------------------

  Auditar         Auditoria BIM: warnings, familias, materiales. Reporte HTML.
  Exportar        Familias a .rfa. Planos y vistas a PDF/DWG con motor PDF
                  configurable. Modelo a NWC.
  Documentar      Acotado automatico de vanos con esquemas personalizables.
  Gestionar       Tablas a Excel, notas clave, subproyectos, trasladar
                  estandares, plantillas de vista, filtros.
  Conectar        Integracion MCP para herramientas de IA.
  Ordenar         Numeracion incremental de elementos.
  Esquemas        Creacion y persistencia de esquemas de acotado.

--------------------------------------------------------------------------------
  INSTALACION
--------------------------------------------------------------------------------

  INSTALLER (recomendado):
  1. Cierra Revit completamente
  2. Ejecuta: BIMPills-1.0.0-beta.3-Setup.exe
  3. Selecciona tu(s) version(es) de Revit (2024/2025/2026/2027)
  4. OPCIONAL: marca "PDF24 Creator" si quieres el motor PDF silencioso
  5. Abre Revit
  6. Activa licencia desde Acerca de > Activar licencia

  MANUAL:
  Copia BIMPills.addin y carpeta BIMPills\ a:
  %APPDATA%\Autodesk\Revit\Addins\{version}\

--------------------------------------------------------------------------------
  LICENCIAS
--------------------------------------------------------------------------------

  BIM Pills requiere licencia activa. Validacion contra Airtable,
  cache DPAPI offline 24 h, periodo de gracia 7 dias tras expirar.

  Para obtener tu licencia: https://bim-ca.com

--------------------------------------------------------------------------------
  HISTORIAL
--------------------------------------------------------------------------------

  Version         Fecha       Revit               Resumen
  --------------  ----------  ------------------  ----------------------------
  1.0.0-beta.3    Abr 2026    2024/2025/2026/2027 Motor PDF configurable +
                                                   PDF24, rebranding, QA
  1.0.0-beta.2.2  Abr 2026    2024/2025/2026/2027 Hotfix: dialogo "Actualizar
                                                   recursos" suprimido en batch
  1.0.0-beta.2.1  Abr 2026    2024/2025/2026/2027 Conjuntos por modelo, tiempo
                                                   exportacion, DWG xrefs
  1.0.0-beta.2    Abr 2026    2024/2025/2026/2027 Keynotes, trasladar, vistas
                                                   3D worksets, Revit 2027
  1.0.0-beta.1    Abr 2026    2024/2025/2026      Licencias, CI/CD, Ordenar,
                                                   Gestionar, seguridad
  1.0.0-alpha.3   Mar 2026    2024/2025/2026      Esquemas, MCP, planos
  1.0.0-alpha.2   Mar 2026    2024/2025/2026      Acotado, Health Score, UI

--------------------------------------------------------------------------------

  BIM Pills (c) 2026 BIM-CA. Todos los derechos reservados.

================================================================================
