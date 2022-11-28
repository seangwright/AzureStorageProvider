using System;
using AzureStorageProvider.Helpers;

namespace AzureStorageProvider
{
    /// <summary>
    /// Sample of FileInfo class of CMS.IO provider.
    /// </summary>
    public class FileInfo : CMS.IO.FileInfo
    {
        private bool? _exists;
        private readonly string _fullName = null;

        public override DateTime CreationTime
        {
            get => new File().GetCreationTime(FullName);
            set
            {
            }
        }

        public override string DirectoryName => CMS.IO.Path.GetDirectoryName(FullName);

        public override bool Exists
        {
            get
            {
                if (_exists == null)
                {
                    _exists = new File().Exists(FullName);
                }

                return _exists.Value;
            }
        }

        public override string Extension => CMS.IO.Path.GetExtension(FullName);

        public override string FullName => _fullName;

        public override bool IsReadOnly
        {
            get => false;

            set
            {
            }
        }

        public override DateTime LastAccessTime
        {
            get => new File().GetLastWriteTime(FullName);

            set
            {
            }
        }

        public override DateTime LastWriteTime
        {
            get => new File().GetLastWriteTime(FullName);

            set
            {
            }
        }

        public override long Length => new File().GetLength(FullName);

        public override string Name => CMS.IO.Path.GetFileName(FullName);

        public override CMS.IO.DirectoryInfo Directory => new DirectoryInfo(CMS.IO.Path.GetDirectoryName(FullName));

        public override CMS.IO.FileAttributes Attributes
        {
            get => new File().GetFileAttributes(FullName);

            set => new File().SetAttributes(FullName, value);
        }

        public FileInfo(string path) => _fullName = path;

        protected override CMS.IO.FileInfo CopyToInternal(string destFileName, bool overwrite)
        {
            new File().Copy(FullName, destFileName, overwrite);
            return new FileInfo(destFileName);
        }

        protected override CMS.IO.StreamWriter CreateTextInternal() => new File().CreateText(FullName);

        protected override void DeleteInternal()
        {
            new File().Delete(FullName);
            _exists = null;

            // Log the web farm task
            SynchronizationHelper.LogDeleteFileTask(FullName);
        }

        protected override void MoveToInternal(string destFileName)
        {
            new File().Move(FullName, destFileName);
            _exists = null;
        }

        protected override CMS.IO.FileStream OpenReadInternal() => new File().OpenRead(FullName);

        protected override CMS.IO.StreamReader OpenTextInternal() => new File().OpenText(FullName);
    }
}
