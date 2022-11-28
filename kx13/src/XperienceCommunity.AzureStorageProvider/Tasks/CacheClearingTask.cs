using System;
using System.Linq;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Collections;
using AzureStorageProvider.Helpers;
using AzureStorageProvider.Models;
using CMS.Core;
using CMS.Scheduler;

namespace AzureStorageProvider.Tasks
{
    public class CacheClearingTask : ITask
    {
        private readonly IBlobCacheService blobCacheService;

        public CacheClearingTask() : this(Service.Resolve<IBlobCacheService>())
        {

        }

        public CacheClearingTask(IBlobCacheService blobCacheService) => this.blobCacheService = blobCacheService;

        public string Execute(TaskInfo task)
        {
            if (blobCacheService.CacheType != BlobCacheType.FileSystem)
            {
                return "Caching of Azure data is disabled or is set to memory. No need to run this task.";
            }

            int minutes = AccountInfo.Instance.BlobCacheMinutes;
            var dateThreshold = DateTime.UtcNow.AddMinutes(-minutes);

            var blobsToDelete = BlobCollection.Instance.GetOutdatedBlobPaths(dateThreshold);

            int blobsDeleted = 0;
            int directoriesUninitialized = 0;

            foreach (string path in blobsToDelete)
            {
                // remove the blob
                blobCacheService.Discard(path);
                blobsDeleted++;
            }

            // clear empty folders in file system
            if (blobsDeleted > 0)
            {
                var folders = System.IO.Directory.GetDirectories(AzurePathHelper.GetTempBlobPath(string.Empty), "*", System.IO.SearchOption.AllDirectories).OrderByDescending(p => p.Length);
                foreach (string subFolder in folders)
                {
                    if (System.IO.Directory.Exists(subFolder) &&
                        !System.IO.Directory.GetFiles(subFolder).Any() &&
                        !System.IO.Directory.EnumerateDirectories(subFolder).Any())
                    {
                        System.IO.Directory.Delete(subFolder, false);
                    }
                }
            }

            return "OK, discarded metadata of " + blobsDeleted + " blobs, " + directoriesUninitialized + " dirs";
        }
    }
}
