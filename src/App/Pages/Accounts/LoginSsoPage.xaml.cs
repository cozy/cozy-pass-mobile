﻿using Bit.App.Models;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Bit.App.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class LoginSsoPage : BaseContentPage
    {
        private readonly IStorageService _storageService;
        private readonly IMessagingService _messagingService;
        private readonly IVaultTimeoutService _vaultTimeoutService;
        private readonly LoginSsoPageViewModel _vm;
        private readonly AppOptions _appOptions;

        private AppOptions _appOptionsCopy;

        public LoginSsoPage(AppOptions appOptions = null)
        {
            _storageService = ServiceContainer.Resolve<IStorageService>("storageService");
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _vaultTimeoutService = ServiceContainer.Resolve<IVaultTimeoutService>("vaultTimeoutService");
            _messagingService.Send("showStatusBar", true);
            _appOptions = appOptions;
            InitializeComponent();
            _vm = BindingContext as LoginSsoPageViewModel;
            _vm.Page = this;
            _vm.StartTwoFactorAction = () => Device.BeginInvokeOnMainThread(async () => await StartTwoFactorAsync());
            _vm.StartSetPasswordAction = () =>
                Device.BeginInvokeOnMainThread(async () => await StartSetPasswordAsync());
            _vm.SsoAuthSuccessAction = () => Device.BeginInvokeOnMainThread(async () => await SsoAuthSuccessAsync());
            _vm.UpdateTempPasswordAction =
                () => Device.BeginInvokeOnMainThread(async () => await UpdateTempPasswordAsync());
            _vm.CloseAction = async () =>
            {
                _messagingService.Send("showStatusBar", false);
                await Navigation.PopModalAsync();
            };
            if (Device.RuntimePlatform == Device.Android)
            {
                ToolbarItems.RemoveAt(0);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitAsync();
            if (string.IsNullOrWhiteSpace(_vm.OrgIdentifier))
            {
                RequestFocus(_orgIdentifier);
            }
        }

        private void CopyAppOptions()
        {
            if (_appOptions != null)
            {
                // create an object copy of _appOptions to persist values when app is exited during web auth flow
                _appOptionsCopy = new AppOptions();
                _appOptionsCopy.SetAllFrom(_appOptions);
            }
        }

        private void RestoreAppOptionsFromCopy()
        {
            if (_appOptions != null)
            {
                // restore values to original readonly _appOptions object from copy
                _appOptions.SetAllFrom(_appOptionsCopy);
                _appOptionsCopy = null;
            }
        }

        private async void LogIn_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                CopyAppOptions();
                await _vm.LogInAsync();
            }
        }

        private void Close_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.CloseAction();
            }
        }

        private async Task StartTwoFactorAsync()
        {
            RestoreAppOptionsFromCopy();
            var page = new TwoFactorPage(true, _appOptions, _vm.OrgIdentifier);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private async Task StartSetPasswordAsync()
        {
            RestoreAppOptionsFromCopy();
            var page = new SetPasswordPage(_appOptions, _vm.OrgIdentifier);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }
        
        private async Task UpdateTempPasswordAsync()
        {
            var page = new UpdateTempPasswordPage();
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private async Task SsoAuthSuccessAsync()
        {
            RestoreAppOptionsFromCopy();
            await AppHelpers.ClearPreviousPage();
            Application.Current.MainPage = new NavigationPage(new LockPage(_appOptions));
        }
    }
}
