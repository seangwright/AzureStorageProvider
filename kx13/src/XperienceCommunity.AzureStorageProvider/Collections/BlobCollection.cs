using System;
using System.Collections.Generic;
using System.Linq;
using AzureStorageProvider.Helpers;
using AzureStorageProvider.Models;

namespace AzureStorageProvider.Collections
{
    public class BlobCollection : Collection<Blob, BlobCollection>
    {
        public List<string> GetOutdatedBlobPaths(DateTime dateThreshold) => items.Where(b => b.Value.LastRefresh.HasValue && b.Value.LastRefresh.Value < dateThreshold).Select(b => b.Key).ToList();
    }
}
