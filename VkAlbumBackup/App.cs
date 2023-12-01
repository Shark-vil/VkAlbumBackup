using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VkNet.Enums.Filters;
using VkNet.Model;
using VkNet;

namespace VkAlbumBackup
{
    internal class App
    {
        private VkService? _vkService;

        internal async Task RunAsync()
        {
            string input;
            string? accessToken = null;

            _vkService = new VkService();

            if (_vkService.IsValidAccessToken())
            {
                input = ConsoleService.ReadPassword("Enter the PIN code you created earlier:");
                accessToken = await _vkService.ReadAccessTokenAsync(input);

                if (accessToken == null || !accessToken.StartsWith("vk1.a."))
                {
                    Console.WriteLine("Invalid PIN code entered! You can restart the application and try again.");
                    accessToken = null;
                }
            }

            if (string.IsNullOrEmpty(accessToken) || !await _vkService.AuthorizeAsync(accessToken))
            {
                do
                {
                    ConsoleService.PrintYesNo("Do you use two-step verification?");
                    input = ConsoleService.EnteredKeys("y", "n");
                    bool isTwoFactorAuthorization = input == "y";

                    _vkService.SetLogin(ConsoleService.Entered("Please enter your login:"));
                    _vkService.SetPassword(ConsoleService.ReadPassword("Please enter your password:"));

                    if (await _vkService.AuthorizeAsync(null, isTwoFactorAuthorization))
                        break;

                    Console.WriteLine("Authorization error, please try again.");
                } while (true);
            }

            Console.Clear();

            while (true)
            {
                await _vkService.ReadAlbumsAsync();
                _vkService.PrintAlbums();

                input = ConsoleService.Entered("Enter album number:");

                int albumId;
                if (!int.TryParse(input, out albumId) || !_vkService.IsExistsAlbum(albumId))
                {
                    ConsoleService.WaitAny();
                    continue;
                }

                Console.WriteLine("What do you want to do - download or clean album? | D/c");

                input = ConsoleService.EnteredKeys("d", "c");
                bool canDownload = input == "d";

                ConsoleService.PrintYesNo(string.Format("Are you sure you want to {0} the album?", canDownload ? "download" : "clean"));
                input = ConsoleService.EnteredKeys("y", "n");
                
                if (input == "n")
                {
                    Console.WriteLine("Action cancelled.");
                    ConsoleService.WaitAny();
                    continue;
                }

                if (canDownload)
                {
                    await _vkService.DownloadAlbumAsync(albumId);
                }
                else
                {
                    await _vkService.CleanAlbumAsync(albumId);
                }
            }
        }
    }
}
