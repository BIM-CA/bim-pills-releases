using System.Runtime.CompilerServices;

// Manually declared here (instead of via <AssemblyAttribute> in the csproj)
// because common.props disables <GenerateAssemblyInfo> for Release builds,
// which would otherwise strip the InternalsVisibleTo entry and break the
// test project's access to internal test-only constructors like
// AirtableLicenseService(string apiKey, LicenseCache?).
[assembly: InternalsVisibleTo("BIMPills.Core.Tests")]
