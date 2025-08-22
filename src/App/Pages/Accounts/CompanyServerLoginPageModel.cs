using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Bit.App.Utilities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Domain;
using Xamarin.Essentials;
using Xamarin.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bit.App.Pages
{
    public class CompanyServerLoginPageModel : BaseViewModel
    {
        private readonly IDeviceActionService _deviceActionService;
        private readonly IAuthService _authService;
        private readonly ISyncService _syncService;
        private readonly IApiService _apiService;
        private readonly ICryptoFunctionService _cryptoFunctionService;
        private readonly IStorageService _storageService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly IStateService _stateService;

        private string _email;

        public CompanyServerLoginPageModel()
        {
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _authService = ServiceContainer.Resolve<IAuthService>("authService");
            _syncService = ServiceContainer.Resolve<ISyncService>("syncService");
            _apiService = ServiceContainer.Resolve<IApiService>("apiService");
            _cryptoFunctionService = ServiceContainer.Resolve<ICryptoFunctionService>("cryptoFunctionService");
            _storageService = ServiceContainer.Resolve<IStorageService>("storageService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");

            PageTitle = AppResources.CompanyServerLogin;
            LogInCommand = new Command(async () => await LogInAsync());
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public Command LogInCommand { get; }
        public Action CloseAction { get; set; }

        public async Task LogInAsync()
        {
            if (Connectivity.NetworkAccess == NetworkAccess.None)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InternetConnectionRequiredMessage,
                    AppResources.InternetConnectionRequiredTitle);
                return;
            }

            string domain = GetDomainFromEmail(Email);

            if (domain == null)
            {
                await _platformUtilsService.ShowDialogAsync(
                    string.Format(AppResources.ValidationFieldRequired, AppResources.Email),
                    AppResources.AnErrorHasOccurred,
                    AppResources.Ok);
                return;
            }

            await _deviceActionService.ShowLoadingAsync(AppResources.LoggingIn);

            try
            {
                var twakeLoginUrlWithWellKnown = await FetchTwakeLoginUrlWithWellKnownAsync(domain);
                if (twakeLoginUrlWithWellKnown != null)
                {
                    await Browser.OpenAsync(twakeLoginUrlWithWellKnown, new BrowserLaunchOptions
                    {
                        LaunchMode = BrowserLaunchMode.SystemPreferred,
                        TitleMode = BrowserTitleMode.Show,
                        Flags = BrowserLaunchFlags.PresentAsPageSheet
                    });
                }
      
                await _deviceActionService.HideLoadingAsync();
                return;
            }
            catch (Exception e)
            {
                await _deviceActionService.HideLoadingAsync();
                await _platformUtilsService.ShowDialogAsync(
                    (e?.Message ?? AppResources.CompanyServerLoginError),
                    AppResources.AnErrorHasOccurred);
                return;
            }
        }

        public static string GetDomainFromEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            int atIndex = email.LastIndexOf('@');

            if (atIndex < 0)
            {
                return null;
            }

            return email.Substring(atIndex + 1);
        }

        public async Task<string> FetchTwakeLoginUrlWithWellKnownAsync(string domain)
        {
            try
            {
                string url = $"https://{domain}/.well-known/twake-configuration";

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonContent = await response.Content.ReadAsStringAsync();

                        JObject twakeConfiguration = JsonConvert.DeserializeObject<JObject>(jsonContent);

                        var twakeUrl = twakeConfiguration["twake-pass-login-uri"]?.ToString();

                        return twakeUrl;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
