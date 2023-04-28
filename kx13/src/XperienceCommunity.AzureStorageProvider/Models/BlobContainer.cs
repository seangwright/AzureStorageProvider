using System;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Helpers;
using CMS.Core;
using Microsoft.Azure.Storage.Blob;

namespace AzureStorageProvider.Models
{
    public class BlobContainer : IObjectWithPath<BlobContainer>
    {
        public string Path
        {
            get;
            private set;
        }

        private bool? _containerExists = null;
        private bool? _containerIsPublic = null;
        private readonly ICloudBlobContainerService cloudBlobContainerService;

        public BlobContainer() : this(Service.Resolve<ICloudBlobContainerService>())
        {
        }

        public BlobContainer(ICloudBlobContainerService cloudBlobContainerService) => this.cloudBlobContainerService = cloudBlobContainerService;

        public BlobContainer Initialize(string name)
        {
            if (!BlobContainerHelper.ValidateName(name))
            {
                throw new ArgumentOutOfRangeException($"Container name '{name}' does not comply with requirements for container names.");
            }

            Path = name;

            if (name == AccountInfo.Instance.RootContainer && !Exists())
            {
                Create();
            }

            return this;
        }

        public void Create()
        {
            if (AccountInfo.Instance.PublicContainer)
            {
                cloudBlobContainerService.Create(Path, BlobContainerPublicAccessType.Blob);
                _containerIsPublic = true;
            }
            else
            {
                cloudBlobContainerService.Create(Path);
                _containerIsPublic = false;
            }

            _containerExists = true;
        }
        public bool Exists()
        {
            if (_containerExists == null)
            {
                _containerExists = cloudBlobContainerService.Exists(Path);
            }

            return _containerExists.Value;
        }

        public bool IsPublic()
        {
            if (_containerIsPublic == null)
            {
                _containerIsPublic = cloudBlobContainerService.GetPublicAccess(Path) == BlobContainerPublicAccessType.Blob;
            }

            return _containerIsPublic.Value;
        }

        public void Delete()
        {
            if (Exists())
            {
                cloudBlobContainerService.DeleteAsync(Path);
                _containerExists = false;
            }
        }
    }
}
