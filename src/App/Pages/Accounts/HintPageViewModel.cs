﻿using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class HintPageViewModel : BaseViewModel
    {
        private readonly IDeviceActionService _deviceActionService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly IApiService _apiService;
        private readonly ICozyClientService _cozyClientService;

        public HintPageViewModel()
        {
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _apiService = ServiceContainer.Resolve<IApiService>("apiService");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");

            PageTitle = AppResources.PasswordHint;
            SubmitCommand = new Command(async () => await SubmitAsync());
        }

        public Command SubmitCommand { get; }
        public string Email { get; set; }

        public async Task SubmitAsync()
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

            // Cozy customization, Email is not an email, it represents the Cozy URL as in
            // the login page : email validation check is disabled.
            if(false && !Email.Contains("@"))
            {
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred, AppResources.InvalidEmail, AppResources.Ok);
                return;
            }

            #region cozy
            var cozyURL = Email;
            await _cozyClientService.ConfigureEnvironmentFromCozyURLAsync(cozyURL);
            #endregion

            try
            {
                await _deviceActionService.ShowLoadingAsync(AppResources.Submitting);
                await _apiService.PostPasswordHintAsync(
                    new Core.Models.Request.PasswordHintRequest { Email = Email });
                await _deviceActionService.HideLoadingAsync();
                await Page.DisplayAlert(null, AppResources.PasswordHintAlert, AppResources.Ok);
                await Page.Navigation.PopModalAsync();
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
    }
}
