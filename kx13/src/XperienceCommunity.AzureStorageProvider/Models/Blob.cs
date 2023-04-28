using System;
using System.Collections.Generic;
using System.IO;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Collections;
using AzureStorageProvider.Helpers;
using CMS.Core;
using Microsoft.Azure.Storage.Blob;

namespace AzureStorageProvider.Models
{
    public class Blob : IObjectWithPath<Blob>
    {
        private readonly object objLock = new object();
        private bool? exists = null;
        private bool attributesFetched = false;
        private BlobAttributes attributes;

        private readonly ICloudBlobService _cloudBlobService = Service.Resolve<ICloudBlobService>();
        private readonly IBlobCacheService _blobCacheService = Service.Resolve<IBlobCacheService>();

        internal DateTime? LastRefresh { get; private set; } = null;
        public string Path { get; protected set; }

        public T GetAttribute<T>(Func<BlobAttributes, T> attribute) => GetAttribute(attribute, default);
        public T GetAttribute<T>(Func<BlobAttributes, T> attribute, T defaultValue)
        {
            if (!Exists())
            {
                return defaultValue;
            }

            lock (objLock)
            {
                return GetAttributeInternal(attribute);
            }
        }

        private T GetAttributeInternal<T>(Func<BlobAttributes, T> attribute)
        {
            if (!attributesFetched)
            {
                attributes = _cloudBlobService.FetchAttributes(Path);
                attributesFetched = true;
                LastRefresh = DateTime.UtcNow;
            }

            return attribute(attributes);
        }

        public string GetMetadataAttribute(BlobMetadataEnum attribute)
        {
            var metadata = GetAttribute(a => a.Metadata);

            metadata.TryGetValue(attribute.ToString(), out string value);

            return value;
        }

        public Blob Initialize(string path)
        {
            Path = path;
            return this;
        }

        public Blob Initialize(CloudBlockBlob blobItem)
        {
            Path = AzurePathHelper.ForceLowercase ? blobItem.Name.ToLowerInvariant() : blobItem.Name;

            lock (objLock)
            {
                exists = true;
                attributes = BlobAttributesHelper.MapAttributes(blobItem);
                attributesFetched = true;
                LastRefresh = DateTime.UtcNow;
            }

            return this;
        }

        public void Uninitialize()
        {
            lock (objLock)
            {
                exists = null;
                LastRefresh = null;
                attributes = null;
                attributesFetched = false;

                _blobCacheService.Discard(Path);
            }
        }

        public void Reinitialize()
        {
            Uninitialize();

            lock (objLock)
            {
                exists = _cloudBlobService.Exists(Path);
                LastRefresh = DateTime.UtcNow;

                if (exists.HasValue && exists.Value)
                {
                    attributes = _cloudBlobService.FetchAttributes(Path);
                    attributesFetched = true;
                }
            }
        }

        public bool Exists()
        {
            lock (objLock)
            {
                return ExistsInternal();
            }
        }
        private bool ExistsInternal()
        {
            if (exists == null)
            {
                // if parent directory has been initialized, this blob can not exist
                string parentDirPath = AzurePathHelper.GetBlobDirectory(Path);
                if (BlobDirectoryCollection.Instance.GetOrCreate(parentDirPath).BlobsInitialized)
                {
                    SetExists(false);
                }
                else
                {
                    SetExists(_cloudBlobService.Exists(Path));
                }
            }

            return exists.Value;
        }
        public void SetExists(bool exists)
        {
            lock (objLock)
            {
                this.exists = exists;
                LastRefresh = DateTime.UtcNow;

                if (this.exists.Value)
                {
                    BlobDirectoryCollection.Instance.GetOrCreate(AzurePathHelper.GetBlobDirectory(Path)).SetExists();
                }
            }
        }


        public void Delete()
        {
            if (Exists())
            {
                lock (objLock)
                {
                    if (!SynchronizationHelper.Synchronizing() || _cloudBlobService.Exists(Path))
                    {
                        _cloudBlobService.Delete(Path);
                    }

                    SetExists(false);
                    _blobCacheService.Discard(Path);

                    BlobDirectoryCollection.Instance.GetOrCreate(AzurePathHelper.GetBlobDirectory(Path)).ResetExists();
                }
            }
        }

        public void Copy(string destPath, bool overwrite)
        {
            if (SynchronizationHelper.Synchronizing())
            {
                Reinitialize();
                if (!Exists())
                {
                    return;
                }
            }

            if (!Exists())
            {
                throw new FileNotFoundException($"Blob on path {Path} does not exist.");
            }

            var targetBlob = BlobCollection.Instance.GetOrCreate(destPath);

            if (!overwrite && targetBlob.Exists())
            {
                throw new InvalidOperationException($"Target blob on path {destPath} already exists.");
            }

            // must be synchronous as we delete asynchronously in MOVE
            lock (objLock)
            {
                _cloudBlobService.Copy(Path, targetBlob.Path);
            }
            targetBlob.SetExists(true);
        }

        public void Move(string destPath)
        {
            if (SynchronizationHelper.Synchronizing())
            {
                BlobCollection.Instance.GetOrCreate(destPath).Reinitialize();
            }

            Copy(destPath, false);
            Delete();
        }

        public byte[] Get()
        {
            lock (objLock)
            {
                return GetInternal();
            }
        }
        public byte[] GetInternal()
        {
            if (ExistsInternal())
            {
                var remoteLastModified = GetAttributeInternal(a => a.LastModified);
                return _blobCacheService.Get(Path, remoteLastModified);
            }

            return new byte[0];
        }

        public void Upload(Stream stream)
        {
            if (SynchronizationHelper.Synchronizing())
            {
                Reinitialize();
                if (Exists())
                {
                    return;
                }
            }

            IDictionary<string, string> metadata;

            lock (objLock)
            {
                bool exists = ExistsInternal();

                if (!exists)
                {
                    attributes = new BlobAttributes
                    {
                        Metadata = new Dictionary<string, string> {
                            { BlobMetadataEnum.DateCreated.ToString(), DateTime.UtcNow.ToString() }
                        }
                    };
                    metadata = attributes.Metadata;
                }
                else
                {
                    SetMetadataAttributeInternal(BlobMetadataEnum.DateCreated, DateTime.UtcNow.ToString());
                    metadata = GetAttributeInternal(a => a.Metadata);
                }

                attributes = _cloudBlobService.Upload(Path, metadata, stream);

                stream.Seek(0, SeekOrigin.Begin);
                _blobCacheService.Add(Path, stream, attributes.LastModified);

                SetExists(true);
                attributesFetched = true;
            }
        }

        public void Append(byte[] content)
        {
            lock (objLock)
            {
                byte[] data = GetInternal();

                SetMetadataAttributeInternal(BlobMetadataEnum.LastWriteTime, DateTime.UtcNow.ToString());

                using (var stream = new MemoryStream())
                {
                    stream.Write(data, 0, data.Length);
                    stream.Write(content, 0, content.Length);
                    stream.Seek(0, SeekOrigin.Begin);

                    attributes = _cloudBlobService.Upload(Path, GetAttribute(a => a.Metadata), stream);

                    LastRefresh = DateTime.UtcNow;
                    stream.Seek(0, SeekOrigin.Begin);
                    _blobCacheService.Add(Path, stream, attributes.LastModified);
                }
            }
        }

        public string GetUrl() => BlobContainerCollection.Instance.GetOrCreate(AccountInfo.Instance.RootContainer).IsPublic() ?
                GetAttribute(a => a.AbsoluteUri) :
                AzurePathHelper.GetDownloadUri(Path);

        public void SetMetadataAttributeAndSave(BlobMetadataEnum attribute, string value)
        {
            lock (objLock)
            {
                SetMetadataAttributeInternal(attribute, value);
                _cloudBlobService.SetMetadataAsync(Path, GetAttribute(a => a.Metadata));
            }
        }
        private void SetMetadataAttributeInternal(BlobMetadataEnum attribute, string value)
        {
            var metadata = GetAttributeInternal(a => a.Metadata);

            if (metadata.ContainsKey(attribute.ToString()))
            {
                metadata[attribute.ToString()] = value;
            }
            else
            {
                metadata.Add(new KeyValuePair<string, string>(attribute.ToString(), value));
            }
        }
    }
}
