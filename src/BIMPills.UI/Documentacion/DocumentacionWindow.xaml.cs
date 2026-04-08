using BIMPills.Core.Documentacion;
using BIMPills.Core.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace BIMPills.UI.Documentacion
{
    public partial class DocumentacionWindow : Window
    {
        public DocumentacionWindow()
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
        }

        /// <summary>
        /// Initializes the Acotado de Vanos tab with data, execution callback and optional logger.
        /// </summary>
        public void InitializeAcotado(
            AcotadoVanosData data,
            Func<AcotadoVanosSettings, AcotadoVanosResult>? executeCallback = null,
            ILogger? logger = null)
        {
            AcotadoPanel.Initialize(data, executeCallback, logger);
        }

        /// <summary>
        /// Sets the document name in the header (from Revit).
        /// </summary>
        public void SetDocumentName(string name)
        {
            DocumentName.Text = name;
        }
    }
}
