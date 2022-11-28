using AzureStorageProvider.Azure;

namespace AzureStorageProvider.Helpers
{
    internal class BlobCacheHelper
    {
        internal static string GetCacheKey(string path) => nameof(BlobCacheService) + "|" + path;

        internal static string GetCacheDependency(string path) => nameof(BlobCacheService) + "dependency|" + path;
    }
}
