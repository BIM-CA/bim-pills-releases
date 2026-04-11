using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BIMPills.UI.Shared;

namespace BIMPills.UI.Sandbox
{
    public partial class BimPillsDialogShowcase : Window
    {
        public BimPillsDialogShowcase()
        {
            InitializeComponent();
        }

        private void SetResult(string text) => ResultText.Text = $"Último resultado: {text}";

        // ── Variantes simples ───────────────────────────────────────────────

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            BimPillsDialog.Info(
                header: "Exportación iniciada",
                message: "Los archivos se están generando en segundo plano. Podés seguir el progreso en la ventana de exportación.",
                owner: this);
            SetResult("Info mostrado");
        }

        private void Success_Click(object sender, RoutedEventArgs e)
        {
            BimPillsDialog.Success(
                header: "Exportación completada",
                message: "Se exportaron 12 planos a PDF sin errores.",
                owner: this);
            SetResult("Success mostrado");
        }

        private void Warning_Click(object sender, RoutedEventArgs e)
        {
            BimPillsDialog.Warning(
                header: "Advertencia",
                message: "3 vistas del conjunto no se encontraron en el modelo actual.",
                owner: this);
            SetResult("Warning mostrado");
        }

        private void Error_Click(object sender, RoutedEventArgs e)
        {
            BimPillsDialog.Error(
                header: "No se pudo exportar",
                message: "La carpeta de destino no está disponible o no tenés permisos de escritura.",
                owner: this);
            SetResult("Error mostrado");
        }

        // ── Confirmaciones ──────────────────────────────────────────────────

        private void ConfirmExport_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = BimPillsDialog.Confirm(
                header: "¿Iniciar exportación?",
                message: "Se exportarán 8 items a PDF y DWG (total: 16 archivos).",
                detail: "Destino: C:\\Exports\\Proyecto_Demo\n\n" +
                        "Durante la exportación Revit quedará ocupado y no podrás usarlo. " +
                        "Mantendremos abierta una ventana de progreso que podrás cancelar en cualquier momento.",
                owner: this,
                yesText: "Exportar",
                noText: "Cancelar");
            SetResult(confirmed ? "Confirm → Sí (Exportar)" : "Confirm → No (Cancelar)");
        }

        private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            var confirmed = BimPillsDialog.Confirm(
                header: "¿Eliminar conjunto?",
                message: "El conjunto «Presentación cliente» se eliminará permanentemente.",
                detail: "Esta acción no se puede deshacer.",
                owner: this,
                yesText: "Eliminar",
                noText: "Cancelar",
                kind: BimPillsDialog.DialogKind.Warning);
            SetResult(confirmed ? "Confirm → Sí (Eliminar)" : "Confirm → No (Cancelar)");
        }

        private void YesNoCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = BimPillsDialog.YesNoCancel(
                header: "Hay cambios sin guardar",
                message: "¿Querés guardar los cambios del perfil de exportación antes de cerrar?",
                detail: "Si cerrás sin guardar, los cambios se perderán.",
                owner: this,
                yesText: "Guardar",
                noText: "Descartar",
                cancelText: "Cancelar");
            SetResult($"YesNoCancel → {result}");
        }

        // ── Con detalle largo ───────────────────────────────────────────────

        private void SuccessDetail_Click(object sender, RoutedEventArgs e)
        {
            BimPillsDialog.Success(
                header: "Conjunto creado",
                message: "«Presentación cliente» guardado con 24 items.",
                detail: "Incluye 18 planos (Arquitectura, Estructura, MEP) y 6 vistas individuales (plantas, alzados, 3D). " +
                        "La configuración de exportación (formato, nombramiento, organización de carpetas) quedó vinculada al conjunto.",
                owner: this);
            SetResult("Success con detalle");
        }

        private void ErrorDetail_Click(object sender, RoutedEventArgs e)
        {
            BimPillsDialog.Error(
                header: "Error inesperado",
                message: "Ocurrió un error al iniciar la exportación.",
                detail: "System.IO.IOException: El proceso no puede acceder al archivo porque está siendo utilizado por otro proceso.\n" +
                        "   en System.IO.FileStream.ValidateFileHandle(...)\n" +
                        "   en BIMPills.Revit.Commands.ExportFamilies.ExportSheetsHelper.ExportPdf(...)",
                owner: this);
            SetResult("Error con stack trace");
        }

        // ── Progress windows ────────────────────────────────────────────────

        private void ProgressFast_Click(object sender, RoutedEventArgs e)
        {
            RunProgressDemo(
                header: "Exportando planos",
                total: 20,
                stepMs: 120,
                failIndexes: Array.Empty<int>(),
                cancellable: false);
        }

        private void ProgressSlow_Click(object sender, RoutedEventArgs e)
        {
            RunProgressDemo(
                header: "Exportando modelos NWC",
                total: 8,
                stepMs: 650,
                failIndexes: new[] { 3, 6 },
                cancellable: false);
        }

        private void ProgressCancellable_Click(object sender, RoutedEventArgs e)
        {
            RunProgressDemo(
                header: "Importando plantillas",
                total: 30,
                stepMs: 250,
                failIndexes: Array.Empty<int>(),
                cancellable: true);
        }

        /// <summary>
        /// Runs a fake long-running task and drives the branded progress window.
        /// Uses a DispatcherTimer so the UI stays responsive and the window is
        /// fully interactive (drag, cancel button). Mirrors the structure that
        /// the real ExportFamiliesRevitCommand uses with the Revit Idling event.
        /// </summary>
        private void RunProgressDemo(string header, int total, int stepMs, int[] failIndexes, bool cancellable)
        {
            var progress = new BimPillsProgressWindow(
                header: header,
                total:  total,
                message: $"Procesando {total} elementos…")
            {
                Owner = this
            };

            int i = 0;
            int failed = 0;
            bool cancelled = false;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(stepMs) };
            progress.Cancelled += (_, __) => cancelled = true;

            timer.Tick += (_, __) =>
            {
                if (cancelled || i >= total)
                {
                    timer.Stop();
                    progress.Complete();

                    string message = cancelled
                        ? $"{i} de {total} procesados antes de cancelar"
                        : $"{i - failed} de {total} procesados";

                    string? detail = failed > 0
                        ? $"{failed} elementos fallaron durante el proceso."
                        : null;

                    if (cancelled)
                        SetResult($"Progress cancelado tras {i} elementos");
                    else if (failed > 0)
                        SetResult($"Progress terminado con {failed} fallos");
                    else
                        SetResult($"Progress completado ({total} elementos)");

                    if (cancelled || failed > 0)
                        BimPillsDialog.Warning(
                            header: cancelled ? "Proceso cancelado" : "Proceso con avisos",
                            message: message,
                            detail: detail,
                            owner: this);
                    else
                        BimPillsDialog.Success(
                            header: "Proceso completado",
                            message: message,
                            owner: this);
                    return;
                }

                i++;
                bool isFail = Array.IndexOf(failIndexes, i) >= 0;
                if (isFail) failed++;

                string file = isFail
                    ? $"Plano_{i:D2}.pdf — ERROR"
                    : $"Plano_{i:D2}.pdf";

                progress.Report(
                    current: i,
                    total:   total,
                    currentItem: file,
                    message: cancellable
                        ? $"Procesando {i} de {total} (pulsá Cancelar para interrumpir)"
                        : $"Procesando {i} de {total}");
            };

            timer.Start();
            progress.Show();
        }
    }
}
