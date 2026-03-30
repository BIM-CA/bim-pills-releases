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
  BIMPills v1.0.0-beta.1  --  FULL RELEASE
  Revit 2024 / 2025 / 2026  |  All Features Unlocked
================================================================================

  VERSION      : 1.0.0-beta.1
  RELEASE DATE : March 30, 2026
  STATUS       : Full release — includes Sprint 3 (Ordenar + Gestionar)
  TARGET       : Revit 2024 (.NET 4.8)  |  Revit 2025 (.NET 8)  |  Revit 2026 (.NET 8)

--------------------------------------------------------------------------------
  WHAT'S NEW IN BETA.1
--------------------------------------------------------------------------------

  Feature                   Status      Details
  -----------------------   ---------   ----------------------------------------
  XAML Ribbon Stability     [FIXED]     Assembly loading conflicts resolved
  UI Redesign               [COMPLETE]  macOS-inspired, BIM-CA branding (#1565C0)
  Icon System               [ENHANCED]  Vector icons redesigned for clarity
  Acotado (Dimensioning)    [MATURE]    Interior void spacing operational
  Health Score              [OPER.]     Building assessment fully functional
  Estandarizar (Worksets)   [STABLE]    Workset standardization working smoothly
  Localization              [COMPLETE]  Full Spanish (ES) across all UI
  Multi-Version Support     [IMPROVED]  Revit 2024/2025/2026 targeting optimized
  Ordenar (Numbering)       [NEW]       Incremental element numbering, interactive
  Gestionar (SheetLink)     [NEW]       Export/import Revit schedules via Excel

--------------------------------------------------------------------------------
  BUG FIXES
--------------------------------------------------------------------------------

  [*] Fixed assembly loading conflicts with ClosedXML dependencies
  [*] Corrected window title and dialog styling consistency
  [*] Improved error handling in parameter assignments
  [*] Fixed category filtering in element selection dialogs
  [*] Enhanced memory management for large models

--------------------------------------------------------------------------------
  QUICK INSTALLATION (5 MINUTES)
--------------------------------------------------------------------------------

  FULL INSTALLER (recommended -- installs from scratch, removes previous version):
  1. Close Revit completely
  2. Run: output\BIMPills_Setup_1.0.0-beta.1.exe
  3. Follow the wizard -- select your Revit version(s)
  4. Restart Revit
  5. Look for the "BIMPills" tab in the ribbon

  HOTFIX ONLY (if you already have alpha.2 and only want to patch files):
  1. Close Revit completely
  2. Run: BIMPills-1.0.0-beta.1-Hotfix-Setup.exe  (Revit 2026 only)
  3. Restart Revit

  Manual installation paths:
  C:\Users\[YourUsername]\AppData\Roaming\Autodesk\Revit\Addins\2026\BIMPills\
  C:\Users\[YourUsername]\AppData\Roaming\Autodesk\Revit\Addins\2025\BIMPills\
  C:\Users\[YourUsername]\AppData\Roaming\Autodesk\Revit\Addins\2024\BIMPills\

--------------------------------------------------------------------------------
  RIBBON LAYOUT
--------------------------------------------------------------------------------

  +-- BIMPills (Main Tab) --------------------------------------------------+
  |                                                                           |
  |  [ DATOS PANEL ]        [ PROCESOS PANEL ]      [ INFO ]                |
  |   +- Auditar              +- Documentar           +- Acerca de           |
  |   +- Exportar             +- Estandarizar                                |
  |   +- Conectar                                                             |
  |   +- Ordenar                                                              |
  |   +- Gestionar                                                            |
  |                                                                           |
  +--------------------------------------------------------------------------+

--------------------------------------------------------------------------------
  INSTALLER CONTENTS
--------------------------------------------------------------------------------

  installer/
   +-- output\BIMPills_Setup_1.0.0-beta.1.exe      <-- FULL INSTALLER (run this)
   +-- BIMPills-1.0.0-beta.1-Hotfix-Setup.exe      <-- Hotfix only (Revit 2026)
   +-- BIMPills_Setup_1.0.0-beta.1.iss             <-- Inno Setup source script
   +-- BIMPills-Hotfix-Installer.nsi               <-- NSIS hotfix source script
   +-- README.txt                                  <-- This file

--------------------------------------------------------------------------------
  VERSION HISTORY
--------------------------------------------------------------------------------

  Version         Date           Revit          Status      Highlights
  --------------  -------------  -------------  ----------  -------------------------------
  1.0.0-beta.1    Mar 30, 2026   2024/2025/2026 [CURRENT]   Sprint 3: Ordenar + Gestionar,
                                                             XAML fixes, UI redesign, ES
  1.0.0-alpha.2   Mar 27, 2026   2024/2025/2026 [Previous]  Acotado, Health Score, UI homo.
  1.0.0-alpha.1   Mar 20, 2026   2026 only      [Archived]  Proof of concept

--------------------------------------------------------------------------------
  KNOWN LIMITATIONS
--------------------------------------------------------------------------------

  [!] Acotado   : Interior voids only; exterior dimensioning in next release
  [!] Health    : Requires complete model; warnings on partial models
  [!] Languages : Spanish complete; other languages coming post-beta
  [!] Gestionar : Excel diff preview before import coming in Sprint 4

--------------------------------------------------------------------------------
  WHAT'S NEXT -- SPRINT 4
--------------------------------------------------------------------------------

  [o] Acotado vanos exteriores
  [o] Multi-language support (EN / PT)
  [o] Gestionar: diff preview before applying Excel imports

--------------------------------------------------------------------------------
  SUPPORT & FEEDBACK
--------------------------------------------------------------------------------

  Found a Bug?           -->  Report it at Website Chat or write to support@bim-ca.com
  Feature Requests?      -->  Contact the BIM-CA team at support@bim-ca.com

--------------------------------------------------------------------------------
  LICENSE
--------------------------------------------------------------------------------

  BIMPills (c) 2026 BIM-CA. All rights reserved.
  Distribution outside the BIM-CA network is prohibited.

================================================================================

  +------------------------------------------------------------------+
  |                                                                    |
  |     [*]   BIMPills Beta 1  --  Fully Unlocked Edition   [*]      |
  |                                                                    |
  |      Thanks for testing! Your feedback drives development.        |
  |                                                                    |
  +------------------------------------------------------------------+

================================================================================
