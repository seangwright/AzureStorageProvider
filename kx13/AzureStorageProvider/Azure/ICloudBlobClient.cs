using Microsoft.Azure.Storage.Blob;

namespace AzureStorageProvider.Azure
{
    internal interface ICloudBlobClient
    {
        CloudBlobContainer GetContainerReference(string containerName);
    }
}
