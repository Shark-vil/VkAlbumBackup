using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkAlbumBackup
{
    internal static class ConsoleService
    {
        internal static string EnteredKeys(params string[] enteredData)
        {
            string? inputString;
            bool isValid = false;
            do
            {
                inputString = Console.ReadLine();
                if (!string.IsNullOrEmpty(inputString))
                {
                    inputString = inputString.ToLower();
                    isValid = enteredData
                        .Where(x => x.ToLower() == inputString)
                        .FirstOrDefault() != null;
                    if (isValid) break;
                    Console.WriteLine("Invalid characters entered");
                }
            } while (true);
            if (inputString == null) throw new NullReferenceException(nameof(inputString));
            return inputString;
        }

        internal static string ReadPassword(string? helpMessage = null)
        {
            if (!string.IsNullOrEmpty(helpMessage)) Console.WriteLine(helpMessage);

            string pass = string.Empty;
            ConsoleKey key;

            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    Console.Write("\b \b");
                    pass = pass[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    pass += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);

            Console.WriteLine(string.Empty);

            return pass;
        }

        internal static void ExceptionMessage(Exception ex)
        {
#if DEBUG
            Console.WriteLine(ex);
#else
            Console.WriteLine(ex.Message);
#endif
        }

        internal static string Entered(string? helpMessage)
        {
            string? inputString;
            do
            {
                if (!string.IsNullOrEmpty(helpMessage)) Console.WriteLine(helpMessage);
                inputString = Console.ReadLine();
                if (!string.IsNullOrEmpty(inputString)) break;
                Console.WriteLine("The string cannot be empty");
            } while (true);
            if (inputString == null) throw new NullReferenceException(nameof(inputString));
            return inputString;
        }

        internal static void PrintYesNo(string text)
        {
            Console.WriteLine(string.Format("{0} | Y/n", text));
        }

        internal static void WaitAny()
        {
            Console.WriteLine("Press any key to continue.");
            Console.ReadLine();
        }
    }
}
