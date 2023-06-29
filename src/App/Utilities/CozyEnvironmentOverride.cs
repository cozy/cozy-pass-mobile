using System;
using System.Threading.Tasks;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Flurl;

namespace Bit.App.Utilities
{
    public static class CozyEnvironmentOverride
    {
        public static async Task ExtractEnvFromUrl(Url uri, ICozyClouderyEnvService cozyClouderyEnvService)
        {
            var clouderyEnv = cozyClouderyEnvService.ParseClouderyEnvFromUrl(uri);

            if (!String.IsNullOrEmpty(clouderyEnv))
            {
                await cozyClouderyEnvService.SaveClouderyEnvOnAsyncStorage(clouderyEnv);
                await AlertNewEnvironment(cozyClouderyEnvService);
            }
        }

        private static async Task AlertNewEnvironment(ICozyClouderyEnvService cozyClouderyEnvService)
        {
            var environmentString = await cozyClouderyEnvService.GetClouderyEnvFromAsyncStorage();
            await App.Current.MainPage.DisplayAlert(
                "Environment",
                $"Environment has been overriden\n\n{environmentString}",
                AppResources.Ok
            );
        }
    }
}
