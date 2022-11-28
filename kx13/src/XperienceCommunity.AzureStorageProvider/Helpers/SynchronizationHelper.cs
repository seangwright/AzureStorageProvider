using CMS.Base;
using CMS.Core;
using CMS.Helpers;
using CMS.IO;

namespace AzureStorageProvider.Helpers
{
    internal static class SynchronizationHelper
    {
        public static bool Synchronizing() => WebFarmHelper.WebFarmEnabled && !CMSActionContext.CurrentLogWebFarmTasks;

        public static void LogDirectoryDeleteTask(string path)
        {
            if (CMSActionContext.CurrentLogWebFarmTasks)
            {
                path = StorageHelper.GetWebApplicationRelativePath(path);
                if (!string.IsNullOrEmpty(path))
                {
                    var service = Service.Resolve<IWebFarmService>();

                    service.CreateIOTask(new DeleteFolderWebFarmTask { Path = path, TaskFilePath = path });
                }
            }
        }
        public static void LogDeleteFileTask(string path)
        {
            if (CMSActionContext.CurrentLogWebFarmTasks)
            {
                path = StorageHelper.GetWebApplicationRelativePath(path);
                if (!string.IsNullOrEmpty(path))
                {
                    var service = Service.Resolve<IWebFarmService>();

                    service.CreateIOTask(new DeleteFileWebFarmTask { Path = path, TaskFilePath = path });
                }
            }
        }
        public static void LogUpdateFileTask(string path)
        {
            if (CMSActionContext.CurrentLogWebFarmTasks)
            {
                string relativePath = StorageHelper.GetWebApplicationRelativePath(path);
                if (!string.IsNullOrEmpty(relativePath))
                {
                    if (CMS.IO.File.Exists(path))
                    {
                        using (var str = CMS.IO.File.OpenRead(path))
                        {
                            var service = Service.Resolve<IWebFarmService>();

                            service.CreateIOTask(new UpdateFileWebFarmTask
                            {
                                Path = relativePath,
                                TaskFilePath = path,
                                TaskBinaryData = str,
                            });
                        }
                    }
                }
            }
        }
    }
}
