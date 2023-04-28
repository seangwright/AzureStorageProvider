using AzureStorageProvider.Azure;
using CMS;
using Microsoft.Azure.Storage.Blob;

[assembly: RegisterImplementation(typeof(ICloudBlobContainerService), typeof(CloudBlobContainerService))]

namespace AzureStorageProvider.Azure
{
    public interface ICloudBlobContainerService
    {
        void Create(string path, BlobContainerPublicAccessType? accessType = null);
        bool Exists(string path);
        BlobContainerPublicAccessType? GetPublicAccess(string path);
        void DeleteAsync(string path);
    }

    public class CloudBlobContainerService : ICloudBlobContainerService
    {
        private readonly ICloudBlobClient cloudBlobClient;

        public CloudBlobContainerService(ICloudBlobClient cloudBlobClient) => this.cloudBlobClient = cloudBlobClient;

        public void Create(string path, BlobContainerPublicAccessType? accessType = null)
        {
            if (accessType.HasValue)
            {
                cloudBlobClient.GetContainerReference(path).Create(accessType.Value);
            }
            else
            {
                cloudBlobClient.GetContainerReference(path).Create();
            }
        }

        public void DeleteAsync(string path) => cloudBlobClient.GetContainerReference(path).DeleteAsync();

        public bool Exists(string path) => cloudBlobClient.GetContainerReference(path).Exists();

        public BlobContainerPublicAccessType? GetPublicAccess(string path) => cloudBlobClient.GetContainerReference(path).Properties.PublicAccess;
    }
}
