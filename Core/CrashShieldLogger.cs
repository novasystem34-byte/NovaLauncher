using System;
using System.IO;
using System.Text;

namespace NovaLauncher.Core
{
    /// <summary>
    /// Centralized, defensive logging used across the launcher. Every call is
    /// wrapped so that a failure to write a log entry can never itself throw
    /// and crash the application - this is part of the Crash Shield design.
    /// </summary>
    public static class CrashShieldLogger
    {
        private static readonly object WriteLock = new();

        public static void LogInfo(string message) => SafeWrite("INFO", message);

        public static void LogWarning(string message) => SafeWrite("WARN", message);

        public static void LogError(string message, Exception? exception = null)
        {
            string fullMessage = message;
            Exception? current = exception;
            int depth = 0;

            while (current != null)
            {
                fullMessage += $"\n[المستوى {depth}] {current.GetType().Name}: {current.Message}\n{current.StackTrace}";
                current = current.InnerException;
                depth++;
            }

            SafeWrite("ERROR", fullMessage);
        }

        private static void SafeWrite(string level, string message)
        {
            try
            {
                lock (WriteLock)
                {
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(AppPaths.LogFile, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never be a source of instability. If the log file
                // cannot be written (locked disk, permissions, etc.) we silently
                // continue - the launcher's own operation always takes priority.
            }
        }
    }
}
