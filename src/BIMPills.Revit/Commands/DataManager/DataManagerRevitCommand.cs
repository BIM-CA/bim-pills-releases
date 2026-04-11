using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using BIMPills.Commands.DataManager;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Revit.Commands;
using BIMPills.Revit.Context;
using BIMPills.UI.DataManager;
using BIMPills.UI.Shared;

namespace BIMPills.Revit.Commands.DataManager
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public sealed class DataManagerRevitCommand : RevitCommandBase
    {
        protected override IPluginCommand CreateCommand()
            => new DataManagerCommand();

        protected override void OnSuccess(IPluginCommand command)
        {
            var logger = ServiceLocator.IsRegistered<ILogger>() ? ServiceLocator.Get<ILogger>() : null;
            var targetDoc        = CommandData!.Application.ActiveUIDocument.Document;
            var documentServices = new RevitDocumentServices(targetDoc);
            var modelName        = targetDoc.Title ?? "Modelo";

            // Read keynote file path from Revit's KeynoteTable configuration
            string? keynoteFilePath = null;
            try
            {
                var kt = KeynoteTable.GetKeynoteTable(targetDoc);
                logger?.Info($"[Keynotes] KeynoteTable: {kt != null}, Id={kt?.Id}");

                if (kt != null)
                {
                    // Approach 1: ExternalFileReference (local files)
                    try
                    {
                        var extRef = kt.GetExternalFileReference();
                        if (extRef != null)
                        {
                            var resolved = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                            logger?.Info($"[Keynotes] ExternalFileRef resolved: [{resolved}]");
                            if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                                keynoteFilePath = resolved;
                        }
                    }
                    catch (Exception ex) { logger?.Warning($"[Keynotes] ExternalFileRef error: {ex.Message}"); }

                    // Approach 2: ExternalResourceReferences (cloud/Desktop Connector files)
                    if (keynoteFilePath == null)
                    {
                        try
                        {
                            var resRefs = kt.GetExternalResourceReferences();
                            logger?.Info($"[Keynotes] ExternalResourceRefs count: {resRefs?.Count ?? 0}");
                            if (resRefs != null && resRefs.Count > 0)
                            {
                                foreach (var kvp in resRefs)
                                {
                                    var resRef = kvp.Value;
                                    logger?.Info($"[Keynotes] ResRef serverId=[{resRef.ServerId}]");

                                    // Try to get the InSessionPath (cloud path like "Autodesk Docs://...")
                                    try
                                    {
                                        var inSessionPath = resRef.InSessionPath;
                                        logger?.Info($"[Keynotes] InSessionPath: [{inSessionPath}]");
                                        if (!string.IsNullOrEmpty(inSessionPath))
                                        {
                                            // Direct use if it's a local path
                                            if (File.Exists(inSessionPath))
                                            {
                                                keynoteFilePath = inSessionPath;
                                                break;
                                            }
                                            // Convert cloud path to local Desktop Connector path
                                            // "Autodesk Docs://org/project/..." → "{USERPROFILE}\DC\ACCDocs\org\project\..."
                                            var localPath = ConvertCloudPathToLocal(inSessionPath, logger);
                                            if (localPath != null)
                                            {
                                                keynoteFilePath = localPath;
                                                break;
                                            }
                                        }
                                    }
                                    catch (Exception ex) { logger?.Warning($"[Keynotes] InSessionPath error: {ex.Message}"); }

                                    // Try reference info for path keys
                                    try
                                    {
                                        var refMap = resRef.GetReferenceInformation();
                                        foreach (var info in refMap)
                                            logger?.Info($"[Keynotes] RefInfo: [{info.Key}] = [{(info.Value?.Length > 80 ? info.Value.Substring(0, 80) + "..." : info.Value)}]");

                                        string[] pathKeys = { "Path", "RelativePath", "AbsolutePath", "LocalPath", "CachedPath" };
                                        foreach (var key in pathKeys)
                                        {
                                            if (refMap.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val) && File.Exists(val))
                                            {
                                                logger?.Info($"[Keynotes] Found local file via [{key}]: [{val}]");
                                                keynoteFilePath = val;
                                                break;
                                            }
                                        }
                                    }
                                    catch (Exception ex) { logger?.Warning($"[Keynotes] RefInfo error: {ex.Message}"); }

                                    // Cloud file detected but no local path found — search Desktop Connector folders
                                    if (keynoteFilePath == null)
                                    {
                                        keynoteFilePath = FindKeynoteInDesktopConnector(targetDoc, logger);
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { logger?.Warning($"[Keynotes] ExternalResourceRefs error: {ex.Message}"); }
                    }

                    // Approach 3: Search Desktop Connector as last resort
                    if (keynoteFilePath == null)
                    {
                        keynoteFilePath = FindKeynoteInDesktopConnector(targetDoc, logger);
                    }
                }
            }
            catch (Exception ex) { logger?.Warning($"[Keynotes] Outer error: {ex.Message}"); }
            logger?.Info($"[Keynotes] FINAL path: [{keynoteFilePath}]");

            // If cloud-hosted and not found, show a clearer empty state message
            bool isCloudKeynote = keynoteFilePath == null;

            var window = new GestionarWindow(documentServices, modelName);

            // Initialize keynotes with Revit's configured file + reload callback
            window.InitializeKeynotes(
                keynoteFilePath,
                reloadInRevitCallback: path =>
                {
                    try
                    {
                        var kt = KeynoteTable.GetKeynoteTable(targetDoc);
                        kt.Reload(new KeyBasedTreeEntriesLoadResults());
                        return true;
                    }
                    catch { return false; }
                });

            window.ShowDialogOverRevit();
        }

        /// <summary>
        /// Searches Desktop Connector sync folders for a keynote .txt file
        /// that belongs to the same project as the current document.
        /// </summary>
        private static string? FindKeynoteInDesktopConnector(Document doc, ILogger? logger)
        {
            try
            {
                // Get the model's file path to determine the project folder
                var modelPath = doc.PathName;
                logger?.Info($"[Keynotes] DC search: modelPath=[{modelPath}]");

                if (string.IsNullOrEmpty(modelPath)) return null;

                // The model folder — keynote files are usually in the same project tree
                var modelDir = Path.GetDirectoryName(modelPath);
                if (string.IsNullOrEmpty(modelDir) || !Directory.Exists(modelDir)) return null;

                // Search the model's folder and parent folders for keynote .txt files
                var candidates = new List<string>();

                // Search in model directory
                SearchForKeynoteFiles(modelDir, candidates, logger);

                // Search one level up (project root)
                var parentDir = Path.GetDirectoryName(modelDir);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    SearchForKeynoteFiles(parentDir, candidates, logger);

                // Also search common Desktop Connector roots
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] dcRoots = { "ACCDocs", "BIM 360", "DC" };
                foreach (var root in dcRoots)
                {
                    var dcPath = Path.Combine(userProfile, root);
                    if (Directory.Exists(dcPath) && !modelDir.StartsWith(dcPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Search for keynote files matching the model name prefix
                        var modelPrefix = doc.Title?.Split('-')[0]?.Trim() ?? "";
                        if (modelPrefix.Length >= 2)
                        {
                            try
                            {
                                foreach (var f in Directory.EnumerateFiles(dcPath, $"{modelPrefix}*Keynote*.txt", SearchOption.AllDirectories))
                                {
                                    candidates.Add(f);
                                    logger?.Info($"[Keynotes] DC candidate (prefix match): [{f}]");
                                }
                            }
                            catch { }
                        }
                    }
                }

                if (candidates.Count == 1)
                {
                    logger?.Info($"[Keynotes] DC found single match: [{candidates[0]}]");
                    return candidates[0];
                }
                if (candidates.Count > 1)
                {
                    // Prefer the one closest to the model path
                    var best = candidates.OrderByDescending(c =>
                        GetCommonPrefixLength(c, modelPath)).First();
                    logger?.Info($"[Keynotes] DC found {candidates.Count} matches, using closest: [{best}]");
                    return best;
                }
            }
            catch (Exception ex) { logger?.Warning($"[Keynotes] DC search error: {ex.Message}"); }
            return null;
        }

        /// <summary>
        /// Converts an Autodesk Docs cloud path to the local Desktop Connector path.
        /// "Autodesk Docs://Org/Project/path/file.txt" → local DC sync folder.
        /// Desktop Connector root varies per user — scans common locations.
        /// </summary>
        private static string? ConvertCloudPathToLocal(string cloudPath, ILogger? logger)
        {
            try
            {
                // Known cloud prefixes and their local DC subfolder names
                string[][] mappings = new[]
                {
                    new[] { "Autodesk Docs://", "ACCDocs" },
                    new[] { "BIM 360://", "BIM 360" },
                    new[] { "A360://", "A360" },
                };

                string? prefix = null;
                string? subFolder = null;
                string? relativePath = null;

                foreach (var mapping in mappings)
                {
                    if (cloudPath.StartsWith(mapping[0], StringComparison.OrdinalIgnoreCase))
                    {
                        prefix = mapping[0];
                        subFolder = mapping[1];
                        relativePath = cloudPath.Substring(prefix.Length).Replace('/', '\\');
                        break;
                    }
                }

                if (relativePath == null || subFolder == null) return null;

                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Desktop Connector root locations to search (varies per user config)
                var dcRoots = new List<string>
                {
                    Path.Combine(userProfile, "DC"),           // default: C:\Users\X\DC
                    userProfile,                                // C:\Users\X (ACCDocs directly in profile)
                    Path.Combine(userProfile, "Documents"),     // some users put it in Documents
                };

                // Also check all drive roots (D:\DC, E:\DC, etc.)
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        dcRoots.Add(Path.Combine(drive.RootDirectory.FullName, "DC"));
                        dcRoots.Add(drive.RootDirectory.FullName);
                    }
                }

                foreach (var root in dcRoots)
                {
                    var candidate = Path.Combine(root, subFolder, relativePath);
                    if (File.Exists(candidate))
                    {
                        logger?.Info($"[Keynotes] Cloud→Local found: [{candidate}]");
                        return candidate;
                    }
                }

                // Last resort: search by filename in all found ACCDocs folders
                var fileName = Path.GetFileName(relativePath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    foreach (var root in dcRoots)
                    {
                        var accDir = Path.Combine(root, subFolder);
                        if (Directory.Exists(accDir))
                        {
                            try
                            {
                                foreach (var f in Directory.EnumerateFiles(accDir, fileName, SearchOption.AllDirectories))
                                {
                                    logger?.Info($"[Keynotes] Cloud→Local found by name: [{f}]");
                                    return f;
                                }
                            }
                            catch { }
                        }
                    }
                }

                logger?.Info($"[Keynotes] Cloud→Local: no local file found for [{cloudPath}]");
            }
            catch (Exception ex) { logger?.Warning($"[Keynotes] CloudToLocal error: {ex.Message}"); }
            return null;
        }

        private static void SearchForKeynoteFiles(string dir, List<string> results, ILogger? logger)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*.txt"))
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (name.Contains("keynote") || name.Contains("notasclave") || name.Contains("notas_clave"))
                    {
                        results.Add(f);
                        logger?.Info($"[Keynotes] DC candidate: [{f}]");
                    }
                }
            }
            catch { }
        }

        private static int GetCommonPrefixLength(string a, string b)
        {
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
                if (char.ToLowerInvariant(a[i]) != char.ToLowerInvariant(b[i])) return i;
            return len;
        }
    }
}
