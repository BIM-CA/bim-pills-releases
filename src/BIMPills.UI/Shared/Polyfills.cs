// Polyfill: enables C# 9 'init' accessor on .NET Framework 4.8 (Revit 2024).
// On .NET 5+ the type is provided by the runtime; no duplicate is defined.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
