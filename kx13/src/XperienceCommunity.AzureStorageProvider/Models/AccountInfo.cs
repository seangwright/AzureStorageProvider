﻿using System;
using System.Linq;
using AzureStorageProvider.Helpers;
using AzureStorageProvider.Tasks;
using CMS.Core;
using CMS.Helpers;
using CMS.Scheduler;

namespace AzureStorageProvider.Models
{
    /// <summary>
    /// Class which represents account info for connection to cloud.
    /// </summary>
    public class AccountInfo
    {
        private static AccountInfo _instance = null;

        public string SharedKey { get; set; }
        public string AccountName { get; set; }
        public string EndPoint { get; set; }
        public bool PublicContainer { get; set; }
        public string RootContainer { get; set; }
        public BlobCacheType BlobCacheType { get; set; }
        public int BlobCacheMinutes { get; set; }

        public static AccountInfo Instance
        {
            get
            {
                if (_instance == null)
                {
                    SetUp();
                }

                return _instance;
            }
        }

        private static void SetUp()
        {
            var settings = Service.Resolve<IAppSettingsService>();

            Enum.TryParse(ValidationHelper.GetString(settings[nameof(WebConfigKeys.AzureStorageProviderCacheType)], nameof(BlobCacheType.None)), out
            BlobCacheType cacheType);

            SetUp(
                accountName: ValidationHelper.GetString(settings[nameof(WebConfigKeys.CMSAzureAccountName)], string.Empty),
                sharedKey: ValidationHelper.GetString(settings[nameof(WebConfigKeys.CMSAzureSharedKey)], string.Empty),
                endPoint: ValidationHelper.GetString(settings[nameof(WebConfigKeys.CMSAzureCDNEndpoint)], string.Empty),
                publicContainer: ValidationHelper.GetBoolean(settings[nameof(WebConfigKeys.CMSAzurePublicContainer)], false),
                rootContainer: ValidationHelper.GetString(settings[nameof(WebConfigKeys.CMSAzureRootContainer)], string.Empty),
                blobCacheType: cacheType,
                blobCacheMinutes: ValidationHelper.GetInteger(settings[nameof(WebConfigKeys.AzureStorageProviderCacheClearMinutes)], 60)
            );
        }

        public static void SetUp(string accountName, string sharedKey, string endPoint, bool publicContainer, string rootContainer, BlobCacheType blobCacheType, int blobCacheMinutes)
        {
            _instance = new AccountInfo
            {
                AccountName = accountName,
                SharedKey = sharedKey,
                EndPoint = endPoint,
                PublicContainer = publicContainer,
                RootContainer = rootContainer,
                BlobCacheType = blobCacheType,
                BlobCacheMinutes = blobCacheMinutes
            };

            // set up cache clearing task
            if (blobCacheType != BlobCacheType.None)
            {
                bool exists = TaskInfo.Provider.Get()
                    .Column(nameof(TaskInfo.TaskID))
                    .WhereEquals(nameof(TaskInfo.TaskName), typeof(CacheClearingTask).FullName)
                    .TopN(1)
                    .Any();

                if (!exists)
                {
                    var taskInfo = new TaskInfo
                    {
                        TaskName = typeof(CacheClearingTask).FullName,
                        TaskDisplayName = "Clear Azure cached metadata and binary objects",
                        TaskAssemblyName = typeof(CacheClearingTask).Assembly.GetName().Name,
                        TaskClass = typeof(CacheClearingTask).FullName,
                        TaskInterval = SchedulingHelper.EncodeInterval(
                            new TaskInterval
                            {
                                Period = SchedulingHelper.PERIOD_MINUTE,
                                StartTime = DateTime.Now,
                                Every = blobCacheMinutes
                            }),
                        TaskNextRunTime = DateTime.Now,
                        TaskRunInSeparateThread = true,
                        TaskGUID = Guid.NewGuid(),
                        TaskData = string.Empty,
                        TaskAllowExternalService = false,
                        TaskEnabled = true,
                        TaskType = ScheduledTaskTypeEnum.System
                    };
                    TaskInfo.Provider.Set(taskInfo);
                }
            }
        }
    }
}
