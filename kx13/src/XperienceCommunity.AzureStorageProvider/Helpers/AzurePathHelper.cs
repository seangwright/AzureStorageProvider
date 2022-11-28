using System;
using System.Web;
using AzureStorageProvider.Models;
using CMS.Base;
using CMS.Core;
using CMS.Helpers;

namespace AzureStorageProvider.Helpers
{
    public class AzurePathHelper
    {
        private static bool? _forceLowercase = null;

        public const string BasePath = "/cmsstorage";
        public static string TempPath
        {
            get
            {
                var settings = Service.Resolve<IAppSettingsService>();

                return ValidationHelper.GetString(settings[nameof(WebConfigKeys.CMSAzureCachePath)], "AzureTemp");
            }
        }
        public const string DownloadPage = "~/CMSPages/GetAzureProviderFile.aspx";


        private static string currentDirectory = null;

        public static string CurrentDirectory
        {
            get
            {
                if (currentDirectory == null)
                {
                    currentDirectory = GetValidPath(SystemContext.WebApplicationVirtualPath);
                }

                return currentDirectory;
            }
            set => currentDirectory = value;
        }

        public static bool ForceLowercase
        {
            get
            {
                if (_forceLowercase is null)
                {
                    var settings = Service.Resolve<IAppSettingsService>();

                    _forceLowercase = ValidationHelper.GetBoolean(settings[nameof(WebConfigKeys.AzureStorageProviderForceLowercase)], false);
                }

                return _forceLowercase ?? false;
            }
            set => _forceLowercase = value;
        }

        public static string GetBlobPath(string path)
        {
            // get path to the website
            string fileSystemPath = GetValidPath(CurrentDirectory);

            path = GetValidPath(path);

            if (!path.StartsWith(fileSystemPath, StringComparison.OrdinalIgnoreCase))
            {
                // path is relative
                if (fileSystemPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    // path is parent to our app
                    return string.Empty;
                }
                else if (path.StartsWith("~"))
                {
                    // path is relative with ~
                    path = path.TrimStart('~');
                    path = path.TrimStart('/');
                }
                else
                {
                    // path is relative with/without slash
                    path = path.TrimStart('/');
                }
            }
            else
            {
                // remove root container
                path = path.Substring(fileSystemPath.Length);
            }

            path = GetValidPathForwardSlashes(path);

            path = path.TrimStart('/');

            if (ForceLowercase)
            {
                path = path.ToLowerInvariant();
            }

            return path;
        }

        public static string GetBlobDirectory(string path) => GetValidPathForwardSlashes(CMS.IO.Path.GetDirectoryName(path));

        public static string GetTempBlobPath(string blobPath)
        {
            if (!string.IsNullOrEmpty(CurrentDirectory))
            {
                return GetValidPath($"{CurrentDirectory}\\{TempPath}\\{GetValidPath(blobPath)}");
            }

            return null;
        }
        public static string GetFileSystemPath(string path)
        {
            // get path to the website
            string fileSystemPath = CurrentDirectory;

            if (ForceLowercase)
            {
                fileSystemPath = fileSystemPath.ToLowerInvariant();
            }

            // add root container
            path = fileSystemPath.TrimEnd('/') + "/" + path;

            path = GetValidPath(path);

            return path;
        }

        public static string GetBlobPath(Uri uri)
        {
            // /cmsstorage/dir/blob.txt -> dir/blob.txt

            string path = HttpUtility.UrlDecode(uri.AbsolutePath);
            string container = $"/{AccountInfo.Instance.RootContainer}/";

            // remove root container
            if (!path.StartsWith(container, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Path does not start with file system prefix.");
            }

            path = path.Substring(container.Length);

            return path;
        }

        public static string GetValidPath(string path)
        {
            path = EnsureBackslashes(path);
            path = path.TrimEnd('\\');
            return path;
        }

        public static string GetValidPathForwardSlashes(string path)
        {
            path = CMS.IO.Path.EnsureSlashes(path);
            path = path.TrimEnd('/');
            return path;
        }

        public static string GetDownloadUri(string path)
        {
            if (AccountInfo.Instance.PublicContainer)
            {
                string server = $"https://{AccountInfo.Instance.AccountName}.blob.core.windows.net";
                if (!string.IsNullOrEmpty(AccountInfo.Instance.EndPoint))
                {
                    server = AccountInfo.Instance.EndPoint;
                }

                return $"{server.TrimEnd('/')}/{AccountInfo.Instance.RootContainer}/{path}";
            }

            string url = $"{DownloadPage}?path=~/{URLHelper.EscapeSpecialCharacters(path)}";
            string hash = ValidationHelper.GetHashString(URLHelper.GetQuery(url), new HashSettings(string.Empty));
            url = URLHelper.AddParameterToUrl(url, "hash", hash);
            return URLHelper.ResolveUrl(url);
        }

        public static string EnsureBackslashes(string path, bool trimEnd = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            path = path.Replace('/', '\\');

            if (trimEnd)
            {
                path = path.TrimEnd('\\');
            }

            return path;
        }

    }
}
