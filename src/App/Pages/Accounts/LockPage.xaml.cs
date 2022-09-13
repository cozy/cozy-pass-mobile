﻿using System;
using System.Threading.Tasks;
using Bit.App.Models;
using Bit.App.Resources;
using Bit.App.Utilities;
using Bit.Core.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class LockPage : BaseContentPage
    {
        private readonly AppOptions _appOptions;
        private readonly bool _autoPromptBiometric;
        private readonly LockPageViewModel _vm;

        private bool _promptedAfterResume;
        private bool _appeared;

        public LockPage(AppOptions appOptions = null, bool autoPromptBiometric = true)
        {
            _appOptions = appOptions;
            _autoPromptBiometric = autoPromptBiometric;
            InitializeComponent();
            _vm = BindingContext as LockPageViewModel;
            _vm.Page = this;
            _vm.UnlockedAction = () => Device.BeginInvokeOnMainThread(async () => await UnlockedAsync());

            // Cozy customization, disable menu
            // logout will be not requested from the login form
            /*
            if (Device.RuntimePlatform == Device.iOS)
            {
                ToolbarItems.Add(_moreItem);
            }
            else
            {
                ToolbarItems.Add(_logOut);
            }
            //*/
        }

        public Entry SecretEntry
        {
            get
            {
                if (_vm?.PinLock ?? false)
                {
                    return _pin;
                }
                return _masterPassword;
            }
        }

        public async Task PromptBiometricAfterResumeAsync()
        {
            if (_vm.BiometricLock)
            {
                await Task.Delay(500);
                if (!_promptedAfterResume)
                {
                    _promptedAfterResume = true;
                    await _vm?.PromptBiometricAsync();
                }
            }
        }

        protected override async void OnAppearing()
        {
            ThemeManager.SetInvertedTheme();
            base.OnAppearing();
            if (_appeared)
            {
                return;
            }

            _appeared = true;
            _mainContent.Content = _mainLayout;

            _accountAvatar?.OnAppearing();

            _vm.AvatarImageSource = await GetAvatarImageSourceAsync();

            await _vm.InitAsync();

            _vm.FocusSecretEntry += PerformFocusSecretEntry;

            if (!_vm.BiometricLock)
            {
                RequestFocus(SecretEntry);
            }
            else
            {
                if (_vm.UsingKeyConnector && !_vm.PinLock)
                {
                    _passwordGrid.IsVisible = false;
                    _unlockButton.IsVisible = false;
                }
                if (_autoPromptBiometric)
                {
                    var tasks = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        Device.BeginInvokeOnMainThread(async () => await _vm.PromptBiometricAsync());
                    });
                }
            }
        }

        private void PerformFocusSecretEntry(int? cursorPosition)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                SecretEntry.Focus();
                if (cursorPosition.HasValue)
                {
                    SecretEntry.CursorPosition = cursorPosition.Value;
                }
            });
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

        private void Unlock_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                var tasks = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    Device.BeginInvokeOnMainThread(async () => await _vm.SubmitAsync());
                });
            }
        }

        private async void LogOut_Clicked(object sender, EventArgs e)
        {
            await _accountListOverlay.HideAsync();
            if (DoOnce())
            {
                await _vm.LogOutAsync();
            }
        }

        private async void Biometric_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.PromptBiometricAsync();
            }
        }

        private async void More_Clicked(object sender, System.EventArgs e)
        {
            await _accountListOverlay.HideAsync();

            if (!DoOnce())
            {
                return;
            }

            var selection = await DisplayActionSheet(AppResources.Options,
                AppResources.Cancel, null, AppResources.LogOut);

            if (selection == AppResources.LogOut)
            {
                await _vm.LogOutAsync();
            }
        }

        private async Task UnlockedAsync()
        {
            if (AppHelpers.SetAlternateMainPage(_appOptions))
            {
                return;
            }
            var previousPage = await AppHelpers.ClearPreviousPage();

            Application.Current.MainPage = new TabsPage(_appOptions, previousPage);
        }
    }
}
