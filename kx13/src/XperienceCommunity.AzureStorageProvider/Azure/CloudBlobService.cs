using System.Collections.Generic;
using System.IO;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Collections;
using AzureStorageProvider.Helpers;
using AzureStorageProvider.Models;
using CMS;
using CMS.Helpers;
using Microsoft.Azure.Storage.Blob;

[assembly: RegisterImplementation(typeof(ICloudBlobService), typeof(CloudBlobService))]

namespace AzureStorageProvider.Azure
{
    public interface ICloudBlobService
    {
        bool Exists(string path);
        BlobAttributes FetchAttributes(string path);
        void SetMetadataAsync(string path, IDictionary<string, string> metadata);
        void Delete(string path);
        void Copy(string path, string targetPath);
        byte[] Download(string path);
        BlobAttributes Upload(string path, IDictionary<string, string> metadata, Stream stream);
    }

    public class CloudBlobService : ICloudBlobService
    {
        private readonly ICloudBlobClient cloudBlobClient;

        public CloudBlobService(ICloudBlobClient cloudBlobClient) => this.cloudBlobClient = cloudBlobClient;

        protected CloudBlockBlob GetBlobReference(string path)
        {
            BlobContainerCollection.Instance.GetOrCreate(AccountInfo.Instance.RootContainer);
            return cloudBlobClient.GetContainerReference(AccountInfo.Instance.RootContainer).GetBlockBlobReference(path);
        }
        protected void UpdateMetadata(CloudBlockBlob blobReference, IDictionary<string, string> metadata)
        {
            foreach (var row in metadata)
            {
                if (blobReference.Metadata.ContainsKey(row.Key))
                {
                    blobReference.Metadata[row.Key] = row.Value;
                }
                else
                {
                    blobReference.Metadata.Add(row);
                }
            }
        }


        public virtual bool Exists(string path) => GetBlobReference(path).Exists();

        public virtual BlobAttributes FetchAttributes(string path)
        {
            var blobReference = GetBlobReference(path);
            blobReference.FetchAttributes();

            var attributes = BlobAttributesHelper.MapAttributes(blobReference);

            return attributes;
        }

        public virtual void SetMetadataAsync(string path, IDictionary<string, string> metadata)
        {
            var blobReference = GetBlobReference(path);
            UpdateMetadata(blobReference, metadata);

            blobReference.SetMetadataAsync();
        }

        public virtual void Delete(string path)
        {
            var blobReference = GetBlobReference(path);

            blobReference.Delete();
        }

        public virtual void Copy(string path, string targetPath)
        {
            var blobReference = GetBlobReference(path);
            var targetBlobReference = GetBlobReference(targetPath);

            targetBlobReference.StartCopy(blobReference.Uri);
        }

        public virtual byte[] Download(string path)
        {
            var blobReference = GetBlobReference(path);
            byte[] data;

            using (var stream = new MemoryStream())
            {
                blobReference.DownloadToStream(stream);
                data = stream.ToArray();
            }

            return data;
        }

        public virtual BlobAttributes Upload(string path, IDictionary<string, string> metadata, Stream stream)
        {
            var blobReference = GetBlobReference(path);
            UpdateMetadata(blobReference, metadata);

            string mimeType = MimeTypeHelper.GetMimetype(path);
            blobReference.Properties.ContentType = mimeType;

            blobReference.UploadFromStream(stream);
            return BlobAttributesHelper.MapAttributes(blobReference);
        }
    }
}
