using BIMPills.Core.Models;
using BIMPills.UI.Shared;
using System;
using System.Windows;

namespace BIMPills.UI.MCPIntegration
{
    public partial class EditMCPConnectionWindow : Window
    {
        private readonly MCPConnectionConfig _connection;
        private readonly bool _isNew;

        public EditMCPConnectionWindow(MCPConnectionConfig connection, bool isNew = false)
        {
            _connection = connection;
            _isNew = isNew;
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            LoadConnection();
        }

        private void LoadConnection()
        {
            NameTextBox.Text = _connection.Name ?? "";
            DescriptionTextBox.Text = _connection.Description ?? "";
            EndpointTextBox.Text = _connection.Endpoint ?? "";

            // Load credentials list
            CredentialsList.ItemsSource = _connection.Credentials;

            // Show API Key if it exists
            if (_connection.Credentials.ContainsKey("api_key"))
            {
                ApiKeyTextBox.Text = _connection.Credentials["api_key"] ?? "";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                BimPillsDialog.Warning("BIMPills — Validación", "El nombre de la conexión no puede estar vacío.");
                return;
            }

            if (string.IsNullOrWhiteSpace(EndpointTextBox.Text))
            {
                BimPillsDialog.Warning("BIMPills — Validación", "El endpoint no puede estar vacío.");
                return;
            }

            _connection.Name = NameTextBox.Text.Trim();
            _connection.Description = DescriptionTextBox.Text.Trim();
            _connection.Endpoint = EndpointTextBox.Text.Trim();

            // Update API key if provided
            if (!string.IsNullOrWhiteSpace(ApiKeyTextBox.Text))
            {
                _connection.Credentials["api_key"] = ApiKeyTextBox.Text;
            }

            try { DialogResult = true; } catch (InvalidOperationException) { }
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch (InvalidOperationException) { }
            Close();
        }
    }
}
