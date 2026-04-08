using BIMPills.Core.Models;
using BIMPills.UI.Helpers;
using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Keynotes
{
    // ── Value converters ──────────────────────────────────────────────────────

    /// <summary>int Level → Thickness left-margin (16px per level + 4px base).</summary>
    public sealed class LevelToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => new Thickness((value is int lvl ? lvl : 0) * 16 + 4, 0, 0, 0);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => 0;
    }

    /// <summary>bool IsGroup → Segoe MDL2 glyph: folder or document.</summary>
    public sealed class BoolToFolderIconConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is true) ? "\uE8B7" : "\uE8A5"; // Folder : Page
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => false;
    }

    /// <summary>bool IsGroup → brush: accent blue for folders, gray for items.</summary>
    public sealed class BoolToFolderColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush _blue = new(Color.FromRgb(0x15, 0x65, 0xC0));
        private static readonly SolidColorBrush _gray = new(Color.FromRgb(0x86, 0x86, 0x8B));
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is true) ? _blue : _gray;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => false;
    }

    /// <summary>bool IsExpanded → chevron glyph (▼ expanded / ▶ collapsed).</summary>
    public sealed class BoolToExpandIconConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is true) ? "\uE70D" : "\uE76C"; // ChevronDown : ChevronRight
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => true;
    }

    /// <summary>bool → Visibility (true = Visible).</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is true) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is Visibility.Visible;
    }

    /// <summary>bool → Visibility inverted (true = Collapsed).</summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => (value is true) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is Visibility.Collapsed;
    }

    // ── Panel ─────────────────────────────────────────────────────────────────

    public partial class KeynotesPanel : UserControl
    {
        // ── Callbacks ────────────────────────────────────────────────────────
        private Func<string, bool>? _reloadInRevitCallback;

        // ── State ────────────────────────────────────────────────────────────
        private List<KeynoteEntry> _allEntries = new();
        private string?            _currentFile;
        private bool               _hasChanges;

        // ── Drag-drop state ──────────────────────────────────────────────────
        private KeynoteEntry? _dragSource;
        private Point         _dragStartPoint;
        private KeynoteEntry? _currentDropTarget;

        // Auto-scroll during drag
        private readonly System.Windows.Threading.DispatcherTimer _scrollTimer = new()
            { Interval = TimeSpan.FromMilliseconds(40) };
        private double _scrollDirection = 0; // -1 = up, +1 = down

        // Auto-save debounce (1.5s after last change)
        private readonly System.Windows.Threading.DispatcherTimer _autoSaveTimer = new()
            { Interval = TimeSpan.FromMilliseconds(1500) };

        // ── Constructor ──────────────────────────────────────────────────────
        public KeynotesPanel()
        {
            InitializeComponent();
            _scrollTimer.Tick += (_, _) =>
            {
                var sv = FindVisualChild<ScrollViewer>(KeynotesGrid);
                if (sv != null && _scrollDirection != 0)
                    sv.ScrollToVerticalOffset(sv.VerticalOffset + _scrollDirection * 24);
            };
            _autoSaveTimer.Tick += (_, _) =>
            {
                _autoSaveTimer.Stop();
                AutoSave();
            };
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void Initialize(
            string? keynoteFilePath = null,
            Func<string, bool>? reloadInRevitCallback = null)
        {
            _reloadInRevitCallback = reloadInRevitCallback;
            if (!string.IsNullOrEmpty(keynoteFilePath))
            {
                if (File.Exists(keynoteFilePath))
                {
                    LoadFromFile(keynoteFilePath);
                }
                else
                {
                    // Revit has a keynote file configured but it's not on disk
                    _currentFile = null;
                    FilePathBox.Text = keynoteFilePath;
                    EmptyStateTitle.Text = "Archivo no encontrado";
                    EmptyStateSubtitle.Text = $"Revit tiene configurado:\n{Path.GetFileName(keynoteFilePath)}\npero no se encontr\u00f3 en disco.";
                    UpdateEmptyState();
                }
            }
            else
            {
                UpdateEmptyState();
            }
        }

        private void UpdateEmptyState()
        {
            bool hasFile = _currentFile != null;
            EmptyStateOverlay.Visibility = hasFile ? Visibility.Collapsed : Visibility.Visible;
            AddFolderButton.IsEnabled    = hasFile;
            AddKeynoteButton.IsEnabled   = hasFile;
            SaveTxtButton.IsEnabled      = hasFile && _hasChanges;
        }

        // ── File operations ──────────────────────────────────────────────────

        private void LoadFromFile(string path)
        {
            try
            {
                _allEntries  = ParseKeynoteFile(path).ToList();
                _currentFile = path;
                FilePathBox.Text = path;
                _hasChanges = false;
                RefreshGrid();
                UpdateStatus();
                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al leer el archivo:\n{ex.Message}",
                    "BIM Pills \u2014 Notas Clave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static IEnumerable<KeynoteEntry> ParseKeynoteFile(string path)
        {
            // Revit keynote files may use UTF-16, UTF-8 with BOM, or ANSI encoding
            var encoding = DetectEncoding(path);
            foreach (var raw in File.ReadLines(path, encoding))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                var parts = line.Split('\t');
                if (parts.Length < 2) continue;
                yield return new KeynoteEntry
                {
                    Key         = parts[0].Trim(),
                    Description = parts[1].Trim(),
                    ParentKey   = parts.Length > 2 ? parts[2].Trim() : ""
                };
            }
        }

        private static System.Text.Encoding DetectEncoding(string path)
        {
            var bom = new byte[4];
            using (var fs = File.OpenRead(path))
                fs.Read(bom, 0, Math.Min(4, (int)fs.Length));

            if (bom[0] == 0xFF && bom[1] == 0xFE) return System.Text.Encoding.Unicode;        // UTF-16 LE
            if (bom[0] == 0xFE && bom[1] == 0xFF) return System.Text.Encoding.BigEndianUnicode; // UTF-16 BE
            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return System.Text.Encoding.UTF8;
            return System.Text.Encoding.Default; // ANSI fallback
        }


        // ── Grid ─────────────────────────────────────────────────────────────

        private void RefreshGrid()
        {
            var q = SearchBox.Text.Trim().ToLowerInvariant();
            IEnumerable<KeynoteEntry> source = string.IsNullOrEmpty(q)
                ? SortedTreeOrder(_allEntries)
                : _allEntries.Where(e =>
                    e.Key.ToLowerInvariant().Contains(q) ||
                    e.Description.ToLowerInvariant().Contains(q));
            KeynotesGrid.ItemsSource = source.ToList();
        }

        /// <summary>Returns entries in parent-before-children tree order, respecting IsExpanded.</summary>
        private static IEnumerable<KeynoteEntry> SortedTreeOrder(IEnumerable<KeynoteEntry> all)
        {
            var list = all.ToList();

            // New entries with blank Key can't be placed in the tree (would cause infinite recursion
            // because IsGroup==true when ParentKey=="" and Key=="" matches itself as its own child).
            // Emit them at the bottom instead.
            var blank = list.Where(e => string.IsNullOrEmpty(e.Key)).ToList();
            var tree  = list.Where(e => !string.IsNullOrEmpty(e.Key)).ToList();

            var roots = tree.Where(e => e.IsGroup).OrderBy(e => e.Key).ToList();
            foreach (var root in roots)
            {
                yield return root;
                if (root.IsExpanded)
                    foreach (var child in GetDescendants(root, tree))
                        yield return child;
            }
            // Orphans: entries with a ParentKey that doesn't match any entry's Key
            var allKeys = new HashSet<string>(tree.Select(e => e.Key));
            foreach (var orphan in tree.Where(e => !e.IsGroup
                && !string.IsNullOrEmpty(e.ParentKey)
                && !allKeys.Contains(e.ParentKey)))
                yield return orphan;

            // Blank-key entries (new, unsaved) go last
            foreach (var b in blank)
                yield return b;
        }

        private static IEnumerable<KeynoteEntry> GetDescendants(
            KeynoteEntry parent, List<KeynoteEntry> all)
        {
            // Guard: skip self-reference and blank keys to prevent infinite recursion
            var children = all.Where(e => e != parent
                                       && !string.IsNullOrEmpty(e.Key)
                                       && e.ParentKey == parent.Key)
                              .OrderBy(e => e.Key);
            foreach (var child in children)
            {
                yield return child;
                if (!child.IsGroup || child.IsExpanded)
                    foreach (var grandchild in GetDescendants(child, all))
                        yield return grandchild;
            }
        }

        private void UpdateStatus()
        {
            int total    = _allEntries.Count;
            int groups   = _allEntries.Count(e => e.IsGroup);
            int modified = _allEntries.Count(e => e.IsModified && !e.IsNew);
            int added    = _allEntries.Count(e => e.IsNew);

            var parts = new List<string> { $"{total} entradas", $"{groups} grupos" };
            if (modified > 0) parts.Add($"{modified} modificadas");
            if (added    > 0) parts.Add($"{added} nuevas");
            StatusText.Text = string.Join(" \u00b7 ", parts);
        }

        private void MarkChanged()
        {
            _hasChanges = true;
            SaveTxtButton.IsEnabled = _currentFile != null;
            // Restart debounce timer for auto-save
            if (_currentFile != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }

        private void AutoSave()
        {
            if (_currentFile == null || !_hasChanges) return;
            try
            {
                var ordered = SortedTreeOrder(_allEntries).ToList();
                File.WriteAllLines(_currentFile,
                    ordered.Where(en => !string.IsNullOrEmpty(en.Key))
                           .Select(en => $"{en.Key}\t{en.Description}\t{en.ParentKey}"));

                foreach (var entry in _allEntries)
                {
                    entry.IsModified = false;
                    entry.IsNew = false;
                }
                _hasChanges = false;
                SaveTxtButton.IsEnabled = false;
                StatusText.Text += " · Guardado";
            }
            catch { /* Auto-save silently fails — user can still manual-save */ }
        }

        // ── Drag-drop ─────────────────────────────────────────────────────────

        private void KeynotesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _dragSource = GetEntryFromPoint(e.GetPosition(KeynotesGrid));
        }

        private void KeynotesGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_currentFile == null) return;
            if (e.LeftButton != MouseButtonState.Pressed || _dragSource == null) return;
            var pos  = e.GetPosition(null);
            var diff = pos - _dragStartPoint;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var data = new DataObject(typeof(KeynoteEntry), _dragSource);
            DragDrop.DoDragDrop(KeynotesGrid, data, DragDropEffects.Move);
        }

        private void KeynotesGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(KeynoteEntry)))
            { e.Effects = DragDropEffects.None; e.Handled = true; return; }

            var pos = e.GetPosition(KeynotesGrid);

            // ── Auto-scroll zone (50px from top/bottom edge) ─────────────────
            const double zone = 50.0;
            if (pos.Y < zone)
                _scrollDirection = -1;
            else if (pos.Y > KeynotesGrid.ActualHeight - zone)
                _scrollDirection = 1;
            else
                _scrollDirection = 0;

            if (_scrollDirection != 0 && !_scrollTimer.IsEnabled) _scrollTimer.Start();
            else if (_scrollDirection == 0 && _scrollTimer.IsEnabled) _scrollTimer.Stop();

            // ── Drop target highlight ────────────────────────────────────────
            var target = GetEntryFromPoint(pos);
            if (_currentDropTarget != target)
            {
                if (_currentDropTarget != null) _currentDropTarget.IsDropTarget = false;
                _currentDropTarget = target;
                if (_currentDropTarget != null) _currentDropTarget.IsDropTarget = true;
            }

            e.Effects = (target != null && target != _dragSource)
                ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void KeynotesGrid_DragLeave(object sender, DragEventArgs e)
        {
            _scrollTimer.Stop();
            _scrollDirection = 0;
            ClearDropHighlight();
        }

        private void KeynotesGrid_Drop(object sender, DragEventArgs e)
        {
            _scrollTimer.Stop();
            _scrollDirection = 0;
            ClearDropHighlight();
            if (!e.Data.GetDataPresent(typeof(KeynoteEntry))) return;

            var source = (KeynoteEntry)e.Data.GetData(typeof(KeynoteEntry));
            var target = GetEntryFromPoint(e.GetPosition(KeynotesGrid));

            if (target == null || target == source) return;
            if (IsDescendantOf(target, source)) return; // prevent circular

            // Reparent
            source.ParentKey = target.Key;

            // Auto re-index: assign new key based on position among siblings
            ReIndexEntry(source);

            MarkChanged();
            RefreshGrid();
            UpdateStatus();
        }

        /// <summary>
        /// Re-indexes an entry and all its descendants after a reparent operation.
        /// Assigns a key like "02.3" = parent.Key + "." + (sibling position).
        /// </summary>
        private void ReIndexEntry(KeynoteEntry entry)
        {
            if (string.IsNullOrEmpty(entry.ParentKey)) return; // root entries keep their key

            var siblings = _allEntries
                .Where(e => e.ParentKey == entry.ParentKey && e != entry)
                .OrderBy(e => e.Key)
                .ToList();

            // Append entry at end of siblings
            int pos = siblings.Count + 1;
            var oldKey  = entry.Key;
            var newKey  = $"{entry.ParentKey}.{pos:D2}";
            if (newKey == oldKey) return;

            // Rename entry and cascade to all descendants
            RenameKeyRecursive(entry, newKey);
        }

        private void RenameKeyRecursive(KeynoteEntry entry, string newKey)
        {
            var oldKey = entry.Key;
            entry.Key = newKey;

            // Update children whose ParentKey matched the old key
            foreach (var child in _allEntries.Where(e => e.ParentKey == oldKey).ToList())
            {
                // Preserve the suffix after the old key
                var suffix = child.Key.Length > oldKey.Length
                    ? child.Key.Substring(oldKey.Length)
                    : "";
                RenameKeyRecursive(child, newKey + suffix);
            }
        }

        private void ClearDropHighlight()
        {
            if (_currentDropTarget != null)
            {
                _currentDropTarget.IsDropTarget = false;
                _currentDropTarget = null;
            }
        }

        /// <summary>Returns the KeynoteEntry under the given point in the DataGrid, or null.</summary>
        private KeynoteEntry? GetEntryFromPoint(Point pt)
        {
            var hit = VisualTreeHelper.HitTest(KeynotesGrid, pt);
            if (hit == null) return null;
            var row = FindVisualParent<DataGridRow>(hit.VisualHit);
            return row?.Item as KeynoteEntry;
        }

        /// <summary>Returns true if <paramref name="candidate"/> is a descendant of <paramref name="ancestor"/>.</summary>
        private bool IsDescendantOf(KeynoteEntry candidate, KeynoteEntry ancestor)
        {
            var current = candidate;
            for (int safety = 0; safety < 50; safety++)
            {
                if (string.IsNullOrEmpty(current.ParentKey)) return false;
                if (current.ParentKey == ancestor.Key) return true;
                current = _allEntries.FirstOrDefault(e => e.Key == current.ParentKey)!;
                if (current == null) return false;
            }
            return false;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T match) return match;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void ToggleExpand_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is KeynoteEntry entry && entry.IsGroup)
            {
                entry.IsExpanded = !entry.IsExpanded;
                RefreshGrid();
                e.Handled = true; // prevent DataGrid from entering edit mode
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in _allEntries.Where(en => en.IsGroup))
                entry.IsExpanded = true;
            RefreshGrid();
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in _allEntries.Where(en => en.IsGroup))
                entry.IsExpanded = false;
            RefreshGrid();
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e) => RefreshGrid();

        private void KeynotesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => DeleteRowButton.IsEnabled = KeynotesGrid.SelectedItem != null;

        private void KeynotesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            MarkChanged();
            Dispatcher.BeginInvoke(new Action(UpdateStatus),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var key = NextRootKey();
            var entry = new KeynoteEntry { Key = key, Description = "Nueva carpeta", ParentKey = "", IsNew = true };
            _allEntries.Add(entry);
            MarkChanged();
            RefreshGrid();
            UpdateStatus();
            KeynotesGrid.SelectedItem = entry;
            KeynotesGrid.ScrollIntoView(entry);
        }

        private void AddKeynote_Click(object sender, RoutedEventArgs e)
        {
            // Determine parent: use selected group's key, or selected item's parent
            string parentKey = "";
            if (KeynotesGrid.SelectedItem is KeynoteEntry sel)
                parentKey = sel.IsGroup ? sel.Key : sel.ParentKey;

            if (string.IsNullOrEmpty(parentKey))
            {
                MessageBox.Show("Selecciona una carpeta donde agregar la nota clave.",
                    "BIM Pills \u2014 Notas Clave", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var key = NextChildKey(parentKey);
            var entry = new KeynoteEntry { Key = key, Description = "", ParentKey = parentKey, IsNew = true };
            _allEntries.Add(entry);

            // Ensure parent is expanded so the new entry is visible
            var parent = _allEntries.FirstOrDefault(e => e.Key == parentKey);
            if (parent != null) parent.IsExpanded = true;

            MarkChanged();
            RefreshGrid();
            UpdateStatus();
            KeynotesGrid.SelectedItem = entry;
            KeynotesGrid.ScrollIntoView(entry);
        }

        private string NextRootKey()
        {
            var maxNum = _allEntries
                .Where(e => e.IsGroup && !string.IsNullOrEmpty(e.Key))
                .Select(e => { int.TryParse(e.Key.Split('.')[0], out var n); return n; })
                .DefaultIfEmpty(0)
                .Max();
            return (maxNum + 1).ToString("D2");
        }

        private string NextChildKey(string parentKey)
        {
            var prefix = parentKey + ".";
            var maxNum = _allEntries
                .Where(e => e.ParentKey == parentKey && !string.IsNullOrEmpty(e.Key))
                .Select(e =>
                {
                    var suffix = e.Key.StartsWith(prefix) ? e.Key.Substring(prefix.Length) : e.Key;
                    int.TryParse(suffix.Split('.')[0], out var n);
                    return n;
                })
                .DefaultIfEmpty(0)
                .Max();
            return $"{parentKey}.{(maxNum + 1):D2}";
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (KeynotesGrid.SelectedItem is not KeynoteEntry item) return;
            var confirm = MessageBox.Show(
                $"\u00bfEliminar \u00ab{item.Key}\u00bb?",
                "BIM Pills \u2014 Notas Clave",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;
            _allEntries.Remove(item);
            MarkChanged();
            RefreshGrid();
            UpdateStatus();
        }

        private void GenerateFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter    = "Archivo de notas clave (*.txt)|*.txt",
                Title     = "Generar nuevo archivo de notas clave",
                FileName  = "NotasClave.txt"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // Write a minimal template with instructions
                File.WriteAllLines(dlg.FileName, new[]
                {
                    "# Archivo de notas clave BIM Pills",
                    "# Formato: CLAVE[TAB]DESCRIPCION[TAB]CLAVE_PADRE",
                    "# Las líneas que comienzan con # son comentarios.",
                    "#",
                    "# Ejemplo:",
                    "# 01\tEstructura\t",
                    "# 01.01\tHormigón armado\t01",
                    "# 01.01.01\tHA-25/B/20/IIa\t01.01",
                });
                LoadFromFile(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear el archivo:\n{ex.Message}",
                    "BIM Pills \u2014 Notas Clave", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Archivo de notas clave (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                Title  = "Seleccionar archivo de notas clave"
            };
            if (dlg.ShowDialog() == true) LoadFromFile(dlg.FileName);
        }

        private void ReloadFile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null)
            { MessageBox.Show("No hay archivo cargado. Usa Examinar para seleccionar uno.", "BIM Pills \u2014 Notas Clave"); return; }

            if (_hasChanges)
            {
                var confirm = MessageBox.Show("Hay cambios sin guardar. \u00bfRecargar igualmente?",
                    "BIM Pills \u2014 Notas Clave", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (confirm != MessageBoxResult.Yes) return;
            }
            LoadFromFile(_currentFile);
        }

        private void SaveTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null)
            {
                var dlg = new SaveFileDialog { Filter = "Archivo de notas clave (*.txt)|*.txt", Title = "Guardar archivo de notas clave" };
                if (dlg.ShowDialog() != true) return;
                _currentFile = dlg.FileName;
            }

            try
            {
                // Sort in tree order before saving
                var ordered = SortedTreeOrder(_allEntries).ToList();
                File.WriteAllLines(_currentFile,
                    ordered.Select(en => $"{en.Key}\t{en.Description}\t{en.ParentKey}"));

                foreach (var entry in _allEntries) { entry.IsModified = false; entry.IsNew = false; }
                _hasChanges = false;
                SaveTxtButton.IsEnabled = false;
                FilePathBox.Text = _currentFile;
                RefreshGrid();
                UpdateStatus();

                if (_reloadInRevitCallback != null)
                {
                    var ask = MessageBox.Show("Archivo guardado.\n\n\u00bfRecargar en Revit ahora?",
                        "BIM Pills \u2014 Notas Clave", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                    if (ask == MessageBoxResult.Yes && !_reloadInRevitCallback(_currentFile))
                        MessageBox.Show("No se pudo recargar en Revit.", "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"Guardado: {Path.GetFileName(_currentFile)}",
                        "BIM Pills \u2014 Notas Clave", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Importar notas clave desde Excel" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var imported = new List<KeynoteEntry>();
                using var wb = new XLWorkbook(dlg.FileName);
                var ws = wb.Worksheets.First();
                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    var key = row.Cell(1).GetString().Trim();
                    if (!string.IsNullOrEmpty(key))
                        imported.Add(new KeynoteEntry
                        {
                            Key         = key,
                            Description = row.Cell(2).GetString().Trim(),
                            ParentKey   = row.Cell(3).GetString().Trim()
                        });
                }

                if (imported.Count == 0)
                { MessageBox.Show("No se encontraron entradas.", "BIM Pills \u2014 Notas Clave"); return; }

                var confirm = MessageBox.Show(
                    $"\u00bfSustituir las {_allEntries.Count} entradas con las {imported.Count} del Excel?",
                    "BIM Pills \u2014 Importar", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                if (confirm != MessageBoxResult.Yes) return;

                _allEntries = imported;
                MarkChanged();
                RefreshGrid();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar: {ex.Message}", "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_allEntries.Count == 0)
            { MessageBox.Show("No hay entradas para exportar.", "BIM Pills \u2014 Notas Clave"); return; }

            var dlg = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "NotasClave.xlsx" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Notas Clave");
                ws.Cell(1, 1).Value = "CLAVE";
                ws.Cell(1, 2).Value = "DESCRIPCI\u00d3N";
                ws.Cell(1, 3).Value = "PADRE";
                var hdr = ws.Range(1, 1, 1, 3);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                hdr.Style.Font.FontColor       = XLColor.White;

                var ordered = SortedTreeOrder(_allEntries).ToList();
                for (int i = 0; i < ordered.Count; i++)
                {
                    var en = ordered[i];
                    ws.Cell(i + 2, 1).Value = en.Key;
                    ws.Cell(i + 2, 2).Value = en.Description;
                    ws.Cell(i + 2, 3).Value = en.ParentKey;
                    if (en.IsGroup)
                        ws.Range(i + 2, 1, i + 2, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF3FF");
                }
                ws.Columns().AdjustToContents();
                wb.SaveAs(dlg.FileName);

                var ask = MessageBox.Show($"Exportado. \u00bfAbrir en Excel?",
                    "BIM Pills \u2014 Exportar", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ask == MessageBoxResult.Yes) ProcessHelper.OpenDocument(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "BIM Pills \u2014 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
