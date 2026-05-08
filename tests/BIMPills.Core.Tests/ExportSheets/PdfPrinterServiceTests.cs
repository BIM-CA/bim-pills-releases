using BIMPills.Infrastructure.Services;
using Microsoft.Win32;
using System;
using System.Runtime.Versioning;
using Xunit;

#pragma warning disable CA1416 // Registry API es Windows-only — estos tests solo corren en Windows

namespace BIMPills.Core.Tests.ExportSheets
{
    /// <summary>
    /// Tests para PdfPrinterService.EnsureBimpillsHkcuServiceConfig().
    /// Estos tests escriben en HKCU (registro real del usuario de test).
    /// Limpian su propio rastro al terminar.
    /// </summary>
    public class PdfPrinterServiceHkcuTests : IDisposable
    {
        private const string KeyPath = @"SOFTWARE\PDF24\Services\bimpills";

        public PdfPrinterServiceHkcuTests()
        {
            // Asegurarse de que la clave no existe antes de cada test
            CleanupKey();
        }

        public void Dispose()
        {
            CleanupKey();
        }

        private static void CleanupKey()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\PDF24\Services\bimpills", throwOnMissingSubKey: false);
            }
            catch { /* best-effort */ }
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Fact]
        public void EnsureBimpillsHkcuServiceConfig_KeyAbsent_WritesKeyAndReturnsTrue()
        {
            // Arrange: clave no existe (limpiada en constructor)

            // Act
            var result = PdfPrinterService.EnsureBimpillsHkcuServiceConfig();

            // Assert: retornó true (fue la primera escritura)
            Assert.True(result);
        }

        [Fact]
        public void EnsureBimpillsHkcuServiceConfig_KeyAbsent_WritesHandlerAutoSave()
        {
            PdfPrinterService.EnsureBimpillsHkcuServiceConfig();

            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            Assert.NotNull(key);
            Assert.Equal("autoSave", key!.GetValue("Handler") as string, ignoreCase: true);
        }

        [Fact]
        public void EnsureBimpillsHkcuServiceConfig_KeyAbsent_WritesAllRequiredValues()
        {
            PdfPrinterService.EnsureBimpillsHkcuServiceConfig();

            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            Assert.NotNull(key);

            // String values
            Assert.False(string.IsNullOrEmpty(key!.GetValue("Port") as string));
            Assert.False(string.IsNullOrEmpty(key.GetValue("AutoSaveDir") as string));
            Assert.Equal("$fileName", key.GetValue("AutoSaveFilename") as string);

            // DWORD values
            Assert.Equal(0, (int)(key.GetValue("AutoSaveShowProgress") ?? -1));
            Assert.Equal(0, (int)(key.GetValue("AutoSaveUseFileChooser") ?? -1));
            Assert.Equal(1, (int)(key.GetValue("AutoSaveOverwriteFile") ?? -1));
        }

        [Fact]
        public void EnsureBimpillsHkcuServiceConfig_AlreadyConfigured_ReturnsFalse()
        {
            // Arrange: primera escritura
            PdfPrinterService.EnsureBimpillsHkcuServiceConfig();

            // Act: segunda llamada — ya estaba configurado
            var result = PdfPrinterService.EnsureBimpillsHkcuServiceConfig();

            // Assert: no-op, retorna false
            Assert.False(result);
        }

        [Fact]
        public void EnsureBimpillsHkcuServiceConfig_KeyExistsButWrongHandler_Overwrites()
        {
            // Arrange: clave existe pero con Handler incorrecto
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key?.SetValue("Handler", "openCreator", RegistryValueKind.String);
            }

            // Act
            var result = PdfPrinterService.EnsureBimpillsHkcuServiceConfig();

            // Assert: debe sobrescribir y retornar true
            Assert.True(result);

            using var check = Registry.CurrentUser.OpenSubKey(KeyPath);
            Assert.Equal("autoSave", check?.GetValue("Handler") as string, ignoreCase: true);
        }

        [Fact]
        public void EnsureBimpillsHkcuServiceConfig_DoesNotThrow_WhenCalledMultipleTimes()
        {
            // No debe lanzar excepción en ninguna invocación
            var ex = Record.Exception(() =>
            {
                PdfPrinterService.EnsureBimpillsHkcuServiceConfig();
                PdfPrinterService.EnsureBimpillsHkcuServiceConfig();
                PdfPrinterService.EnsureBimpillsHkcuServiceConfig();
            });

            Assert.Null(ex);
        }
    }
}
