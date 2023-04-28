using System;
using System.Collections.Generic;
using System.Linq;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Models;
using CMS;
using CMS.Core;
using Microsoft.Azure.Storage.Blob;

[assembly: RegisterImplementation(typeof(ICloudDirectoryService), typeof(CloudDirectoryService))]

namespace AzureStorageProvider.Azure
{
    public class CloudDirectoryService : ICloudDirectoryService
    {
        private ICloudBlobClient _cloudBlobClient = Service.Resolve<ICloudBlobClient>();
        protected CloudBlobDirectory GetDirectoryReference(string path)
        {
            return _cloudBlobClient.GetContainerReference(AccountInfo.Instance.RootContainer).GetDirectoryReference(path);
        }
        public virtual List<Blob> GetBlobs(string path)
        {
            return GetDirectoryReference(path)
                .ListBlobs(true, BlobListingDetails.Metadata)
                .Where(b => b is CloudBlockBlob)
                .Cast<CloudBlockBlob>()
                .Where(b => !b.Name.EndsWith("$cmsfolder$", StringComparison.OrdinalIgnoreCase))
                .Select(cloudBlockBlob => new Blob().Initialize(cloudBlockBlob))
                .ToList();
        }
    }
}
