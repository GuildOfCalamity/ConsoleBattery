using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleBattery
{
    internal static class Console
    {
        static bool addTimeStamp = false;

        /// <summary>
        /// Writes a message to the console with a timestamp.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public static void WriteLine(string message)
        {
            if (Program.Verbose)
                System.Console.WriteLine($"{(addTimeStamp ? $"[{DateTime.Now:HH:mm:ss.fff}]" : $"")} {message}");
        }

        /// <summary>
        /// Writes <see cref="Environment.NewLine"/> to the console.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public static void WriteLine()
        {
            if (Program.Verbose)
                System.Console.WriteLine();
        }

        /// <summary>
        /// Writes a message to the console without a newline.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public static void Write(string message)
        {
            if (Program.Verbose)
                System.Console.Write($"{(addTimeStamp ? $"[{DateTime.Now:HH:mm:ss.fff}]" : $"")} {message}");
        }

        /// <summary>
        /// Sets the cursor to the given coordinates.
        /// </summary>
        public static void SetCursorPosition(int left, int top)
        {
            System.Console.SetCursorPosition(left, top);
        }

        /// <summary>
        /// Clears the entire console window of any text.
        /// </summary>
        public static void Clear()
        {
            System.Console.Clear();
        }
    }
}
