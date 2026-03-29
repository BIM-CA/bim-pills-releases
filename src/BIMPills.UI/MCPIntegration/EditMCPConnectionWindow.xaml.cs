using BIMPills.Core.Models;
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
                MessageBox.Show("El nombre de la conexión no puede estar vacío.", "BIMPills — Validación");
                return;
            }

            if (string.IsNullOrWhiteSpace(EndpointTextBox.Text))
            {
                MessageBox.Show("El endpoint no puede estar vacío.", "BIMPills — Validación");
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
