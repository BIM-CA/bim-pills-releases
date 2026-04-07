================================================================================

            ____   ___  __  __  ____   ___  _      _       ____
           | __ ) |_ _||  \/  ||  _ \ |_ _|| |    | |     / ___|
           |  _ \  | | | |\/| || |_) | | | | |    | |     \___ \
           | |_) | | | | |  | ||  __/  | | | |___ | |___   ___) |
           |____/ |___||_|  |_||_|   |___||_____||_____|  |____/

                ____   ___  __  __ ___          ____      _
               | __ ) |_ _||  \/  |_ _|  ___   / ___|    / \
               |  _ \  | | | |\/| || |  |___| | |       / _ \
               | |_) | | | | |  | || |        | |___   / ___ \
               |____/ |___||_|  |_|___|        \____| /_/   \_\

                         [ REVIT ADDON -- BETA EDITION ]

================================================================================
  BIMPills v1.0.0-beta.1  --  LANZAMIENTO COMPLETO
  Revit 2024 / 2025 / 2026  |  Gestion de Licencias Incluida
================================================================================

  VERSION        : 1.0.0-beta.1
  FECHA          : Abril 2026
  ESTADO         : Beta estable -- Sprints 1-5 completos
  OBJETIVO       : Revit 2024 (.NET 4.8) | Revit 2025 (.NET 8) | Revit 2026 (.NET 8)
  SOPORTE        : support@bim-ca.com  |  https://bim-ca.com

--------------------------------------------------------------------------------
  NOVEDADES EN BETA.1
--------------------------------------------------------------------------------

  Funcionalidad               Estado       Detalle
  --------------------------  -----------  ----------------------------------------
  Auditar                     [ESTABLE]    Salud del modelo: warnings, purgables
  Exportar Familias           [ESTABLE]    Exportacion de familias cargadas a Excel
  Exportar Planos             [ESTABLE]    PDF / XLSX con perfiles guardados
  Conectar (MCP)              [OPERATIVO]  Conexiones a servicios externos via MCP
  Documentar -- Acotado       [MADURO]     5 esquemas: interiores, vanos, ejes,
                                           niveles ARQ, muros exteriores
  Esquemas personalizados     [COMPLETO]   Creacion, edicion y persistencia en JSON
  Estandarizar (Worksets)     [ESTABLE]    Estandarizacion de worksets BIM-CA
  Ordenar (Numeracion)        [NUEVO]      Numeracion incremental interactiva,
                                           prefijo / paso / sufijo configurables
  Gestionar (SheetLink)       [NUEVO]      Exportar tablas Revit a Excel e
                                           importar parametros en lote
  Licencias                   [NUEVO]      Activacion por clave, binding a equipo,
                                           cache DPAPI offline 24 h,
                                           periodo de gracia 7 dias
  Seguridad                   [MEJORADO]   Token XOR-obfuscado, DPAPI, whitelist URLs,
                                           TypeNameHandling.None en todo JSON
  CI/CD                       [NUEVO]      GitHub Actions: build + 76 tests en cada push
  Soporte multi-version       [MEJORADO]   Binarios separados net48 / net8.0-windows

--------------------------------------------------------------------------------
  CORRECCIONES
--------------------------------------------------------------------------------

  [*] Conflictos de carga de ensamblados ClosedXML resueltos (AssemblyResolve)
  [*] Filtrado de vista en Ordenar -- solo categorias visibles en vista activa
  [*] Totales y encabezados excluidos de las tablas en Gestionar (SheetLink)
  [*] Nombre del titular de licencia leido correctamente (campo lookup Airtable)
  [*] Version simplificada -- sin hash de commit en la cadena mostrada
  [*] Ventana Acerca de siempre accesible aunque la licencia este expirada
  [*] Acotado vanos exteriores (muros perimetrales) disponible como esquema

--------------------------------------------------------------------------------
  INSTALACION RAPIDA
--------------------------------------------------------------------------------

  INSTALADOR COMPLETO (recomendado):
  1. Cierra Revit completamente
  2. Ejecuta: installer\releases\2026-04-01\BIMPills-1.0.0-beta.1-Setup.exe
  3. Sigue el asistente -- selecciona tu(s) version(es) de Revit
  4. Reinicia Revit
  5. La pestana "BIMPills" aparece en el ribbon
  6. Ve a Acerca de > Activar licencia e ingresa tu clave

  Rutas de instalacion manual:
  %APPDATA%\Autodesk\Revit\Addins\2026\BIMPills\
  %APPDATA%\Autodesk\Revit\Addins\2025\BIMPills\
  %APPDATA%\Autodesk\Revit\Addins\2024\BIMPills\

--------------------------------------------------------------------------------
  LICENCIAS
--------------------------------------------------------------------------------

  BIMPills requiere una licencia activa. La validacion se realiza contra
  Airtable y se cachea localmente con cifrado DPAPI (valida 24 h offline).
  Cada licencia se vincula a un equipo por Machine ID.

  Plan                  Descripcion
  --------------------  -------------------------------------------------------
  Pro Mensual           Licencia individual, renueva cada mes
  Pro Anual             Licencia individual, renueva cada ano
  Pro Interno           Para colaboradores BIM-CA, sin binding de equipo

  Periodo de gracia  :  7 dias tras expirar antes del bloqueo total
  Desactivar equipo  :  disponible desde Acerca de > Desactivar equipo
  Soporte offline    :  cache DPAPI valido 24 h sin conexion

  Para obtener tu licencia: https://bim-ca.com

--------------------------------------------------------------------------------
  RIBBON
--------------------------------------------------------------------------------

  +-- BIMPills (pestana principal) ------------------------------------------+
  |                                                                            |
  |  [ PANEL DATOS ]          [ PANEL PROCESOS ]       [ INFO ]              |
  |   +- Auditar               +- Documentar            +- Acerca de          |
  |   +- Exportar              +- Estandarizar                                |
  |   +- Conectar                                                              |
  |   +- Ordenar                                                               |
  |   +- Gestionar                                                             |
  |                                                                            |
  +---------------------------------------------------------------------------+

--------------------------------------------------------------------------------
  DATOS PERSISTIDOS
--------------------------------------------------------------------------------

  %APPDATA%\BIMPills\
   +-- Profiles\reports.json            -- Perfiles de exportacion
   +-- Schemes\dimensions.json          -- Esquemas de acotado personalizados
   +-- MCPConnections\connections.json  -- Conexiones MCP
   +-- Exports\                         -- Exportaciones de tablas a Excel
   +-- license.dat                      -- Cache de licencia (cifrado DPAPI)

--------------------------------------------------------------------------------
  ARQUITECTURA
--------------------------------------------------------------------------------

  BIMPills.Core           -- Interfaces, modelos, contratos (sin dependencias)
  BIMPills.Commands       -- Logica de negocio (sin RevitAPI)
  BIMPills.Infrastructure -- Persistencia JSON, exportadores XLSX, licencias
  BIMPills.UI             -- Ventanas WPF (XAML)
  BIMPills.Revit          -- Adaptador RevitAPI, ribbon, ExternalCommand bridge

  Framework por version:
  Revit 2024  -->  net48-windows  (.NET Framework 4.8)
  Revit 2025  -->  net8.0-windows (.NET 8)
  Revit 2026  -->  net8.0-windows (.NET 8)

--------------------------------------------------------------------------------
  COMPILACION
--------------------------------------------------------------------------------

  # Una version
  dotnet build src/BIMPills.Revit/ -c Release -p:RevitVersion=2026

  # Todas las versiones (genera dist\RevitXXXX\BIMPills\)
  powershell -ExecutionPolicy Bypass -File build/build-all.ps1 -Configuration Release

  # Tests (76+ passing)
  dotnet test tests/BIMPills.Core.Tests/ -c Release

  # Compilar instalador (requiere Inno Setup 6)
  cd installer
  iscc BIMPills_Setup_1.0.0-beta.1.iss

--------------------------------------------------------------------------------
  ESTRUCTURA DEL REPOSITORIO
--------------------------------------------------------------------------------

  src\
   +-- BIMPills.Core\          -- Modelos e interfaces
   +-- BIMPills.Commands\      -- Logica de comandos
   +-- BIMPills.Infrastructure\ -- Persistencia y servicios externos
   +-- BIMPills.UI\            -- Interfaz WPF
   +-- BIMPills.Revit\         -- Adaptador Revit + ribbon
  tests\
   +-- BIMPills.Core.Tests\    -- Suite de tests xUnit (76+)
  installer\
   +-- BIMPills_Setup_1.0.0-beta.1.iss  -- Script Inno Setup (fuente)
   +-- releases\2026-04-01\             -- Instalador compilado
  manifests\                  -- Archivos .addin por version de Revit
  build\                      -- Scripts de build y deploy
  .github\workflows\          -- CI/CD GitHub Actions

--------------------------------------------------------------------------------
  HISTORIAL DE VERSIONES
--------------------------------------------------------------------------------

  Version         Fecha       Revit          Estado      Resumen
  --------------  ----------  -------------  ----------  ------------------------
  1.0.0-beta.1    Abr 2026    2024/2025/2026 [ACTUAL]    Sprints 1-5: licencias,
                                                          Ordenar, Gestionar,
                                                          acotado exterior, CI/CD
  1.0.0-alpha.3   Mar 2026    2024/2025/2026 [Anterior]  Esquemas personalizados,
                                                          MCP, exportacion planos
  1.0.0-alpha.2   Mar 2026    2024/2025/2026 [Archivado] Acotado, Health Score,
                                                          UI homogenea
  1.0.0-alpha.1   Mar 2026    2026 only      [Archivado] Prueba de concepto

--------------------------------------------------------------------------------
  LIMITACIONES CONOCIDAS
--------------------------------------------------------------------------------

  [!] Acotado    : Esquema de niveles ARQ en refinamiento
  [!] Conectar   : Discovery MCP experimental
  [!] Idiomas    : Solo espanol completo; ingles y portugues post-beta

--------------------------------------------------------------------------------
  SOPORTE
--------------------------------------------------------------------------------

  Errores o problemas  -->  support@bim-ca.com
  Solicitudes          -->  Chat en bim-ca.com

--------------------------------------------------------------------------------
  LICENCIA DE USO
--------------------------------------------------------------------------------

  BIMPills (c) 2026 BIM-CA. Todos los derechos reservados.
  Distribucion fuera de la red BIM-CA prohibida sin autorizacion.

================================================================================

  +------------------------------------------------------------------+
  |                                                                    |
  |   [*]   BIMPills Beta 1  --  Edicion Completa con Licencias      |
  |                                                                    |
  |   Gracias por usar BIMPills. Tu feedback impulsa el desarrollo.   |
  |                                                                    |
  +------------------------------------------------------------------+

================================================================================
