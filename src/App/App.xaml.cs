﻿using System;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Models;
using Bit.App.Pages;
using Bit.App.Resources;
using Bit.App.Services;
using Bit.App.Utilities;
using Bit.App.Utilities.AccountManagement;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace Bit.App
{
    public partial class App : Application, IAccountsManagerHost
    {
        private readonly IBroadcasterService _broadcasterService;
        private readonly IMessagingService _messagingService;
        private readonly IStateService _stateService;
        private readonly IVaultTimeoutService _vaultTimeoutService;
        private readonly ISyncService _syncService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly IAuthService _authService;
        private readonly IStorageService _secureStorageService;
        private readonly IDeviceActionService _deviceActionService;
        private readonly IAccountsManager _accountsManager;

        private static bool _isResumed;

        public App(AppOptions appOptions)
        {
            Options = appOptions ?? new AppOptions();
            if (Options.IosExtension)
            {
                Current = this;
                return;
            }
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");
            _vaultTimeoutService = ServiceContainer.Resolve<IVaultTimeoutService>("vaultTimeoutService");
            _syncService = ServiceContainer.Resolve<ISyncService>("syncService");
            _authService = ServiceContainer.Resolve<IAuthService>("authService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _secureStorageService = ServiceContainer.Resolve<IStorageService>("secureStorageService");
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _accountsManager = ServiceContainer.Resolve<IAccountsManager>("accountsManager");

            _accountsManager.Init(() => Options, this);

            Bootstrap();
            _broadcasterService.Subscribe(nameof(App), async (message) =>
            {
                try
                {
                    if (message.Command == "showDialog")
                    {
                        var details = message.Data as DialogDetails;
                        var confirmed = true;
                        var confirmText = string.IsNullOrWhiteSpace(details.ConfirmText) ?
                            AppResources.Ok : details.ConfirmText;
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            if (!string.IsNullOrWhiteSpace(details.CancelText))
                            {
                                confirmed = await Current.MainPage.DisplayAlert(details.Title, details.Text, confirmText,
                                    details.CancelText);
                            }
                            else
                            {
                                await Current.MainPage.DisplayAlert(details.Title, details.Text, confirmText);
                            }
                            _messagingService.Send("showDialogResolve", new Tuple<int, bool>(details.DialogId, confirmed));
                        });
                    }
                    else if (message.Command == "resumed")
                    {
                        if (Device.RuntimePlatform == Device.iOS)
                        {
                            ResumedAsync().FireAndForget();
                        }
                    }
                    else if (message.Command == "slept")
                    {
                        if (Device.RuntimePlatform == Device.iOS)
                        {
                            await SleptAsync();
                        }
                    }
                    else if (message.Command == "migrated")
                    {
                        await Task.Delay(1000);
                        await _accountsManager.NavigateOnAccountChangeAsync();
                    }
                    else if (message.Command == "popAllAndGoToTabGenerator" ||
                        message.Command == "popAllAndGoToTabMyVault" ||
                        message.Command == "popAllAndGoToTabSend" ||
                        message.Command == "popAllAndGoToAutofillCiphers")
                    {
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            if (Current.MainPage is TabsPage tabsPage)
                            {
                                while (tabsPage.Navigation.ModalStack.Count > 0)
                                {
                                    await tabsPage.Navigation.PopModalAsync(false);
                                }
                                if (message.Command == "popAllAndGoToAutofillCiphers")
                                {
                                    Current.MainPage = new NavigationPage(new AutofillCiphersPage(Options));
                                }
                                else if (message.Command == "popAllAndGoToTabMyVault")
                                {
                                    Options.MyVaultTile = false;
                                    tabsPage.ResetToVaultPage();
                                }
                                else if (message.Command == "popAllAndGoToTabGenerator")
                                {
                                    Options.GeneratorTile = false;
                                    tabsPage.ResetToGeneratorPage();
                                }
                                else if (message.Command == "popAllAndGoToTabSend")
                                {
                                    tabsPage.ResetToSendPage();
                                }
                            }
                        });
                    }
                    else if (message.Command == "convertAccountToKeyConnector")
                    {
                        Device.BeginInvokeOnMainThread(async () =>
                        {
                            await Application.Current.MainPage.Navigation.PushModalAsync(
                                new NavigationPage(new RemoveMasterPasswordPage()));
                        });
                    }
                }
                catch (Exception ex)
                {
                    LoggerHelper.LogEvenIfCantBeResolved(ex);
                }
            });
        }

        public AppOptions Options { get; private set; }

        protected async override void OnStart()
        {
            System.Diagnostics.Debug.WriteLine("XF App: OnStart");
            await ClearCacheIfNeededAsync();
            Prime();
            if (string.IsNullOrWhiteSpace(Options.Uri))
            {
                var updated = await AppHelpers.PerformUpdateTasksAsync(_syncService, _deviceActionService,
                    _stateService);
                if (!updated)
                {
                    SyncIfNeeded();
                }
            }
            if (Device.RuntimePlatform == Device.Android)
            {
                await _vaultTimeoutService.CheckVaultTimeoutAsync();
                // Reset delay on every start
                _vaultTimeoutService.DelayLockAndLogoutMs = null;
            }
            _messagingService.Send("startEventTimer");
        }

        protected async override void OnSleep()
        {
            System.Diagnostics.Debug.WriteLine("XF App: OnSleep");
            _isResumed = false;
            if (Device.RuntimePlatform == Device.Android)
            {
                var isLocked = await _vaultTimeoutService.IsLockedAsync();
                if (!isLocked)
                {
                    await _stateService.SetLastActiveTimeAsync(_deviceActionService.GetActiveTime());
                }
                if (!SetTabsPageFromAutofill(isLocked))
                {
                    ClearAutofillUri();
                }
                await SleptAsync();
            }
        }

        protected override void OnResume()
        {
            System.Diagnostics.Debug.WriteLine("XF App: OnResume");
            _isResumed = true;
            if (Device.RuntimePlatform == Device.Android)
            {
                ResumedAsync().FireAndForget();
            }
        }

        private async Task SleptAsync()
        {
            await _vaultTimeoutService.CheckVaultTimeoutAsync();
            _messagingService.Send("stopEventTimer");
        }

        private async Task ResumedAsync()
        {
            await _stateService.CheckExtensionActiveUserAndSwitchIfNeededAsync();
            await _vaultTimeoutService.CheckVaultTimeoutAsync();
            _messagingService.Send("startEventTimer");
            await UpdateThemeAsync();
            await ClearCacheIfNeededAsync();
            Prime();
            SyncIfNeeded();
            if (Current.MainPage is NavigationPage navPage && navPage.CurrentPage is LockPage lockPage)
            {
                await lockPage.PromptBiometricAfterResumeAsync();
            }
        }

        public async Task UpdateThemeAsync()
        {
            await Device.InvokeOnMainThreadAsync(() =>
            {
                ThemeManager.SetTheme(Current.Resources);
                _messagingService.Send("updatedTheme");
            });
        }

        private void SetCulture()
        {
            // Calendars are removed by linker. ref https://bugzilla.xamarin.com/show_bug.cgi?id=59077
            new System.Globalization.ThaiBuddhistCalendar();
            new System.Globalization.HijriCalendar();
            new System.Globalization.UmAlQuraCalendar();
        }

        private async Task ClearCacheIfNeededAsync()
        {
            var lastClear = await _stateService.GetLastFileCacheClearAsync();
            if ((DateTime.UtcNow - lastClear.GetValueOrDefault(DateTime.MinValue)).TotalDays >= 1)
            {
                var task = Task.Run(() => _deviceActionService.ClearCacheAsync());
            }
        }

        private void ClearAutofillUri()
        {
            if (Device.RuntimePlatform == Device.Android && !string.IsNullOrWhiteSpace(Options.Uri))
            {
                Options.Uri = null;
            }
        }

        private bool SetTabsPageFromAutofill(bool isLocked)
        {
            if (Device.RuntimePlatform == Device.Android && !string.IsNullOrWhiteSpace(Options.Uri) &&
                !Options.FromAutofillFramework)
            {
                Task.Run(() =>
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        Options.Uri = null;
                        if (isLocked)
                        {
                            Current.MainPage = new NavigationPage(new LockPage());
                        }
                        else
                        {
                            Current.MainPage = new TabsPage();
                        }
                    });
                });
                return true;
            }
            return false;
        }

        private void Prime()
        {
            Task.Run(() =>
            {
                var word = EEFLongWordList.Instance.List[1];
                var parsedDomain = DomainName.TryParse("https://bitwarden.com", out var domainName);
            });
        }

        private void Bootstrap()
        {
            InitializeComponent();
            SetCulture();
            ThemeManager.SetTheme(Current.Resources);
            Current.RequestedThemeChanged += (s, a) =>
            {
                UpdateThemeAsync();
            };
            Current.MainPage = new NavigationPage(new HomePage(Options));
            _accountsManager.NavigateOnAccountChangeAsync().FireAndForget();
            ServiceContainer.Resolve<MobilePlatformUtilsService>("platformUtilsService").Init();
        }

        private void SyncIfNeeded()
        {
            if (Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.None)
            {
                return;
            }
            Task.Run(async () =>
            {
                var lastSync = await _syncService.GetLastSyncAsync();
                if (lastSync == null || ((DateTime.UtcNow - lastSync) > TimeSpan.FromMinutes(30)))
                {
                    await Task.Delay(1000);
                    await _syncService.FullSyncAsync(false);
                }
            });
        }

        public async Task SetPreviousPageInfoAsync()
        {
            PreviousPageInfo lastPageBeforeLock = null;
            if (Current.MainPage is TabbedPage tabbedPage && tabbedPage.Navigation.ModalStack.Count > 0)
            {
                var topPage = tabbedPage.Navigation.ModalStack[tabbedPage.Navigation.ModalStack.Count - 1];
                if (topPage is NavigationPage navPage)
                {
                    if (navPage.CurrentPage is CipherDetailsPage cipherDetailsPage)
                    {
                        lastPageBeforeLock = new PreviousPageInfo
                        {
                            Page = "view",
                            CipherId = cipherDetailsPage.ViewModel.CipherId
                        };
                    }
                    else if (navPage.CurrentPage is CipherAddEditPage cipherAddEditPage && cipherAddEditPage.ViewModel.EditMode)
                    {
                        lastPageBeforeLock = new PreviousPageInfo
                        {
                            Page = "edit",
                            CipherId = cipherAddEditPage.ViewModel.CipherId
                        };
                    }
                }
            }
            await _stateService.SetPreviousPageInfoAsync(lastPageBeforeLock);
        }

        public void Navigate(NavigationTarget navTarget, INavigationParams navParams)
        {
            switch (navTarget)
            {
                case NavigationTarget.HomeLogin:
                    Current.MainPage = new NavigationPage(new HomePage(Options));
                    break;
                case NavigationTarget.Login:
                    if (navParams is LoginNavigationParams loginParams)
                    {
                        Current.MainPage = new NavigationPage(new LoginPage(loginParams.Email, Options));
                    }
                    break;
                case NavigationTarget.Lock:
                    if (navParams is LockNavigationParams lockParams)
                    {
                        Current.MainPage = new NavigationPage(new LockPage(Options, lockParams.AutoPromptBiometric));
                    }
                    else
                    {
                        Current.MainPage = new NavigationPage(new LockPage(Options));
                    }
                    break;
                case NavigationTarget.Home:
                    Current.MainPage = new TabsPage(Options);
                    break;
                case NavigationTarget.AddEditCipher:
                    Current.MainPage = new NavigationPage(new CipherAddEditPage(appOptions: Options));
                    break;
                case NavigationTarget.AutofillCiphers:
                    Current.MainPage = new NavigationPage(new AutofillCiphersPage(Options));
                    break;
                case NavigationTarget.SendAddEdit:
                    Current.MainPage = new NavigationPage(new SendAddEditPage(Options));
                    break;
            }
        }
    }
}
