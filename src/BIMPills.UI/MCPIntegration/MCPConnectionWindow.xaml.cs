using BIMPills.Core.Models;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Security;
using BIMPills.Core.Services;
using BIMPills.UI.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace BIMPills.UI.MCPIntegration
{
    public partial class MCPConnectionWindow : Window
    {
        // ── Claude AI config ──────────────────────────────────────────────────────
        private static readonly string _claudeConfigPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "BIMPills", "claude_config.json");

        // ── MCP connections ───────────────────────────────────────────────────────
        private readonly List<McpConnectionViewModel> _mcpConnections = new List<McpConnectionViewModel>();
        private IMCPDiscoveryService? _mcpService;

        public MCPConnectionWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            LoadSavedClaudeConfig();
            _ = LoadMcpConnectionsAsync();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ════════════════════════════════════════════════════════════════════════
        //  Tab 1 — Claude AI
        // ════════════════════════════════════════════════════════════════════════

        private void LoadSavedClaudeConfig()
        {
            try
            {
                if (!System.IO.File.Exists(_claudeConfigPath)) return;

                var json = System.IO.File.ReadAllText(_claudeConfigPath, Encoding.UTF8);
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ClaudeConfig>(
                    json,
                    new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None });

                if (data == null || string.IsNullOrEmpty(data.ApiKey)) return;

                // Decrypt the stored API key (DPAPI). If decryption fails the value is
                // legacy plaintext — use it as-is and re-save in encrypted form.
                if (!SecureStorage.TryUnprotect(data.ApiKey, out var apiKey))
                {
                    Debug.WriteLine("[BIMPills] Migrating Claude API key to encrypted storage.");
                    SaveClaudeConfig(apiKey, data.Model); // re-save encrypted
                }

                ApiKeyBox.Password = apiKey;
                SetClaudeStatus(true, "Configurado — API Key guardada");
                foreach (System.Windows.Controls.ComboBoxItem item in ModelCombo.Items)
                    if (item.Tag?.ToString() == data.Model)
                        item.IsSelected = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BIMPills] Error loading Claude config: {ex.Message}");
            }
        }

        /// <summary>Encrypts <paramref name="apiKey"/> with DPAPI and writes the config file.</summary>
        private void SaveClaudeConfig(string apiKey, string model)
        {
            var encrypted = SecureStorage.Protect(apiKey);
            var config    = new ClaudeConfig { ApiKey = encrypted, Model = model };
            var dir       = System.IO.Path.GetDirectoryName(_claudeConfigPath)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(
                _claudeConfigPath,
                Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented),
                Encoding.UTF8);
        }

        private void ApiKey_Changed(object sender, RoutedEventArgs e)
        {
            SetClaudeStatus(false, "No configurado — ingresa tu API Key para comenzar");
        }

        private void SetClaudeStatus(bool ok, string message)
        {
            StatusText.Text = message;
            if (ok)
            {
                StatusBadge.Background   = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9));
                StatusBadge.BorderBrush  = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
                StatusText.Foreground    = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
                StatusIcon.Text          = "\uE73E"; // CheckMark
                StatusIcon.Foreground    = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
            }
            else
            {
                StatusBadge.Background   = new SolidColorBrush(Color.FromRgb(0xFF, 0xF3, 0xE0));
                StatusBadge.BorderBrush  = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
                StatusText.Foreground    = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
                StatusIcon.Text          = "\uE7BA"; // Info
                StatusIcon.Foreground    = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = ApiKeyBox.Password?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                SetClaudeStatus(false, "Ingresa una API Key para probar");
                return;
            }

            TestBtn.IsEnabled = false;
            SetClaudeStatus(false, "Probando conexión...");

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                client.Timeout = TimeSpan.FromSeconds(10);

                var body = "{\"model\":\"claude-haiku-4-5\",\"max_tokens\":10,\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}]}";
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);

                if (response.IsSuccessStatusCode)
                    SetClaudeStatus(true, "✓ Conexión exitosa — API Key válida");
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    SetClaudeStatus(false, "API Key inválida — verifica en console.anthropic.com");
                else
                    SetClaudeStatus(false, $"Error {(int)response.StatusCode} — verifica tu conexión a internet");
            }
            catch (TaskCanceledException)
            {
                SetClaudeStatus(false, "Tiempo de espera agotado — verifica tu conexión");
            }
            catch (Exception ex)
            {
                SetClaudeStatus(false, $"Error: {ex.Message}");
            }
            finally
            {
                TestBtn.IsEnabled = true;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = ApiKeyBox.Password?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                BimPillsDialog.Warning(
                    header: "API Key requerida",
                    message: "Ingresa una API Key antes de guardar.",
                    owner: this);
                return;
            }

            try
            {
                var selectedModel = (ModelCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)
                    ?.Tag?.ToString() ?? "claude-sonnet-4-5";

                SaveClaudeConfig(apiKey, selectedModel);   // ← encrypts with DPAPI

                SetClaudeStatus(true, "✓ Configuración guardada correctamente");
                BimPillsDialog.Success(
                    header: "Claude AI configurado",
                    message: "La configuración se guardó correctamente.",
                    detail: "Ahora puedes usar el análisis AI desde el informe de auditoría.",
                    owner: this);
            }
            catch (Exception ex)
            {
                BimPillsDialog.Error(
                    header: "Error al guardar",
                    message: "No se pudo guardar la configuración de Claude AI.",
                    detail: ex.Message,
                    owner: this);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Tab 2 — MCP Personalizado
        // ════════════════════════════════════════════════════════════════════════

        private async Task LoadMcpConnectionsAsync()
        {
            try
            {
                if (ServiceLocator.IsRegistered<IMCPDiscoveryService>())
                    _mcpService = ServiceLocator.Get<IMCPDiscoveryService>();

                if (_mcpService != null)
                {
                    var connections = await _mcpService.GetAllConnectionsAsync();
                    foreach (var cfg in connections)
                        _mcpConnections.Add(new McpConnectionViewModel(cfg));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BIMPills] Error loading MCP connections: {ex.Message}");
            }

            RefreshMcpList();
        }

        private void RefreshMcpList()
        {
            McpConnectionsList.ItemsSource = null;
            McpConnectionsList.ItemsSource = _mcpConnections;
            bool hasSelection = McpConnectionsList.SelectedItem != null;
            TestMcpBtn.IsEnabled   = hasSelection;
            DeleteMcpBtn.IsEnabled = hasSelection;
        }

        private void McpConnections_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasSelection = McpConnectionsList.SelectedItem != null;
            TestMcpBtn.IsEnabled   = hasSelection;
            DeleteMcpBtn.IsEnabled = hasSelection;
        }

        private void NewMcpConnection_Click(object sender, RoutedEventArgs e)
        {
            McpFormPanel.Visibility = McpFormPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (McpFormPanel.Visibility == Visibility.Visible)
            {
                McpNameBox.Text     = "";
                McpEndpointBox.Text = "http://localhost:3000";
                McpNameBox.Focus();
            }
        }

        private void CancelMcp_Click(object sender, RoutedEventArgs e)
        {
            McpFormPanel.Visibility = Visibility.Collapsed;
        }

        private async void AddMcp_Click(object sender, RoutedEventArgs e)
        {
            var name     = McpNameBox.Text?.Trim();
            var endpoint = McpEndpointBox.Text?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                BimPillsDialog.Warning(
                    header: "Nombre requerido",
                    message: "Ingresa un nombre para la conexión.",
                    owner: this);
                return;
            }
            if (string.IsNullOrEmpty(endpoint))
            {
                BimPillsDialog.Warning(
                    header: "Endpoint requerido",
                    message: "Ingresa el endpoint (URL o ruta local) de la conexión.",
                    owner: this);
                return;
            }

            try
            {
                var cfg = new MCPConnectionConfig
                {
                    Id       = Guid.NewGuid().ToString(),
                    Name     = name,
                    Endpoint = endpoint,
                };

                if (_mcpService != null)
                {
                    await _mcpService.CreateConnectionAsync(cfg);
                }

                _mcpConnections.Add(new McpConnectionViewModel(cfg));
                McpFormPanel.Visibility = Visibility.Collapsed;
                RefreshMcpList();
            }
            catch (Exception ex)
            {
                BimPillsDialog.Error(
                    header: "Error al agregar conexión",
                    message: "No se pudo agregar la conexión MCP.",
                    detail: ex.Message,
                    owner: this);
            }
        }

        private async void TestMcpConnection_Click(object sender, RoutedEventArgs e)
        {
            if (McpConnectionsList.SelectedItem is not McpConnectionViewModel vm) return;

            TestMcpBtn.IsEnabled = false;
            vm.StatusLabel = "Probando...";
            vm.StatusColor = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C));
            RefreshMcpList();
            McpConnectionsList.SelectedItem = vm;

            bool success = false;
            try
            {
                if (_mcpService != null)
                    success = await _mcpService.TestConnectionAsync(vm.Config);
                else
                {
                    // Fallback: simple HTTP GET
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var response = await client.GetAsync(vm.Config.Endpoint);
                    success = response.IsSuccessStatusCode || (int)response.StatusCode < 500;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BIMPills] Error testing MCP connection: {ex.Message}");
            }

            vm.StatusLabel = success ? "Conectado" : "Sin conexión";
            vm.StatusColor = success
                ? new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60))
                : new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
            vm.Config.Status = success ? MCPConnectionStatus.Connected : MCPConnectionStatus.Error;
            vm.Config.LastTestedAt = DateTime.UtcNow;

            RefreshMcpList();
            McpConnectionsList.SelectedItem = vm;
            TestMcpBtn.IsEnabled = true;
        }

        private async void DeleteMcpConnection_Click(object sender, RoutedEventArgs e)
        {
            if (McpConnectionsList.SelectedItem is not McpConnectionViewModel vm) return;

            var confirmed = BimPillsDialog.Confirm(
                header: "¿Eliminar conexión?",
                message: $"Se eliminará la conexión «{vm.Name}».",
                detail: "Esta acción no se puede deshacer.",
                owner: this,
                yesText: "Eliminar",
                noText: "Cancelar");

            if (!confirmed) return;

            try
            {
                if (_mcpService != null)
                {
                    await _mcpService.DeleteConnectionAsync(vm.Config.Id);
                }

                _mcpConnections.Remove(vm);
                RefreshMcpList();
            }
            catch (Exception ex)
            {
                BimPillsDialog.Error(
                    header: "Error al eliminar",
                    message: "No se pudo eliminar la conexión MCP.",
                    detail: ex.Message,
                    owner: this);
            }
        }
    }

    // ── Supporting types ─────────────────────────────────────────────────────────

    internal class ClaudeConfig
    {
        public string ApiKey { get; set; } = "";
        public string Model  { get; set; } = "claude-sonnet-4-5";
    }

    internal class McpConnectionViewModel
    {
        public MCPConnectionConfig Config { get; }

        public string Name        => Config.Name;
        public string Endpoint    => Config.Endpoint;
        public string StatusLabel { get; set; }
        public Brush  StatusColor { get; set; }

        public McpConnectionViewModel(MCPConnectionConfig config)
        {
            Config      = config;
            StatusLabel = config.Status switch
            {
                MCPConnectionStatus.Connected    => "Conectado",
                MCPConnectionStatus.Error        => "Error",
                MCPConnectionStatus.Disconnected => "Desconectado",
                _                                => "Desconocido"
            };
            StatusColor = config.Status switch
            {
                MCPConnectionStatus.Connected => new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
                MCPConnectionStatus.Error     => new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
                _                             => new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C))
            };
        }
    }
}
