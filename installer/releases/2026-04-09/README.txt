================================================================================

            ____   ___  __  __  ____   ___  _      _       ____
           | __ ) |_ _||  \/  ||  _ \ |_ _|| |    | |     / ___|
           |  _ \  | | | |\/| || |_) | | | | |    | |     \___ \
           | |_) | | | | |  | ||  __/  | | | |___ | |___   ___) |
           |____/ |___||_|  |_||_|   |___||_____||_____|  |____/

                        [ REVIT ADDON -- BETA 2.1 ]

================================================================================
  BIMPills v1.0.0-beta.2.1
  Revit 2024 / 2025 / 2026 / 2027  |  Gestion de Licencias Incluida
================================================================================

  VERSION        : 1.0.0-beta.2.1
  FECHA          : 9 Abril 2026
  ESTADO         : Beta estable (hotfix)
  OBJETIVO       : Revit 2024 (.NET 4.8) | 2025/2026 (.NET 8) | 2027 (.NET 10)
  SOPORTE        : support@bim-ca.com  |  https://bim-ca.com
  FEEDBACK       : https://bimca.notion.site/33bd89d548c2802a83d6f01c013c6e41

--------------------------------------------------------------------------------
  NOVEDADES EN BETA.2.1
--------------------------------------------------------------------------------

  Mejoras en Exportacion:
  [+] Conjuntos de publicacion ahora guardan configuracion completa
      (formato PDF/DWG, nombramiento, organizacion de carpetas, preset DWG)
  [+] Conjuntos de publicacion se guardan por modelo (scoped al proyecto)
  [+] Tiempo transcurrido en todos los procesos de exportacion:
      - Planos y vistas (PDF/DWG)
      - Auditoria BIM (HTML)
      - Tablas a Excel (Gestionar)
      - Familias a .rfa
      - Modelo a NWC (Navisworks)
  [+] Tab "Exportar" con secciones colapsables (Expander)
      para mejor navegacion
  [+] Exportacion DWG: checkbox para controlar xrefs vs archivo unico
      - Activado (recomendado): paper space correcto con tamano de hoja
      - Desactivado: archivo unico sin xrefs (layout comprimido)

  Correcciones:
  [*] "Exportacion cancelada" aparecia al terminar exportaciones exitosas
  [*] Error "archivo en uso" al re-exportar DWG/PDF (ahora se eliminan antes)
  [*] Conjuntos de publicacion ahora persisten configuraciones de exportacion

--------------------------------------------------------------------------------
  FUNCIONES COMPLETAS
--------------------------------------------------------------------------------

  Auditar         Auditoria BIM: warnings, familias, materiales. Reporte HTML.
  Exportar        Familias a .rfa. Planos y vistas a PDF/DWG. Modelo a NWC.
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
  2. Ejecuta: BIMPills-1.0.0-beta.2.1-Setup.exe
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
  1.0.0-beta.2.1  Abr 2026    2024/2025/2026/2027 Hotfix exportacion: tiempo,
                                                   conjuntos completos, DWG
  1.0.0-beta.2    Abr 2026    2024/2025/2026/2027 Keynotes, trasladar, vistas
                                                   3D worksets, Revit 2027
  1.0.0-beta.1    Abr 2026    2024/2025/2026      Licencias, CI/CD, Ordenar,
                                                   Gestionar, seguridad
  1.0.0-alpha.3   Mar 2026    2024/2025/2026      Esquemas, MCP, planos
  1.0.0-alpha.2   Mar 2026    2024/2025/2026      Acotado, Health Score, UI

--------------------------------------------------------------------------------

  BIMPills (c) 2026 BIM-CA. Todos los derechos reservados.

================================================================================
