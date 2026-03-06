using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace VTuberKitForYMM4.Commons
{
    public partial class ConsoleManager
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AllocConsole();

#if DEBUG
        private static bool isConsoleAllocated;

        public static void Debug(string message)
        {
            if (!isConsoleAllocated)
            {
                AllocConsole();
                isConsoleAllocated = true;
            }

            StackFrame frame = new(1);
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            var name = method?.Name;
            Console.WriteLine($" [{DateTime.Now}]  {type}.{name}(): {message}");
        }
#else
        public static void Debug(string message)
        {
        }
#endif

        public static void Error(string message)
        {
#if DEBUG
            StackFrame frame = new(1);
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            var name = method?.Name;
            Console.WriteLine($"[ERROR {DateTime.Now}] {type}.{name}(): {message}");
#endif
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
