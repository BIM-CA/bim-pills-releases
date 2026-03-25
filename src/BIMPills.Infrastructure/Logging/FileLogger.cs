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
            var logFile = Path.Combine(logDirectory, $"BIMPills_{DateTime.Now:yyyyMMdd}.log");
            _writer = new StreamWriter(logFile, append: true) { AutoFlush = true };
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
