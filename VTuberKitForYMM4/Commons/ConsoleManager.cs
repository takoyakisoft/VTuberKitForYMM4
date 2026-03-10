using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace VTuberKitForYMM4.Commons
{
    public partial class ConsoleManager
    {
        private static readonly object fileLock = new();
        private static readonly string logFilePath = Path.Combine(Path.GetTempPath(), "VTuberKitForYMM4.debug.log");

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AllocConsole();

        private static void EnsureConsole()
        {
#if DEBUG
            if (!isConsoleAllocated)
            {
                AllocConsole();
                isConsoleAllocated = true;
            }
#endif
        }

#if DEBUG
        private static bool isConsoleAllocated;

        private static void WriteLog(string level, string message, int skipFrames = 2)
        {
            EnsureConsole();

            StackFrame frame = new(skipFrames);
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            var name = method?.Name;
            var line = $"[{level} {DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}] {type}.{name}(): {message}";
            Console.WriteLine(line);

            lock (fileLock)
            {
                File.AppendAllText(logFilePath, line + Environment.NewLine);
            }
        }

        public static void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }
#else
        public static void Debug(string message)
        {
        }
#endif

        public static void Error(string message)
        {
#if DEBUG
            WriteLog("ERROR", message);
#endif
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
