using BIMPills.Commands.ModelAudit;
using System.Windows;

namespace BIMPills.UI.ModelAudit
{
    public partial class ModelAuditWindow : Window
    {
        public string WarningsHeader => $"Advertencias ({_result.Warnings.Count})";
        public string FamiliesHeader => $"Familias ({_result.Families.Count})";
        public string ViewsHeader    => $"Vistas sin colocar ({_result.UnplacedViews.Count})";
        public string OrphansHeader  => $"Elementos huérfanos ({_result.OrphanElements.Count})";

        private readonly ModelAuditResult _result;

        public ModelAuditWindow(ModelAuditResult result)
        {
            _result = result;
            InitializeComponent();
            DataContext = this;
            Populate();
        }

        private void Populate()
        {
            TitleText.Text    = $"Auditoría: {_result.DocumentTitle}";
            SubtitleText.Text = $"Modelo {(_result.IsWorkshared ? "colaborativo" : "local")}";

            WarningsGrid.ItemsSource = _result.Warnings;
            FamiliesGrid.ItemsSource = _result.Families;
            ViewsGrid.ItemsSource    = _result.UnplacedViews;
            OrphansGrid.ItemsSource  = _result.OrphanElements;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
