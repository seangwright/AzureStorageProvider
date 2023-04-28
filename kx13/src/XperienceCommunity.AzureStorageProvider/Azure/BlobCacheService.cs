using System;
using System.Linq;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Helpers;
using AzureStorageProvider.Models;
using CMS;
using CMS.Helpers;

[assembly: RegisterImplementation(typeof(IBlobCacheService), typeof(BlobCacheService))]

namespace AzureStorageProvider.Azure
{
    public interface IBlobCacheService
    {
        BlobCacheType CacheType { get; }
        void Add(string path, System.IO.Stream stream, DateTime created);
        byte[] Get(string path, DateTime remoteLastModified);
        void Discard(string path);
    }

    public class BlobCacheService : IBlobCacheService
    {
        private static readonly string[] excludedPaths = { "App_Data" };
        private readonly ICloudBlobService cloudBlobService;
        protected BlobCacheType cacheType = AccountInfo.Instance.BlobCacheType;

        public BlobCacheType CacheType => cacheType;

        public BlobCacheService(ICloudBlobService cloudBlobService) => this.cloudBlobService = cloudBlobService;

        public byte[] Get(string path, DateTime remoteLastModified)
        {
            if (cacheType == BlobCacheType.None || excludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return GetFromRemote(path);
            }

            byte[] data = null;

            switch (CacheType)
            {
                case BlobCacheType.Memory:
                    data = CacheHelper.Cache(cs => GetFromRemote(cs, path), new CacheSettings(AccountInfo.Instance.BlobCacheMinutes, BlobCacheHelper.GetCacheKey(path)));
                    break;

                case BlobCacheType.FileSystem:
                    data = TryGetFromFileSystemCache(path, remoteLastModified);

                    if (data == null)
                    {
                        data = GetFromRemote(path);
                        AddToCache(path, data, DateTime.UtcNow);
                    }
                    break;
                case BlobCacheType.None:
                    break;
                default:
                    break;
            }

            return data;
        }

        private byte[] GetFromRemote(CacheSettings cs, string path)
        {
            byte[] data = GetFromRemote(path);

            if (cs.Cached)
            {
                cs.CacheDependency = CacheHelper.GetCacheDependency(BlobCacheHelper.GetCacheDependency(path));
            }

            return data;
        }

        private byte[] GetFromRemote(string path) => cloudBlobService.Download(path);

        public void Discard(string path)
        {
            switch (cacheType)
            {
                case BlobCacheType.Memory:
                    CacheHelper.TouchKey(BlobCacheHelper.GetCacheDependency(path));
                    break;

                case BlobCacheType.FileSystem:
                    string tempFilePath = AzurePathHelper.GetTempBlobPath(path);
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }

                    break;
                case BlobCacheType.None:
                    break;
                default:
                    break;
            }
        }

        public void Add(string path, System.IO.Stream stream, DateTime created)
        {
            if (cacheType == BlobCacheType.None)
            {
                return;
            }

            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);

            AddToCache(path, data, created);
        }

        private byte[] TryGetFromFileSystemCache(string path, DateTime remoteLastModified)
        {
            byte[] data = null;
            var fileLastModified = DateTime.MinValue;

            string tempFilePath = AzurePathHelper.GetTempBlobPath(path);

            if (System.IO.File.Exists(tempFilePath))
            {
                fileLastModified = System.IO.File.GetLastWriteTimeUtc(tempFilePath);
                using (var stream = new System.IO.FileStream(tempFilePath, System.IO.FileMode.Open))
                {
                    data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                }
            }

            // no data in cache or outdated
            if (data == null || fileLastModified < remoteLastModified)
            {
                if (data != null)
                {
                    Discard(path);
                }

                return null;
            }

            return data;
        }
        private void AddToCache(string path, byte[] data, DateTime created)
        {
            switch (cacheType)
            {
                case BlobCacheType.Memory:
                    CacheHelper.Add(BlobCacheHelper.GetCacheKey(path), data, CacheHelper.GetCacheDependency(
                        BlobCacheHelper.GetCacheDependency(path)),
                        DateTime.Now.AddMinutes(AccountInfo.Instance.BlobCacheMinutes),
                        TimeSpan.Zero);
                    break;

                case BlobCacheType.FileSystem:
                    AddToFileSystem(path, data, created);
                    break;
                case BlobCacheType.None:
                    break;
                default:
                    break;
            }
        }

        private void AddToFileSystem(string path, byte[] data, DateTime created)
        {
            string tempFilePath = AzurePathHelper.GetTempBlobPath(path);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(tempFilePath));

            using (var stream = new System.IO.FileStream(tempFilePath, System.IO.FileMode.Create))
            {
                stream.Write(data, 0, data.Length);
            }

            // make sure the last write time matches cloud timestamp
            System.IO.File.SetLastWriteTimeUtc(tempFilePath, created);
        }
    }
}
