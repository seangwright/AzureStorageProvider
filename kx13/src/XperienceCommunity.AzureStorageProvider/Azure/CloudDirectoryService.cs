using System;
using System.Collections.Generic;
using System.Linq;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Models;
using CMS;
using Microsoft.Azure.Storage.Blob;

[assembly: RegisterImplementation(typeof(ICloudDirectoryService), typeof(CloudDirectoryService))]

namespace AzureStorageProvider.Azure
{
    public interface ICloudDirectoryService
    {
        List<Blob> GetBlobs(string path);
    }

    public class CloudDirectoryService : ICloudDirectoryService
    {
        private readonly ICloudBlobClient cloudBlobClient;

        public CloudDirectoryService(ICloudBlobClient cloudBlobClient) => this.cloudBlobClient = cloudBlobClient;

        public virtual List<Blob> GetBlobs(string path) => cloudBlobClient.GetContainerReference(AccountInfo.Instance.RootContainer)
                .GetDirectoryReference(path)
                .ListBlobs(true, BlobListingDetails.Metadata)
                .Where(b => b is CloudBlockBlob)
                .Cast<CloudBlockBlob>()
                .Where(b => !b.Name.EndsWith("$cmsfolder$", StringComparison.OrdinalIgnoreCase))
                .Select(cloudBlockBlob => new Blob().Initialize(cloudBlockBlob))
                .ToList();
    }
}
