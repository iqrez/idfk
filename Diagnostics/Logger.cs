using System;
using System.IO;
using System.Text;
using System.Linq;

namespace WootMouseRemap
{
    public static class Logger
    {
        private static readonly object _lock = new();
        private static string _file = Path.Combine("Logs", "woot.log"); // Ensure logs go in Logs folder
        private const long MaxSize = 2 * 1024 * 1024;

        public static void Init(string file) => _file = Path.Combine("Logs", file); // Always use Logs folder

        public static void Info(string msg, params object[] args) => Write("INFO", msg, args);
        public static void Warn(string msg, params object[] args) => Write("WARN", msg, args);
        public static void Error(string msg, params object[] args)
        {
            Exception? ex = null;
            object[] formatArgs = args;
            if (args.Length > 0 && args[args.Length - 1] is Exception lastArg)
            {
                ex = lastArg;
                formatArgs = args.Take(args.Length - 1).ToArray();
            }
            Write("ERR ", msg + (ex != null ? "\n" + ex : ""), formatArgs);
        }
        public static void Debug(string msg, params object[] args) => Write("DBUG", msg, args);

        private static string SafeFormat(string format, params object[] args)
        {
            try
            {
                // Sanitize format string to prevent injection
                var safeFormat = SanitizeLogMessage(format);
                var safeArgs = args?.Select(arg => arg?.ToString()?.Replace("\n", "\\n")?.Replace("\r", "\\r") ?? "null").ToArray() ?? new string[0];
                return string.Format(safeFormat, safeArgs);
            }
            catch
            {
                return $"[LOG_FORMAT_ERROR] {SanitizeLogMessage(format)}";
            }
        }

        private static string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            
            // Remove potential log injection characters
            return message
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0")
                .Replace("\x1b", ""); // Remove ANSI escape sequences
        }

        private static void Write(string lvl, string msg, params object[] args)
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory("Logs"); // Ensure Logs folder exists
                    if (File.Exists(_file) && new FileInfo(_file).Length > MaxSize)
                    {
                        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        File.Move(_file, Path.Combine("Logs", $"woot_{ts}.log"));
                    }
                    var formattedMsg = args.Length > 0 ? SafeFormat(msg, args) : SanitizeLogMessage(msg);
                    File.AppendAllText(_file, $"{DateTime.Now:HH:mm:ss.fff} [{lvl}] {formattedMsg}\n", Encoding.UTF8);
                }
                catch { /* ignore */ }
            }
        }
    }
}
