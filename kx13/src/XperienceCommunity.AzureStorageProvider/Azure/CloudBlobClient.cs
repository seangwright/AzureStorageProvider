using AzureStorageProvider.Azure;
using AzureStorageProvider.Models;
using CMS;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using AzureCloudBlobClient = Microsoft.Azure.Storage.Blob.CloudBlobClient;

[assembly: RegisterImplementation(typeof(ICloudBlobClient), typeof(AzureStorageProvider.Azure.CloudBlobClient))]

namespace AzureStorageProvider.Azure
{
    public interface ICloudBlobClient
    {
        CloudBlobContainer GetContainerReference(string containerName);
    }

    public class CloudBlobClient : ICloudBlobClient
    {
        private readonly AzureCloudBlobClient blobClient = InitializeClient();

        private static AzureCloudBlobClient InitializeClient()
        {
            var accountInfo = AccountInfo.Instance;
            var credentials = new StorageCredentials(accountInfo.AccountName, accountInfo.SharedKey);
            var account = new CloudStorageAccount(credentials, true);
            return account.CreateCloudBlobClient();
        }

        public CloudBlobContainer GetContainerReference(string containerName) => blobClient.GetContainerReference(containerName);
    }
}
