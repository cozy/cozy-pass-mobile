﻿using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class LoginPageViewModel : BaseViewModel
    {
        private const string Keys_RememberedEmail = "rememberedEmail";
        private const string Keys_RememberEmail = "rememberEmail";

        private readonly IDeviceActionService _deviceActionService;
        private readonly IAuthService _authService;
        private readonly ISyncService _syncService;
        private readonly IStorageService _storageService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly IStateService _stateService;
        private readonly ICozyClientService _cozyClientService;

        private bool _showPassword;
        private string _email;
        private string _masterPassword;

        public LoginPageViewModel()
        {
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _authService = ServiceContainer.Resolve<IAuthService>("authService");
            _syncService = ServiceContainer.Resolve<ISyncService>("syncService");
            _storageService = ServiceContainer.Resolve<IStorageService>("storageService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");

            PageTitle = AppResources.CozyPass;
            TogglePasswordCommand = new Command(TogglePassword);
            LogInCommand = new Command(async () => await LogInAsync());
        }

        public bool ShowPassword
        {
            get => _showPassword;
            set => SetProperty(ref _showPassword, value,
                additionalPropertyNames: new string[]
                {
                    nameof(ShowPasswordIcon)
                });
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string MasterPassword
        {
            get => _masterPassword;
            set => SetProperty(ref _masterPassword, value);
        }

        public Command LogInCommand { get; }
        public Command TogglePasswordCommand { get; }
        public string ShowPasswordIcon => ShowPassword ? "" : "";
        public bool RememberEmail { get; set; }

        public async Task InitAsync()
        {
            if(string.IsNullOrWhiteSpace(Email))
            {
                Email = await _storageService.GetAsync<string>(Keys_RememberedEmail);
            }
            var rememberEmail = await _storageService.GetAsync<bool?>(Keys_RememberEmail);
            RememberEmail = rememberEmail.GetValueOrDefault(true);
        }

        public async Task LogInAsync()
        {
            if(Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.None)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InternetConnectionRequiredMessage,
                    AppResources.InternetConnectionRequiredTitle);
                return;
            }
            if(string.IsNullOrWhiteSpace(Email))
            {
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred,
                    string.Format(AppResources.ValidationFieldRequired, AppResources.EmailAddress),
                    AppResources.Ok);
                return;
            }

            #region cozy
            // Deactivate test on Email since Email has been used for the CozyURL
            if (false && !Email.Contains("@"))
            {
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred, AppResources.InvalidEmail, AppResources.Ok);
                return;
            }
            #endregion

            if (string.IsNullOrWhiteSpace(MasterPassword))
            {
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred,
                    string.Format(AppResources.ValidationFieldRequired, AppResources.MasterPassword),
                    AppResources.Ok);
                return;
            }

            ShowPassword = false;
            try
            {
                await _deviceActionService.ShowLoadingAsync(AppResources.LoggingIn);

                #region cozy
                // Email field is used as CozyURL, it is not renamed not to change the original code
                // too much.
                var cozyURL = Email;
                await _cozyClientService.ConfigureEnvironmentFromCozyURLAsync(cozyURL);
                var email = _cozyClientService.GetEmailFromCozyURL(cozyURL);
                var response = await _authService.LogInAsync(email, MasterPassword);
                #endregion

                MasterPassword = string.Empty;
                if(RememberEmail)
                {
                    await _storageService.SaveAsync(Keys_RememberedEmail, Email);
                }
                else
                {
                    await _storageService.RemoveAsync(Keys_RememberedEmail);
                }
                await _deviceActionService.HideLoadingAsync();
                if(response.TwoFactor)
                {
                    var page = new TwoFactorPage();
                    await Page.Navigation.PushModalAsync(new NavigationPage(page));
                }
                else
                {
                    var disableFavicon = await _storageService.GetAsync<bool?>(Constants.DisableFaviconKey);
                    await _stateService.SaveAsync(Constants.DisableFaviconKey, disableFavicon.GetValueOrDefault());
                    var task = Task.Run(async () => await _syncService.FullSyncAsync(true));
                    Application.Current.MainPage = new TabsPage();
                }
            }
            catch(ApiException e)
            {
                await _deviceActionService.HideLoadingAsync();
                if(e?.Error != null)
                {
                    await _platformUtilsService.ShowDialogAsync(e.Error.GetSingleMessage(),
                        AppResources.AnErrorHasOccurred);
                }
            }
        }

        public void TogglePassword()
        {
            ShowPassword = !ShowPassword;
            (Page as LoginPage).MasterPasswordEntry.Focus();
        }
    }
}
