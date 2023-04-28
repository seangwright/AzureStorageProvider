using CMS.Core;
using CMS.DataEngine;

namespace AzureStorageProvider.Helpers
{
    internal sealed class LoggingHelper
    {
        public static void Log(string eventCode, string text)
        {
            if (SettingsKeyInfoProvider.GetBoolValue(nameof(SettingsKeys.AzureStorageProviderEnableLogs)))
            {
                var service = Service.Resolve<IEventLogService>();

                service.LogInformation("AzureStorageProvider", eventCode, text);
            }
        }
    }
}
