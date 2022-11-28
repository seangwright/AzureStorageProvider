using System;
using System.Collections.Generic;
using System.Linq;
using AzureStorageProvider.Azure;
using AzureStorageProvider.Collections;
using AzureStorageProvider.Helpers;
using CMS.Core;

namespace AzureStorageProvider.Models
{
    public class BlobDirectory : IObjectWithPath<BlobDirectory>
    {
        private readonly object objLock = new object();
        private bool? exists = null;
        private readonly ICloudDirectoryService cloudDirectoryService;

        public string Path
        {
            get;
            protected set;
        }

        public bool BlobsInitialized { get; private set; } = false;

        public BlobDirectory() : this(Service.Resolve<ICloudDirectoryService>())
        {
        }

        public BlobDirectory(ICloudDirectoryService cloudDirectoryService) => this.cloudDirectoryService = cloudDirectoryService;

        protected void TryInitializeFromParent()
        {
            if (BlobsInitialized)
            {
                return;
            }

            // if there is initialized folder in the hierarchy, no need to initialize again
            bool pathsHaveInitializedBlobs = DirectoryHelper
                .PathToParent(Path, string.Empty)
                .Any(p =>
                {
                    var dir = BlobDirectoryCollection.Instance.TryGet(p);
                    return dir != null && dir.BlobsInitialized;
                });

            if (pathsHaveInitializedBlobs)
            {
                lock (objLock)
                {
                    BlobsInitialized = true;
                }

                return;
            }
        }

        protected void InitializeBlobs(bool forceRefresh = false)
        {
            lock (objLock)
            {
                InitializeBlobsInternal(forceRefresh);
            }
        }
        protected void InitializeBlobsInternal(bool forceRefresh = false)
        {
            if (!forceRefresh)
            {
                TryInitializeFromParent();
            }

            if (!forceRefresh && BlobsInitialized)
            {
                exists = BlobCollection.Instance
                    .GetStartingWith(Path + "/", false)
                    .Any(b => b.Exists());

                return;
            }

            // at this point we need to get data from remote
            var blobs = cloudDirectoryService.GetBlobs(Path);

            LoggingHelper.Log($"Blobs for path {Path}", $"{string.Join(",", blobs.Select(b => b.Path))}");

            BlobCollection.Instance.AddRangeDistinct(blobs);
            BlobsInitialized = true;
            exists = blobs.Any();

            // update directories in collection
            var subDirectoriesPaths = new List<string>();

            blobs
                .Select(b => AzurePathHelper.GetBlobDirectory(b.Path))
                .Distinct()
                .Where(d => d != Path)
                .ToList()
                .ForEach(p => subDirectoriesPaths.AddRange(DirectoryHelper.PathToParent(p, Path)));

            var subDirectories = subDirectoriesPaths
                .Distinct()
                .Select(d => new BlobDirectory().InitializeWithFlag(d))
                .ToList();

            BlobDirectoryCollection.Instance.AddRangeDistinct(subDirectories);
        }

        public BlobDirectory Initialize(string directoryPath)
        {
            Path = directoryPath;
            TryInitializeFromParent();

            return this;
        }
        public void ResetExists()
        {
            if (exists.HasValue)
            {
                lock (objLock)
                {
                    exists = null;
                    if (BlobsInitialized)
                    {
                        ExistsInternal();
                    }
                }
                if (!string.IsNullOrEmpty(Path))
                {
                    ResetExistsForParents();
                }
            }
        }
        public void ResetExistsForParents()
        {
            string blobDirectory = AzurePathHelper.GetBlobDirectory(Path);
            if (blobDirectory != string.Empty)
            {
                if (exists.HasValue && exists.Value)
                {
                    BlobDirectoryCollection.Instance.GetOrCreate(blobDirectory).SetExists();
                }
                else
                {
                    BlobDirectoryCollection.Instance.GetOrCreate(blobDirectory).ResetExists();
                }
            }
        }
        public void SetExists()
        {
            lock (objLock)
            {
                exists = true;
            }

            ResetExistsForParents();
        }

        public BlobDirectory Reinitialize()
        {
            foreach (var blob in GetBlobs(false).ToList())
            {
                blob.Uninitialize();
            }

            foreach (var dir in GetSubdirectories(false).ToList())
            {
                dir.Uninitialize();
            }

            InitializeBlobs(true);
            ResetExistsForParents();

            return this;
        }

        protected void Uninitialize()
        {
            lock (objLock)
            {
                BlobsInitialized = false;
                exists = false;
            }
        }

        protected BlobDirectory InitializeWithFlag(string directoryPath)
        {
            lock (objLock)
            {
                BlobsInitialized = true;
                exists = true;
            }

            return Initialize(directoryPath);
        }

        public IEnumerable<Blob> GetBlobs(bool flat)
        {
            if (!BlobsInitialized)
            {
                InitializeBlobs();
            }

            return BlobCollection.Instance.GetStartingWith(Path + "/", flat).Where(b => b.Exists());
        }

        public void Delete(bool flat)
        {
            if (SynchronizationHelper.Synchronizing())
            {
                Reinitialize();
            }
            else
            {
                foreach (var blob in GetBlobs(flat).ToList())
                {
                    blob.Delete();
                }

                ResetExists();
            }
        }

        public bool Exists()
        {
            lock (objLock)
            {
                return ExistsInternal();
            }
        }

        public bool ExistsInternal()
        {
            if (exists.HasValue)
            {
                return exists.Value;
            }

            // if any blob below current directory exists, folder must exist too
            if (BlobCollection.Instance.GetStartingWith(Path + "/", false).Any(b => b.Exists()))
            {
                exists = true;
                return true;
            }

            // if there is no blob, find out if any parent has already been initialized
            var parents = DirectoryHelper.PathToParent(Path, string.Empty);

            if (parents.Any(p => BlobDirectoryCollection.Instance.GetOrCreate(p).BlobsInitialized))
            {
                exists = false;
                return false;
            }

            // otherwise we need to initialize
            if (!BlobsInitialized)
            {
                InitializeBlobsInternal(false);
                return exists.Value;
            }

            // at this point we know the DIR does not exist
            exists = false;

            return false;
        }

        public IEnumerable<BlobDirectory> GetSubdirectories(bool flat)
        {
            if (!BlobsInitialized)
            {
                InitializeBlobs();
            }

            var dirsPaths = new List<string>();

            BlobCollection.Instance.GetStartingWith(Path + "/", flat)
                .Where(b => b.Exists())
                .Select(b => AzurePathHelper.GetBlobDirectory(b.Path))
                .Union(BlobDirectoryCollection.Instance.GetStartingWith(Path + "/", flat).Where(d => d.Exists()).Select(d => d.Path))
                .Distinct()
                .ToList()
                .ForEach(b => dirsPaths.AddRange(DirectoryHelper.PathToParent(b, Path)));

            dirsPaths = dirsPaths.Distinct().ToList();

            if (flat)
            {
                dirsPaths = dirsPaths.Where(d => AzurePathHelper.GetBlobDirectory(d) == Path).ToList();
            }

            return dirsPaths.Select(p => BlobDirectoryCollection.Instance.GetOrCreate(p));
        }

        public DateTime GetLastWriteTime() => GetBlobs(false)
                .Select(b => b.GetAttribute(a => a.LastModified))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
    }
}
