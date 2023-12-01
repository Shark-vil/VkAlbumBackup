using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VkAlbumBackup
{
    internal static class Defender
    {
        /*
        private static IEnumerable<string> _Split(string str, int chunkSize)
        {
            return str.Chunk(chunkSize)
                .Select(s => new string(s))
                .ToList();
        }
        */

        internal static string? Encrypt(string key, string plainText)
        {
            /*
            string base64plainText = _Base64Encode(plainText);
            IEnumerable<string> parts = _Split(base64plainText, 42);
            List<string> keys = new List<string>();
            foreach (string part in parts)
            {
                string? cryptString = _EncryptString(key, part);
                if (!string.IsNullOrEmpty(cryptString)) keys.Add(cryptString);
            }

            return string.Join(";", keys);
            */

            return _EncryptString(key, plainText);
        }

        internal static string? Decrypt(string key, string cipherText)
        {
            /*
            string[] cipherParts = cipherText.Split(';');
            List<string> parts = new List<string>();
            foreach (string cipherPart in cipherParts)
            {
                string? decryptString = _DecryptString(key, cipherPart);
                if (!string.IsNullOrEmpty(decryptString))
                {
                    string base64cipherText = _Base64Decode(decryptString);
                    parts.Add(base64cipherText);
                }
            }

            return string.Join(string.Empty, parts);
            */

            return _DecryptString(key, cipherText);
        }

        /*
        private static string _Base64Encode(string plainText)
        {
            byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private static string _Base64Decode(string base64EncodedData)
        {
            byte[] base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        */

        private static string? _EncryptString(string key, string plainText)
        {
            try
            {
                byte[] data = Encoding.Default.GetBytes(plainText);
                byte[] pwd = !string.IsNullOrEmpty(key) ? Encoding.Default.GetBytes(key) : Array.Empty<byte>();
#pragma warning disable CA1416 // Проверка совместимости платформы
                byte[] cipher = ProtectedData.Protect(data, pwd, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416 // Проверка совместимости платформы
                return Convert.ToBase64String(cipher);
            }
            catch (Exception ex)
            {
                ConsoleService.ExceptionMessage(ex);
            }

            return null;
        }

        private static string? _DecryptString(string key, string cipherText)
        {
            try
            {
                byte[] cipher = Convert.FromBase64String(cipherText);
                byte[] pwd = !string.IsNullOrEmpty(key) ? Encoding.Default.GetBytes(key) : Array.Empty<byte>();
#pragma warning disable CA1416 // Проверка совместимости платформы
                byte[] data = ProtectedData.Unprotect(cipher, pwd, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416 // Проверка совместимости платформы
                return Encoding.Default.GetString(data);
            }
            catch (Exception ex)
            {
                ConsoleService.ExceptionMessage(ex);
            }

            return null;
        }
    }
}
