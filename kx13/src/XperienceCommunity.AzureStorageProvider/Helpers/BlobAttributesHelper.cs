using AzureStorageProvider.Models;
using Microsoft.Azure.Storage.Blob;

namespace AzureStorageProvider.Helpers
{
    internal static class BlobAttributesHelper
    {
        public static BlobAttributes MapAttributes(CloudBlob blobReference)
        {
            var attributes = new BlobAttributes
            {
                AbsoluteUri = blobReference.Uri.AbsoluteUri,
                Etag = blobReference.Properties.ETag,
                LastModified = blobReference.Properties.LastModified.Value.DateTime,
                Length = blobReference.Properties.Length,
                Metadata = blobReference.Metadata
            };

            return attributes;
        }
    }
}
