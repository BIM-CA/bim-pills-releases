using System;

namespace BIMPills.Core.Services
{
    public interface ILogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception? exception = null);
    }
}
