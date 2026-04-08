================================================================================

            ____   ___  __  __  ____   ___  _      _       ____
           | __ ) |_ _||  \/  ||  _ \ |_ _|| |    | |     / ___|
           |  _ \  | | | |\/| || |_) | | | | |    | |     \___ \
           | |_) | | | | |  | ||  __/  | | | |___ | |___   ___) |
           |____/ |___||_|  |_||_|   |___||_____||_____|  |____/

                         [ REVIT ADDON -- BETA 2 ]

================================================================================
  BIMPills v1.0.0-beta.2
  Revit 2024 / 2025 / 2026 / 2027  |  Gestion de Licencias Incluida
================================================================================

  VERSION        : 1.0.0-beta.2
  FECHA          : 8 Abril 2026
  ESTADO         : Beta estable
  OBJETIVO       : Revit 2024 (.NET 4.8) | 2025/2026 (.NET 8) | 2027 (.NET 10)
  SOPORTE        : support@bim-ca.com  |  https://bim-ca.com
  FEEDBACK       : https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41

--------------------------------------------------------------------------------
  NOVEDADES EN BETA.2
--------------------------------------------------------------------------------

  Funcionalidad                       Estado       Detalle
  ----------------------------------  -----------  ----------------------------
  Notas Clave (Keynotes)              [NUEVO]      Editor visual con jerarquia,
                                                   drag-and-drop, import/export
                                                   Excel. Compatible con
                                                   Autodesk Docs (Desktop
                                                   Connector)
  Trasladar Estandares                [NUEVO]      Transferencia selectiva de
                                                   estandares desde otros
                                                   modelos: cotas, texto,
                                                   lineas, muros, pisos, techos,
                                                   patrones, cotas puntuales
  Vistas 3D por Subproyecto           [NUEVO]      Genera vistas 3D con
                                                   visibilidad por workset
  Soporte Revit 2027                  [NUEVO]      .NET 10 target framework
  Enlace de feedback en Acerca de     [NUEVO]      Reporte de bugs, sugerencias

  Correcciones Criticas:
  [*] Visibilidad de worksets en vistas 3D (IntegerValue=0 en Revit 2024)
  [*] Carga de notas clave desde Autodesk Docs (cloud -> Desktop Connector)
  [*] Jerarquia de notas clave (entries huerfanas fuera de su padre)
  [*] Transferencia de cotas del sistema que corrompia Revit
  [*] Cotas puntuales separadas como categoria propia
  [*] Detalle de items omitidos en transferencia
  [*] Nombre "BIM Pills" con espacio en toda la UI
  [*] Tema oscuro deshabilitado hasta estar completo

--------------------------------------------------------------------------------
  FUNCIONES COMPLETAS
--------------------------------------------------------------------------------

  Auditar         Auditoria BIM: warnings, familias, materiales. Reporte HTML.
  Exportar        Familias a .rfa. Planos y vistas a PDF/DWG.
  Documentar      Acotado automatico de vanos con esquemas personalizables.
  Gestionar       Tablas a Excel, notas clave, subproyectos, trasladar.
  Conectar        Integracion MCP para herramientas de IA.
  Ordenar         Numeracion incremental de elementos.
  Esquemas        Creacion y persistencia de esquemas de acotado.

--------------------------------------------------------------------------------
  INSTALACION
--------------------------------------------------------------------------------

  INSTALLER (recomendado):
  1. Cierra Revit completamente
  2. Ejecuta: BIMPills-1.0.0-beta.2-Setup.exe
  3. Selecciona tu(s) version(es) de Revit
  4. Abre Revit
  5. Activa licencia desde Acerca de > Activar licencia

  MANUAL:
  Copia BIMPills.addin y carpeta BIMPills\ a:
  %APPDATA%\Autodesk\Revit\Addins\{version}\

--------------------------------------------------------------------------------
  LICENCIAS
--------------------------------------------------------------------------------

  BIMPills requiere licencia activa. Validacion contra Airtable,
  cache DPAPI offline 24 h, periodo de gracia 7 dias tras expirar.

  Para obtener tu licencia: https://bim-ca.com

--------------------------------------------------------------------------------
  HISTORIAL
--------------------------------------------------------------------------------

  Version         Fecha       Revit               Resumen
  --------------  ----------  ------------------  ----------------------------
  1.0.0-beta.2    Abr 2026    2024/2025/2026/2027 Keynotes, trasladar, vistas
                                                   3D worksets, Revit 2027
  1.0.0-beta.1    Abr 2026    2024/2025/2026      Licencias, CI/CD, Ordenar,
                                                   Gestionar, seguridad
  1.0.0-alpha.3   Mar 2026    2024/2025/2026      Esquemas, MCP, planos
  1.0.0-alpha.2   Mar 2026    2024/2025/2026      Acotado, Health Score, UI

--------------------------------------------------------------------------------

  BIMPills (c) 2026 BIM-CA. Todos los derechos reservados.

================================================================================
