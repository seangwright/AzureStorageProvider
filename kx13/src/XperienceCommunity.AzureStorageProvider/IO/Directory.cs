using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using AzureStorageProvider.Collections;
using AzureStorageProvider.Helpers;
using AzureStorageProvider.Models;
using CMS.Core;
using CMS.Helpers;
using CMS.IO;

namespace AzureStorageProvider
{
    /// <summary>
    /// Sample of Directory class of CMS.IO provider.
    /// </summary>
    public class Directory : AbstractDirectory
    {
        public bool IgnoreLastWriteTime
        {
            get
            {
                var settings = Service.Resolve<ISettingsService>();
                return ValidationHelper.GetBoolean(settings[nameof(WebConfigKeys.AzureStorageProviderIgnoreLastWriteTime)], false);
            }
        }
        private BlobDirectory Get(string path) => BlobDirectoryCollection.Instance.GetOrCreate(AzurePathHelper.GetBlobPath(path));
        public override CMS.IO.DirectoryInfo CreateDirectory(string path)
        {
            var info = new DirectoryInfo(path)
            {
                CreationTime = DateTime.Now,
                LastWriteTime = DateTime.Now,
            };

            // weird fallback from original implementation that checks whether file exists on file system
            // should be absolutely removed!!!
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            return info;
        }

        public override void Delete(string path) => Delete(path, false);

        public override void Delete(string path, bool recursive)
        {
            Get(path).Delete(!recursive);

            // weird fallback from original implementation that checks whether file exists on file system
            // should be absolutely removed!!!
            if (System.IO.Directory.Exists(path))
            {
                System.IO.Directory.Delete(path, recursive);
            }

            SynchronizationHelper.LogDirectoryDeleteTask(path);
        }

        public override void DeleteDirectoryStructure(string path) => Delete(path, true);

        public override bool Exists(string path)
        {
            bool exists = ExistsInCloud(path);

            // weird fallback from original implementation that checks whether file exists on file system
            // should be absolutely removed!!!
            return exists || System.IO.Directory.Exists(path);

        }

        private bool ExistsInCloud(string path) => Get(path).Exists();

        public override string[] GetDirectories(string path) => GetDirectories(path, "*");

        public override string[] GetDirectories(string path, string searchPattern) => GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);

        public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            // weird fallback from original implementation that checks whether file exists on file system
            // should be absolutely removed!!!
            string[] fileSystemSubDirectories = System.IO.Directory.Exists(path) ? System.IO.Directory.GetDirectories(path, searchPattern, searchOption == SearchOption.AllDirectories ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly) : new string[0];

            if (AzurePathHelper.ForceLowercase)
            {
                fileSystemSubDirectories = fileSystemSubDirectories.Select(s => s.ToLowerInvariant()).ToArray();
            }
            // end of weird fallback!
            //

            var condition = new Func<string, bool>(p => true);

            if (!string.IsNullOrEmpty(searchPattern) || searchPattern == "*")
            {
                var regex = RegexHelper.GetRegex(searchPattern.Replace("*", ".*"), true);
                condition = new Func<string, bool>(p => regex.IsMatch(Path.GetFileName(p)));
            }

            bool flat = searchOption == SearchOption.TopDirectoryOnly;

            string[] cloudSubDirectories = Get(path).GetSubdirectories(flat)
                .Where(d => d.Exists())
                .Select(d => AzurePathHelper.GetFileSystemPath(d.Path))
                .Where(condition)
                .ToArray();

            LoggingHelper.Log($"GetDirectories {path}", $"Base path: {path}, cloud directories: {string.Join(",", cloudSubDirectories)}, file system directories: {string.Join(",", fileSystemSubDirectories)}");

            var listOfAll = new List<string>(fileSystemSubDirectories);
            listOfAll.AddRange(cloudSubDirectories);

            return listOfAll.Distinct().ToArray();
        }

        public override string[] GetFiles(string path) => GetFiles(path, string.Empty);
        public override string[] GetFiles(string path, string searchPattern) => GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var condition = new Func<string, bool>(p => true);
            if (!string.IsNullOrEmpty(searchPattern))
            {
                var regex = RegexHelper.GetRegex(searchPattern.Replace("*", ".*"), true);
                condition = new Func<string, bool>(p => regex.IsMatch(Path.GetFileName(p)));
            }

            bool flat = searchOption == SearchOption.TopDirectoryOnly;

            string[] files = Get(path).GetBlobs(flat)
                .Select(d => AzurePathHelper.GetFileSystemPath(d.Path))
                .Where(condition)
                .ToArray();

            LoggingHelper.Log($"GetFiles {path}", $"Base path: {path}, condition: {condition}, files: {string.Join(",", files)}");

            return files;
        }

        public override void Move(string sourceDirName, string destDirName)
        {
            string sourceDirPath = AzurePathHelper.GetBlobPath(sourceDirName);
            string destDirPath = AzurePathHelper.GetBlobPath(destDirName);

            // if web farms are enabled, reinitialize source and destination folder to see if another web farm server has already moved the files
            if (SynchronizationHelper.Synchronizing())
            {
                BlobDirectoryCollection.Instance.GetOrCreate(sourceDirPath).Reinitialize();
                BlobDirectoryCollection.Instance.GetOrCreate(destDirPath).Reinitialize();
            }

            var sourceDirBlobs = Get(sourceDirName).GetBlobs(false);
            foreach (var blob in sourceDirBlobs)
            {
                blob.Move(destDirPath + "/" + blob.Path.Substring(sourceDirPath.Length + 1));
            }


            // we have to do this on file system too because of Dirs :(
            if (System.IO.Directory.Exists(sourceDirName))
            {
                System.IO.Directory.Move(sourceDirName, destDirName);
            }
        }

        public override void PrepareFilesForImport(string path) => Get(path).Reinitialize();

        public DateTime GetLastWriteTime(string path) => IgnoreLastWriteTime ? DateTime.MinValue : Get(path).GetLastWriteTime();

        public override IEnumerable<string> EnumerateFiles(string path, string searchPattern) => GetFiles(path, searchPattern);

        public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => GetDirectories(path, searchPattern, searchOption);

        public override DirectorySecurity GetAccessControl(string path) => new DirectorySecurity();
    }
}
