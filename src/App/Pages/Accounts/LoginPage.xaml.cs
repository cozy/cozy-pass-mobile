﻿using System;
using System.Threading.Tasks;
using Bit.App.Models;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class LoginPage : BaseContentPage
    {
        private readonly LoginPageViewModel _vm;
        private readonly AppOptions _appOptions;

        private bool _inputFocused;

        readonly LazyResolve<ILogger> _logger = new LazyResolve<ILogger>("logger");

        public LoginPage(string email = null, AppOptions appOptions = null)
        {
            _appOptions = appOptions;
            InitializeComponent();
            _vm = BindingContext as LoginPageViewModel;
            _vm.Page = this;
            _vm.StartTwoFactorAction = () => Device.BeginInvokeOnMainThread(async () => await StartTwoFactorAsync());
            _vm.LogInSuccessAction = () => Device.BeginInvokeOnMainThread(async () => await LogInSuccessAsync());
            _vm.UpdateTempPasswordAction =
                () => Device.BeginInvokeOnMainThread(async () => await UpdateTempPasswordAsync());
            _vm.CloseAction = async () =>
            {
                await _accountListOverlay.HideAsync();
                await Navigation.PopModalAsync();
            };
            _vm.IsEmailEnabled = string.IsNullOrWhiteSpace(email);
            _vm.IsIosExtension = _appOptions?.IosExtension ?? false;

            if (_vm.IsEmailEnabled)
            {
                _vm.ShowCancelButton = true;
            }
            _vm.Email = email;
            MasterPasswordEntry = _masterPassword;

            _email.ReturnType = ReturnType.Next;
            _email.ReturnCommand = new Command(() => _masterPassword.Focus());

            // Cozy customization, disable menu
            // password hint will be not requested from the login form
            /*
            if (Device.RuntimePlatform == Device.iOS)
            {
                ToolbarItems.Add(_moreItem);
            }
            else
            {
                ToolbarItems.Add(_getPasswordHint);
            }
            //*/

            if (Device.RuntimePlatform == Device.Android && !_vm.IsEmailEnabled)
            {
                ToolbarItems.Add(_removeAccount);
            }

            if (_appOptions?.IosExtension ?? false)
            {
                _vm.ShowCancelButton = true;
            }

            if (_appOptions?.HideAccountSwitcher ?? false)
            {
                ToolbarItems.Remove(_accountAvatar);
            }
        }

        public Entry MasterPasswordEntry { get; set; }

        protected override async void OnAppearing()
        {
            ThemeManager.SetInvertedTheme();
            base.OnAppearing();
            _mainContent.Content = _mainLayout;
            _accountAvatar?.OnAppearing();

            if (!_appOptions?.HideAccountSwitcher ?? false)
            {
                _vm.AvatarImageSource = await GetAvatarImageSourceAsync();
            }
            await _vm.InitAsync();
            if (!_inputFocused)
            {
                RequestFocus(string.IsNullOrWhiteSpace(_vm.Email) ? _email : _masterPassword);
                _inputFocused = true;
            }
        }

        protected override bool OnBackButtonPressed()
        {
            if (_accountListOverlay.IsVisible)
            {
                _accountListOverlay.HideAsync().FireAndForget();
                return true;
            }
            return false;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _accountAvatar?.OnDisappearing();
        }

        private async void LogIn_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.LogInAsync(true, _vm.IsEmailEnabled);
            }
        }

        private void Hint_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                Navigation.PushModalAsync(new NavigationPage(new HintPage()));
            }
        }

        private async void RemoveAccount_Clicked(object sender, EventArgs e)
        {
            await _accountListOverlay.HideAsync();
            if (DoOnce())
            {
                await _vm.RemoveAccountAsync();
            }
        }

        private void Cancel_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.CloseAction();
            }
        }

        private async void More_Clicked(object sender, EventArgs e)
        {
            try
            {
                await _accountListOverlay.HideAsync();
                _vm.MoreCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.Value.Exception(ex);
            }
        }

        private async Task StartTwoFactorAsync()
        {
            var page = new TwoFactorPage(false, _appOptions);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private async Task LogInSuccessAsync()
        {
            if (AppHelpers.SetAlternateMainPage(_appOptions))
            {
                return;
            }
            var previousPage = await AppHelpers.ClearPreviousPage();
            Application.Current.MainPage = new TabsPage(_appOptions, previousPage);
        }

        private async Task UpdateTempPasswordAsync()
        {
            var page = new UpdateTempPasswordPage();
            await Navigation.PushModalAsync(new NavigationPage(page));
        }
    }
}
