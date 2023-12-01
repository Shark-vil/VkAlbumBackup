using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;

namespace VkAlbumBackup
{
    internal class VkService
    {
        private VkApi _api = new VkApi();
        private string _accessToken = string.Empty;
        private const ulong _vkAppId = 7627789;
        private User? _ownerUser;
        private string _vkLogin = string.Empty;
        private string _vkPassword = string.Empty;
        private IReadOnlyList<PhotoAlbum>? _vkAlbums;
        private List<Photo>? _allowPhotosCache;
        private HttpClient _httpClient;
        private const string _tempAlbumName = "VkAlbumBackupClean";
        private object _locker = new object();

        private string _tokenFilePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "token.dat");
            }
        }

        public VkService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");
        }

        internal void SetLogin(string login) => _vkLogin = login;

        internal void SetPassword(string password) => _vkPassword = password;

        internal async Task<string?> ReadAccessTokenAsync(string pinCode)
        {
            if (IsValidAccessToken())
            {
                string ctyptToken = await File.ReadAllTextAsync(_tokenFilePath);
                return Defender.Decrypt(pinCode, ctyptToken);
            }
            return string.Empty;
        }

        internal bool IsValidAccessToken() => File.Exists(_tokenFilePath);

        internal async Task<bool> AuthorizeAsync(string? accessToken = null, bool isTwoFactorAuthorization = false)
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                try
                {
                    await _api.AuthorizeAsync(new ApiAuthParams
                    {
                        AccessToken = accessToken
                    });

                    _accessToken = accessToken;
                }
                catch (Exception ex)
                {
                    ConsoleService.ExceptionMessage(ex);
                }
            }
            else
            {
                if (isTwoFactorAuthorization)
                {
                    try
                    {
                        await _api.AuthorizeAsync(new ApiAuthParams
                        {
                            ApplicationId = _vkAppId,
                            Login = _vkLogin,
                            Password = _vkPassword,
                            Settings = Settings.Photos,
                            TwoFactorAuthorization = () =>
                            {
                                Console.WriteLine("Enter two-step verification code:");
                                return Console.ReadLine();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        ConsoleService.ExceptionMessage(ex);
                    }
                }
                else
                {
                    try
                    {
                        await _api.AuthorizeAsync(new ApiAuthParams
                        {
                            ApplicationId = _vkAppId,
                            Login = _vkLogin,
                            Password = _vkPassword,
                            Settings = Settings.Photos
                        });
                    }
                    catch (Exception ex)
                    {
                        ConsoleService.ExceptionMessage(ex);
                    }
                }
            }

            bool isValid = _api.IsAuthorized;
            if (isValid && (string.IsNullOrEmpty(_accessToken) || _accessToken != _api.Token))
            {
                string pinCode = ConsoleService.ReadPassword("Please create a PIN code. This is necessary for the safety of saving the token:");
                string? cryptToken = Defender.Encrypt(pinCode, _api.Token);
                if (string.IsNullOrEmpty(cryptToken))
                    Console.WriteLine("Error! Failed to write new token.");
                else
                    await File.WriteAllTextAsync(_tokenFilePath, cryptToken);
            }

            if (isValid)
            {
                IEnumerable<User> users = await _api.Users.GetAsync(new long[] { });
                _ownerUser = users.FirstOrDefault();
            }

            _accessToken = string.Empty;

            return _api.IsAuthorized;
        }

        internal async Task ReadAlbumsAsync()
        {
            try
            {
                _vkAlbums = await _api.Photo.GetAlbumsAsync(new PhotoGetAlbumsParams
                {
                    NeedSystem = true,
                });
            }
            catch (Exception ex)
            {
                ConsoleService.ExceptionMessage(ex);
            }
        }

        internal void PrintAlbums()
        {
            if (_vkAlbums == null || _vkAlbums.Count == 0)
            {
                Console.WriteLine("No albums available");
                return;
            }

            for (int i = 0; i < _vkAlbums.Count; i ++)
            {
                PhotoAlbum album = _vkAlbums[i];
                Console.WriteLine(string.Format("{0} - {1}", i, album.Title));
            }
        }

        internal bool IsExistsAlbum(int albumId)
        {
            return _vkAlbums != null && albumId >= 0 && albumId < _vkAlbums.Count;
        }

        internal async Task DownloadAlbumAsync(int albumId)
        {
            await CacheAlbumPhotosAsync(albumId);
            if (_allowPhotosCache == null || _allowPhotosCache.Count == 0) return;

            string targetDirectory;

            do
            {
                targetDirectory = ConsoleService.Entered("Specify the path to the folder where you want to download the files:");
                if (Directory.Exists(targetDirectory)) break;
                Console.WriteLine("Failed to get folder");
            } while (true);

            int maxFiles = _allowPhotosCache.Count;
            int successDownloaded = 0;
            DateTime printProgressDelay = DateTime.Now.AddSeconds(2);

            await Parallel.ForEachAsync(_allowPhotosCache, async (Photo photo, CancellationToken cancellationToken) =>
            {
                string? photoUrl = GetPhotoUrl(photo);
                if (string.IsNullOrEmpty(photoUrl)) return;

                string filePath = Path.Combine(targetDirectory, string.Format("{0}.jpg", photo.Id));

                do
                {
                    try
                    {
                        using (HttpResponseMessage response = await _httpClient.GetAsync(photoUrl))
                        using (FileStream fs = new FileStream(filePath, FileMode.CreateNew))
                            await response.Content.CopyToAsync(fs);

                        lock (_locker)
                        {
                            successDownloaded++;

                            if (printProgressDelay < DateTime.Now)
                            {
                                Console.WriteLine(string.Format("Completed: {0} / {1}", successDownloaded, maxFiles));
                                printProgressDelay = DateTime.Now.AddSeconds(2);
                            }
                        }

                        break;
                    }
                    catch (Exception ex)
                    {
                        ConsoleService.ExceptionMessage(ex);
                    }
                } while (true);
            });

            Console.WriteLine("All files have been downloaded!");
        }

        internal async Task CleanAlbumAsync(int albumId)
        {
            if (_vkAlbums == null)
                throw new NullReferenceException(nameof(_vkAlbums));

            PhotoAlbum getAlbum = _vkAlbums[albumId];
            if (getAlbum.Title != _tempAlbumName)
            {
                await CacheAlbumPhotosAsync(albumId);
                if (_allowPhotosCache == null) return;
            }

            if (_ownerUser == null)
                throw new NullReferenceException(nameof(_ownerUser));

            PhotoAlbum? tempAlbum;

            tempAlbum = _vkAlbums
                .Where(x => x.Title == _tempAlbumName)
                .FirstOrDefault();

            if (tempAlbum == null)
            {
                try
                {
                    tempAlbum = await _api.Photo.CreateAlbumAsync(new PhotoCreateAlbumParams
                    {
                        Title = _tempAlbumName,
                        CommentsDisabled = true,
                        Description = "VkAlbumBackup",
                        PrivacyComment = new List<Privacy>
                        {
                            Privacy.OnlyMe
                        },
                        PrivacyView = new List<Privacy>
                        {
                            Privacy.OnlyMe
                        }
                    });
                }
                catch (Exception ex)
                {
                    ConsoleService.ExceptionMessage(ex);
                    return;
                }
            }

            if (getAlbum.Title != _tempAlbumName && _allowPhotosCache != null && _allowPhotosCache.Count != 0)
            {
                int vurrentMove = 0;
                int moveLimit = 3;
                int maxFiles = _allowPhotosCache.Count;
                int succesMoved = 0;
                DateTime printProgressDelay = DateTime.Now.AddSeconds(2);

                await Parallel.ForEachAsync(_allowPhotosCache, async (Photo photo, CancellationToken cancellationToken) =>
                {
                    if (photo.Id == null)
                        throw new NullReferenceException(nameof(photo.Id));

                    bool isDone = false;
                    do
                    {
                        bool isLimited = false;
                        lock (_locker) isLimited = vurrentMove >= moveLimit;

                        if (isLimited)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));
                            continue;
                        }

                        try
                        {
                            isDone = await _api.Photo.MoveAsync(tempAlbum.Id, (ulong)photo.Id, _ownerUser.Id);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            vurrentMove = moveLimit;
                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }

                        if (isDone)
                        {
                            lock (_locker)
                            {
                                vurrentMove++;
                                succesMoved++;

                                if (printProgressDelay < DateTime.Now)
                                {
                                    Console.WriteLine(string.Format("Completed move: {0} / {1}", succesMoved, maxFiles));
                                    printProgressDelay = DateTime.Now.AddSeconds(2);
                                }
                            }

                            if (vurrentMove >= moveLimit)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1.5f));
                                lock (_locker) vurrentMove = 0;
                            }
                        }
                    } while (!isDone);
                });
            }

            bool isDelete = false;
            do
            {
                try
                {
                    isDelete = await _api.Photo.DeleteAlbumAsync(tempAlbum.Id);
                }
                catch (Exception ex)
                {
                    ConsoleService.ExceptionMessage(ex);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            } while (!isDelete);

            Console.WriteLine("All files have been clean!");
        }

        internal async Task CacheAlbumPhotosAsync(int albumId)
        {
            if (!IsExistsAlbum(albumId))
                throw new Exception("Invalid album ID");

            if (_vkAlbums == null)
                throw new NullReferenceException(nameof(_vkAlbums));

            if (_ownerUser == null)
                throw new NullReferenceException(nameof(_ownerUser));

            PhotoAlbum album = _vkAlbums[albumId];
            List<Photo> allowPhotos = new List<Photo>();

            IEnumerable<Photo>? photos = null;
            ulong maxCount = 1000;
            ulong parseOffset = 0;
            int photosCount;
            do
            {
                photosCount = 0;

                if (parseOffset != 0)
                    await Task.Delay(TimeSpan.FromSeconds(.5f));

                PhotoAlbumType type;

                switch (album.Id)
                {
                    case -15:
                        type = PhotoAlbumType.Saved;
                        break;

                    default:
                        type = PhotoAlbumType.Id(album.Id);
                        break;
                }

                try
                {
                    photos = await _api.Photo.GetAsync(new PhotoGetParams
                    {
                        OwnerId = _ownerUser.Id,
                        AlbumId = type,
                        Count = maxCount,
                        Offset = parseOffset
                    });

                    photosCount = photos.Count();

                    //Console.WriteLine(string.Format("{0} photos received", photosCount));
                    //Console.WriteLine("The search continues...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    continue;
                }

                var newPhotos = new List<Photo>();

                if (photosCount != 0)
                    foreach (Photo photo in photos)
                        if (allowPhotos.Where(x => x.Id == photo.Id).FirstOrDefault() == null)
                        {
                            newPhotos.Add(photo);
                            //Console.WriteLine(string.Format("Cached photo - {0}", photo.Id));
                        }

                if (newPhotos.Count == 0)
                    break;

                allowPhotos.AddRange(newPhotos);

                Console.WriteLine(string.Format("Registered {0} / {1}", allowPhotos.Count, album.Size));

                parseOffset += maxCount;
            } while (photos == null || photosCount != 0);

            Console.WriteLine("Search completed.");

            _allowPhotosCache = allowPhotos;
        }

        internal string? GetPhotoUrl(Photo photo)
        {
            if (photo.BigPhotoSrc != null) return photo.BigPhotoSrc.AbsoluteUri;
            if (photo.Sizes != null && photo.Sizes.Count > 0)
            {
                PhotoSize photoSize = photo.Sizes[photo.Sizes.Count - 1];
                return photoSize.Url.ToString();
            }
            if (photo.Photo2560 != null) return photo.Photo2560.AbsoluteUri;
            if (photo.Photo1280 != null) return photo.Photo1280.AbsoluteUri;
            if (photo.Photo807 != null) return photo.Photo807.AbsoluteUri;
            if (photo.Photo604 != null) return photo.Photo604.AbsoluteUri;
            if (photo.Photo200 != null) return photo.Photo200.AbsoluteUri;
            if (photo.Photo130 != null) return photo.Photo130.AbsoluteUri;
            if (photo.Photo100 != null) return photo.Photo100.AbsoluteUri;
            if (photo.Photo75 != null) return photo.Photo75.AbsoluteUri;
            if (photo.Photo50 != null) return photo.Photo50.AbsoluteUri;
            if (photo.PhotoSrc != null) return photo.PhotoSrc.AbsoluteUri;
            return null;
        }
    }
}
