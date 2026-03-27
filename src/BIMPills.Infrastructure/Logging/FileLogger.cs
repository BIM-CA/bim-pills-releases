using BIMPills.Core.Services;
using System;
using System.IO;

namespace BIMPills.Infrastructure.Logging
{
    public sealed class FileLogger : ILogger, IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new object();

        public FileLogger(string logDirectory)
        {
            Directory.CreateDirectory(logDirectory);
            var baseName = $"BIMPills_{DateTime.Now:yyyyMMdd}";
            _writer = OpenLogWriter(logDirectory, baseName);
        }

        private static StreamWriter OpenLogWriter(string dir, string baseName)
        {
            // Try the base name first, then append _2, _3, etc. if locked by another process
            for (int i = 0; i < 5; i++)
            {
                var fileName = i == 0 ? $"{baseName}.log" : $"{baseName}_{i + 1}.log";
                var path = Path.Combine(dir, fileName);
                try
                {
                    var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    return new StreamWriter(stream) { AutoFlush = true };
                }
                catch (IOException) { /* File locked — try next suffix */ }
            }

            // Last resort: unique name guaranteed not to collide
            var fallback = Path.Combine(dir, $"{baseName}_{Guid.NewGuid():N}.log");
            var fs = new FileStream(fallback, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            return new StreamWriter(fs) { AutoFlush = true };
        }

        public void Info(string message)    => Write("INFO   ", message, null);
        public void Warning(string message) => Write("WARN   ", message, null);
        public void Error(string message, Exception? exception = null) => Write("ERROR  ", message, exception);

        private void Write(string level, string message, Exception? ex)
        {
            lock (_lock)
            {
                _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}");
                if (ex != null)
                    _writer.WriteLine($"  Exception: {ex}");
            }
        }

        public void Dispose() => _writer?.Dispose();
    }
}
