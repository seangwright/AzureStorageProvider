using System;
using AzureStorageProvider;
using AzureStorageProvider.Collections;
using AzureStorageProvider.Helpers;
using CMS;
using CMS.Base;
using CMS.Core;
using CMS.DataEngine;
using CMS.Helpers;
using CMS.IO;

using SystemDirectory = System.IO.Directory;

[assembly: RegisterModule(typeof(AzureStorageProviderModule))]

namespace AzureStorageProvider
{
    public class AzureStorageProviderModule : Module
    {
        public AzureStorageProviderModule() : base(nameof(AzureStorageProviderModule))
        {
        }

        protected override void OnInit()
        {
            base.OnInit();

            ApplicationEvents.End.Execute += End_Execute;

            var settings = Service.Resolve<IAppSettingsService>();

            if (settings[nameof(WebConfigKeys.CMSStorageProviderAssembly)] == "AzureStorageProvider")
            {
                var provider = new StorageProvider("AzureStorageProvider", "AzureStorageProvider", true);

                StorageHelper.MapStoragePath("/", provider);
            }


            if (SettingsKeyInfoProvider.GetSettingsKeyInfo(nameof(SettingsKeys.AzureStorageProviderEnableLogs)) == null)
            {
                var category = SettingsCategoryInfo.Provider.Get("CMS.Debug.General");
                // settings key does not exist
                var logEnabledKey = new SettingsKeyInfo
                {
                    KeyName = nameof(SettingsKeys.AzureStorageProviderEnableLogs),
                    KeyDisplayName = "Azure Storage Provider Enable Logs",
                    KeyDescription = "Enables logs",
                    KeyType = "boolean",
                    KeyCategoryID = category.CategoryID,
                    KeyDefaultValue = "False",
                    KeyIsGlobal = true,
                    KeyIsCustom = false,
                    KeyIsHidden = false
                };

                logEnabledKey.Insert();
            }

            if (ValidationHelper.GetBoolean(settings[nameof(WebConfigKeys.AzureStorageProviderInitializeAtAppStart)], false))
            {
                BlobDirectoryCollection.Instance.GetOrCreate(string.Empty).Reinitialize();
            }
        }

        private void End_Execute(object sender, EventArgs e)
        {
            // remove azure temp folder
            string tempFolder = AzurePathHelper.GetTempBlobPath(string.Empty);

            if (!string.IsNullOrEmpty(tempFolder) && SystemDirectory.Exists(tempFolder))
            {
                SystemDirectory.Delete(tempFolder, true);
            }
        }
    }
}
