﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Pages.Accounts;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.Domain;
using Bit.Core.Utilities;
using Xamarin.CommunityToolkit.ObjectModel;
using Xamarin.Forms;
using Xamarin.CommunityToolkit.ObjectModel;

namespace Bit.App.Pages
{
    public class SettingsPageViewModel : BaseViewModel
    {
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly ICryptoService _cryptoService;
        private readonly IStateService _stateService;
        private readonly IDeviceActionService _deviceActionService;
        private readonly IEnvironmentService _environmentService;
        private readonly IMessagingService _messagingService;
        private readonly IVaultTimeoutService _vaultTimeoutService;
        private readonly ISyncService _syncService;
        private readonly IBiometricService _biometricService;
        private readonly IPolicyService _policyService;
        private readonly ILocalizeService _localizeService;
        private readonly IKeyConnectorService _keyConnectorService;
        private readonly IClipboardService _clipboardService;
        private readonly ILogger _loggerService;
        private readonly ICozyClientService _cozyClientService;
        private readonly II18nService _i18nService;

        private const int CustomVaultTimeoutValue = -100;

        private bool _supportsBiometric;
        private bool _pin;
        private bool _biometric;
        private bool _screenCaptureAllowed;
        private string _lastSyncDate;
        private string _vaultTimeoutDisplayValue;
        private string _vaultTimeoutActionDisplayValue;
        private bool _showChangeMasterPassword;
        private bool _reportLoggingEnabled;

        private List<KeyValuePair<string, int?>> _vaultTimeouts =
            new List<KeyValuePair<string, int?>>
            {
                new KeyValuePair<string, int?>(AppResources.Immediately, 0),
                new KeyValuePair<string, int?>(AppResources.OneMinute, 1),
                new KeyValuePair<string, int?>(AppResources.FiveMinutes, 5),
                new KeyValuePair<string, int?>(AppResources.FifteenMinutes, 15),
                new KeyValuePair<string, int?>(AppResources.ThirtyMinutes, 30),
                new KeyValuePair<string, int?>(AppResources.OneHour, 60),
                new KeyValuePair<string, int?>(AppResources.FourHours, 240),
                new KeyValuePair<string, int?>(AppResources.OnRestart, -1),
                new KeyValuePair<string, int?>(AppResources.Never, null),
                new KeyValuePair<string, int?>(AppResources.Custom, CustomVaultTimeoutValue),
            };
        private List<KeyValuePair<string, VaultTimeoutAction>> _vaultTimeoutActions =
            new List<KeyValuePair<string, VaultTimeoutAction>>
            {
                new KeyValuePair<string, VaultTimeoutAction>(AppResources.Lock, VaultTimeoutAction.Lock),
                new KeyValuePair<string, VaultTimeoutAction>(AppResources.LogOut, VaultTimeoutAction.Logout),
            };

        private Policy _vaultTimeoutPolicy;
        private int? _vaultTimeout;

        public SettingsPageViewModel()
        {
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _cryptoService = ServiceContainer.Resolve<ICryptoService>("cryptoService");
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _environmentService = ServiceContainer.Resolve<IEnvironmentService>("environmentService");
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _vaultTimeoutService = ServiceContainer.Resolve<IVaultTimeoutService>("vaultTimeoutService");
            _syncService = ServiceContainer.Resolve<ISyncService>("syncService");
            _biometricService = ServiceContainer.Resolve<IBiometricService>("biometricService");
            _policyService = ServiceContainer.Resolve<IPolicyService>("policyService");
            _localizeService = ServiceContainer.Resolve<ILocalizeService>("localizeService");
            _keyConnectorService = ServiceContainer.Resolve<IKeyConnectorService>("keyConnectorService");
            _clipboardService = ServiceContainer.Resolve<IClipboardService>("clipboardService");
            _loggerService = ServiceContainer.Resolve<ILogger>("logger");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");

            GroupedItems = new ObservableRangeCollection<ISettingsPageListItem>();
            PageTitle = AppResources.Settings;

            ExecuteSettingItemCommand = new AsyncCommand<SettingsPageListItem>(item => item.ExecuteAsync(), onException: _loggerService.Exception, allowsMultipleExecutions: false);
        }

        public ObservableRangeCollection<ISettingsPageListItem> GroupedItems { get; set; }

        public IAsyncCommand<SettingsPageListItem> ExecuteSettingItemCommand { get; }

        public async Task InitAsync()
        {
            _supportsBiometric = await _platformUtilsService.SupportsBiometricAsync();
            var lastSync = await _syncService.GetLastSyncAsync();
            if (lastSync != null)
            {
                lastSync = lastSync.Value.ToLocalTime();
                _lastSyncDate = string.Format("{0} {1}",
                    _localizeService.GetLocaleShortDate(lastSync.Value),
                    _localizeService.GetLocaleShortTime(lastSync.Value));
            }

            if (await _policyService.PolicyAppliesToUser(PolicyType.MaximumVaultTimeout))
            {
                _vaultTimeoutPolicy = (await _policyService.GetAll(PolicyType.MaximumVaultTimeout)).First();
                var minutes = _policyService.GetPolicyInt(_vaultTimeoutPolicy, "minutes").GetValueOrDefault();
                _vaultTimeouts = _vaultTimeouts.Where(t =>
                    t.Value <= minutes &&
                    (t.Value > 0 || t.Value == CustomVaultTimeoutValue) &&
                    t.Value != null).ToList();
            }

            _vaultTimeout = await _vaultTimeoutService.GetVaultTimeout();
            _vaultTimeoutDisplayValue = _vaultTimeouts.FirstOrDefault(o => o.Value == _vaultTimeout).Key;
            var action = await _stateService.GetVaultTimeoutActionAsync() ?? VaultTimeoutAction.Lock;
            _vaultTimeoutActionDisplayValue = _vaultTimeoutActions.FirstOrDefault(o => o.Value == action).Key;
            var pinSet = await _vaultTimeoutService.IsPinLockSetAsync();
            _pin = pinSet.Item1 || pinSet.Item2;
            _biometric = await _vaultTimeoutService.IsBiometricLockSetAsync();
            _screenCaptureAllowed = await _stateService.GetScreenCaptureAllowedAsync();

            if (_vaultTimeoutDisplayValue == null)
            {
                _vaultTimeoutDisplayValue = AppResources.Custom;
            }

            _showChangeMasterPassword = IncludeLinksWithSubscriptionInfo() &&
                !await _keyConnectorService.GetUsesKeyConnector();
            _reportLoggingEnabled = await _loggerService.IsEnabled();
            BuildList();
        }

        public async Task AboutAsync()
        {
            var debugText = string.Format("{0}: {1} ({2})", AppResources.Version,
                _platformUtilsService.GetApplicationVersion(), _deviceActionService.GetBuildNumber());

#if DEBUG
            var pushNotificationsRegistered = ServiceContainer.Resolve<IPushNotificationService>("pushNotificationService").IsRegisteredForPush;
            var pnServerRegDate = await _stateService.GetPushLastRegistrationDateAsync();
            var pnServerError = await _stateService.GetPushInstallationRegistrationErrorAsync();

            var pnServerRegDateMessage = default(DateTime) == pnServerRegDate ? "-" : $"{pnServerRegDate.GetValueOrDefault().ToShortDateString()}-{pnServerRegDate.GetValueOrDefault().ToShortTimeString()} UTC";
            var errorMessage = string.IsNullOrEmpty(pnServerError) ? string.Empty : $"Push Notifications Server Registration error: {pnServerError}";

            var text = string.Format("Based on Bitwarden Mobile © Bitwarden Inc. 2015-{0}\n\n{1}\nPush Notifications registered:{2}\nPush Notifications Server Last Date :{3}\n{4}", DateTime.Now.Year, debugText, pushNotificationsRegistered, pnServerRegDateMessage, errorMessage);
#else
            var text = string.Format("Based on Bitwarden Mobile © Bitwarden Inc. 2015-{0}\n\n{1}", DateTime.Now.Year, debugText);
#endif

            var copy = await _platformUtilsService.ShowDialogAsync(text, AppResources.CozyPass, AppResources.Copy,
                AppResources.Close);
            if (copy)
            {
                await _clipboardService.CopyTextAsync(debugText);
            }
        }

        public void Help()
        {
            // Cozy customization, set Custom Cozy url based on language
            /*
            _platformUtilsService.LaunchUri("https://bitwarden.com/help/");
            /*/
	        var frSupportURL = "https://support.cozy.io/category/378-gestionnaire-de-mots-de-passe";
	        var enSupportURL = "https://help.cozy.io/category/395-password-manager";
            var lang = _i18nService.Culture.TwoLetterISOLanguageName; 
            _platformUtilsService.LaunchUri(lang == "fr" ? frSupportURL : enSupportURL);
            //*/
        }

        public async Task FingerprintAsync()
        {
            List<string> fingerprint;
            try
            {
                fingerprint = await _cryptoService.GetFingerprintAsync(await _stateService.GetActiveUserIdAsync());
            }
            catch (Exception e) when (e.Message == "No public key available.")
            {
                return;
            }
            var phrase = string.Join("-", fingerprint);
            var text = string.Format("{0}:\n\n{1}", AppResources.YourAccountsFingerprint, phrase);
            var learnMore = await _platformUtilsService.ShowDialogAsync(text, AppResources.FingerprintPhrase,
                AppResources.LearnMore, AppResources.Close);
            if (learnMore)
            {
                _platformUtilsService.LaunchUri("https://bitwarden.com/help/fingerprint-phrase/");
            }
        }

        public void Rate()
        {
            _deviceActionService.RateApp();
        }

        public async void Import()
        {
            // Cozy customization, set Custom Cozy url
            /*
            _platformUtilsService.LaunchUri("https://bitwarden.com/help/import-data/");
            /*/
            var passwordsURL = await _cozyClientService.GetURLForApp("passwords", fragment: "/vault?action=import");
            _platformUtilsService.LaunchUri(passwordsURL);
            //*/
        }

        public void WebVault()
        {
            _platformUtilsService.LaunchUri(_environmentService.GetWebVaultUrl());
        }

        public async Task ShareAsync()
        {
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.LearnOrgConfirmation,
               AppResources.LearnOrg, AppResources.Yes, AppResources.Cancel);
            if (confirmed)
            {
                _platformUtilsService.LaunchUri("https://bitwarden.com/help/about-organizations/");
            }
        }

        public async Task TwoStepAsync()
        {
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.TwoStepLoginConfirmation,
                AppResources.TwoStepLogin, AppResources.Yes, AppResources.Cancel);
            if (confirmed)
            {
                // Cozy customization, set Custom Cozy url
                /*
                _platformUtilsService.LaunchUri($"{_environmentService.GetWebVaultUrl()}/#/settings");
                /*/
                var twoFAURL = await _cozyClientService.GetURLForApp("settings", fragment: "/profile");
                _platformUtilsService.LaunchUri(twoFAURL);
                //*/
            }
        }

        public async Task ChangePasswordAsync()
        {
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.ChangePasswordConfirmation,
                AppResources.ChangeMasterPassword, AppResources.Yes, AppResources.Cancel);
            if (confirmed)
            {
                _platformUtilsService.LaunchUri($"{_environmentService.GetWebVaultUrl()}/#/settings");
            }
        }

        // Cozy customization: Add Setting entry for purchasing premium membership
        //*
        public async Task PurchasePremiumMembershipAsync()
        {
            await Task.Run(() =>
            {
                var lang = _i18nService.Culture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";
                var uri = $"https://cozy.io/{lang}/pricing/";
                _platformUtilsService.LaunchUri(uri);
            });
        }
        //*/

        public async Task LogOutAsync()
        {
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.LogoutConfirmation,
                AppResources.LogOut, AppResources.Yes, AppResources.Cancel);
            if (confirmed)
            {
                _messagingService.Send("logout");
            }
        }

        public async Task LockAsync()
        {
            await _vaultTimeoutService.LockAsync(true, true);
        }

        public async Task VaultTimeoutAsync(bool promptOptions = true, int? newTimeout = 0)
        {
            var oldTimeout = _vaultTimeout;

            var options = _vaultTimeouts.Select(
                o => o.Key == _vaultTimeoutDisplayValue ? $"✓ {o.Key}" : o.Key).ToArray();
            if (promptOptions)
            {
                var selection = await Page.DisplayActionSheet(AppResources.VaultTimeout,
                    AppResources.Cancel, null, options);
                if (selection == null || selection == AppResources.Cancel)
                {
                    return;
                }
                var cleanSelection = selection.Replace("✓ ", string.Empty);
                var selectionOption = _vaultTimeouts.FirstOrDefault(o => o.Key == cleanSelection);

                // Check if the selected Timeout action is "Never" and if it's different from the previous selected value
                if (selectionOption.Value == null && selectionOption.Value != oldTimeout)
                {
                    var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.NeverLockWarning,
                        AppResources.Warning, AppResources.Yes, AppResources.Cancel);
                    if (!confirmed)
                    {
                        return;
                    }
                }
                _vaultTimeoutDisplayValue = selectionOption.Key;
                newTimeout = selectionOption.Value;
            }

            if (_vaultTimeoutPolicy != null)
            {
                var maximumTimeout = _policyService.GetPolicyInt(_vaultTimeoutPolicy, "minutes");

                if (newTimeout > maximumTimeout)
                {
                    await _platformUtilsService.ShowDialogAsync(AppResources.VaultTimeoutToLarge, AppResources.Warning);
                    var timeout = await _vaultTimeoutService.GetVaultTimeout();
                    _vaultTimeoutDisplayValue = _vaultTimeouts.FirstOrDefault(o => o.Value == timeout).Key ??
                                                AppResources.Custom;
                    return;
                }
            }

            await _vaultTimeoutService.SetVaultTimeoutOptionsAsync(newTimeout,
                GetVaultTimeoutActionFromKey(_vaultTimeoutActionDisplayValue));

            if (newTimeout != CustomVaultTimeoutValue)
            {
                _vaultTimeout = newTimeout;
            }
            if (oldTimeout != newTimeout)
            {
                await Device.InvokeOnMainThreadAsync(BuildList);
            }
        }

        public async Task LoggerReportingAsync()
        {
            var options = new[]
            {
                    CreateSelectableOption(AppResources.Yes, _reportLoggingEnabled),
                    CreateSelectableOption(AppResources.No, !_reportLoggingEnabled),
            };

            var selection = await Page.DisplayActionSheet(AppResources.SubmitCrashLogsDescription, AppResources.Cancel, null, options);

            if (selection == null || selection == AppResources.Cancel)
            {
                return;
            }

            await _loggerService.SetEnabled(CompareSelection(selection, AppResources.Yes));
            _reportLoggingEnabled = await _loggerService.IsEnabled();
            BuildList();
        }

        public async Task VaultTimeoutActionAsync()
        {
            var options = _vaultTimeoutActions.Select(o =>
                o.Key == _vaultTimeoutActionDisplayValue ? $"✓ {o.Key}" : o.Key).ToArray();
            var selection = await Page.DisplayActionSheet(AppResources.VaultTimeoutAction,
                AppResources.Cancel, null, options);
            if (selection == null || selection == AppResources.Cancel)
            {
                return;
            }
            var cleanSelection = selection.Replace("✓ ", string.Empty);
            if (cleanSelection == AppResources.LogOut)
            {
                var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.VaultTimeoutLogOutConfirmation,
                    AppResources.Warning, AppResources.Yes, AppResources.Cancel);
                if (!confirmed)
                {
                    // Reset to lock and continue process as if lock were selected
                    cleanSelection = AppResources.Lock;
                }
            }
            var selectionOption = _vaultTimeoutActions.FirstOrDefault(o => o.Key == cleanSelection);
            var changed = _vaultTimeoutActionDisplayValue != selectionOption.Key;
            _vaultTimeoutActionDisplayValue = selectionOption.Key;
            await _vaultTimeoutService.SetVaultTimeoutOptionsAsync(_vaultTimeout,
                selectionOption.Value);
            if (changed)
            {
                _messagingService.Send("vaultTimeoutActionChanged");
            }
            BuildList();
        }

        public async Task UpdatePinAsync()
        {
            _pin = !_pin;
            if (_pin)
            {
                var pin = await _deviceActionService.DisplayPromptAync(AppResources.EnterPIN,
                    AppResources.SetPINDescription, null, AppResources.Submit, AppResources.Cancel, true);
                if (!string.IsNullOrWhiteSpace(pin))
                {
                    var masterPassOnRestart = false;
                    if (!await _keyConnectorService.GetUsesKeyConnector())
                    {
                        masterPassOnRestart = await _platformUtilsService.ShowDialogAsync(
                            AppResources.PINRequireMasterPasswordRestart, AppResources.UnlockWithPIN,
                            AppResources.Yes, AppResources.No);
                    }

                    var kdf = await _stateService.GetKdfTypeAsync();
                    var kdfIterations = await _stateService.GetKdfIterationsAsync();
                    var email = await _stateService.GetEmailAsync();
                    var pinKey = await _cryptoService.MakePinKeyAysnc(pin, email,
                        kdf.GetValueOrDefault(Core.Enums.KdfType.PBKDF2_SHA256),
                        kdfIterations.GetValueOrDefault(5000));
                    var key = await _cryptoService.GetKeyAsync();
                    var pinProtectedKey = await _cryptoService.EncryptAsync(key.Key, pinKey);

                    if (masterPassOnRestart)
                    {
                        var encPin = await _cryptoService.EncryptAsync(pin);
                        await _stateService.SetProtectedPinAsync(encPin.EncryptedString);
                        await _stateService.SetPinProtectedKeyAsync(pinProtectedKey);
                    }
                    else
                    {
                        await _stateService.SetPinProtectedAsync(pinProtectedKey.EncryptedString);
                    }
                }
                else
                {
                    _pin = false;
                }
            }
            if (!_pin)
            {
                await _cryptoService.ClearPinProtectedKeyAsync();
                await _vaultTimeoutService.ClearAsync();
            }
            BuildList();
        }

        public async Task UpdateBiometricAsync()
        {
            var current = _biometric;
            if (_biometric)
            {
                _biometric = false;
            }
            else if (await _platformUtilsService.SupportsBiometricAsync())
            {
                _biometric = await _platformUtilsService.AuthenticateBiometricAsync(null,
                    _deviceActionService.DeviceType == Core.Enums.DeviceType.Android ? "." : null);
            }
            if (_biometric == current)
            {
                return;
            }
            if (_biometric)
            {
                await _biometricService.SetupBiometricAsync();
                await _stateService.SetBiometricUnlockAsync(true);
            }
            else
            {
                await _stateService.SetBiometricUnlockAsync(null);
            }
            await _stateService.SetBiometricLockedAsync(false);
            await _cryptoService.ToggleKeyAsync();
            BuildList();
        }

        public void BuildList()
        {
            //TODO: Refactor this once navigation is abstracted so that it doesn't depend on Page, e.g. Page.Navigation.PushModalAsync...

            var doUpper = Device.RuntimePlatform != Device.Android;
            var autofillItems = new List<SettingsPageListItem>();
            if (Device.RuntimePlatform == Device.Android)
            {
                autofillItems.Add(new SettingsPageListItem
                {
                    Name = AppResources.AutofillServices,
                    SubLabel = _deviceActionService.AutofillServicesEnabled() ? AppResources.On : AppResources.Off,
                    ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new AutofillServicesPage(Page as SettingsPage)))
                });
            }
            else
            {
                if (_deviceActionService.SystemMajorVersion() >= 12)
                {
                    autofillItems.Add(new SettingsPageListItem
                    {
                        Name = AppResources.PasswordAutofill,
                        ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new AutofillPage()))
                    });
                }
                autofillItems.Add(new SettingsPageListItem
                {
                    Name = AppResources.AppExtension,
                    ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new ExtensionPage()))
                });
            }
            var manageItems = new List<SettingsPageListItem>
            {
                // Cozy customization: we deactivate folder management
                /*
                new SettingsPageListItem
                {
                    Name = AppResources.Folders,
                    ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new FoldersPage()))
                },
                //*/
                new SettingsPageListItem
                {
                    Name = AppResources.Sync,
                    SubLabel = _lastSyncDate,
                    ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new SyncPage()))
                }
            };
            var securityItems = new List<SettingsPageListItem>
            {
                new SettingsPageListItem
                {
                    Name = AppResources.VaultTimeout,
                    SubLabel = _vaultTimeoutDisplayValue,
                    ExecuteAsync = () => VaultTimeoutAsync() },
                new SettingsPageListItem
                {
                    Name = AppResources.VaultTimeoutAction,
                    SubLabel = _vaultTimeoutActionDisplayValue,
                    ExecuteAsync = () => VaultTimeoutActionAsync()
                },
                new SettingsPageListItem
                {
                    Name = AppResources.UnlockWithPIN,
                    SubLabel = _pin ? AppResources.On : AppResources.Off,
                    ExecuteAsync = () => UpdatePinAsync()
                },
                new SettingsPageListItem
                {
                    Name = AppResources.LockNow,
                    ExecuteAsync = () => LockAsync()
                },
                new SettingsPageListItem
                {
                    Name = AppResources.TwoStepLogin,
                    ExecuteAsync = () => TwoStepAsync()
                },
                // Cozy customization: Add Setting entry for purchasing premium membership
                //*
                new SettingsPageListItem
                {
                    Name = AppResources.PurchasePremiumMembership,
                    ExecuteAsync = () => PurchasePremiumMembershipAsync()
                }
                //*/
            };
            if (_supportsBiometric || _biometric)
            {
                var biometricName = AppResources.Biometrics;
                if (Device.RuntimePlatform == Device.iOS)
                {
                    biometricName = _deviceActionService.SupportsFaceBiometric() ? AppResources.FaceID :
                        AppResources.TouchID;
                }
                var item = new SettingsPageListItem
                {
                    Name = string.Format(AppResources.UnlockWith, biometricName),
                    SubLabel = _biometric ? AppResources.On : AppResources.Off,
                    ExecuteAsync = () => UpdateBiometricAsync()
                };
                securityItems.Insert(2, item);
            }
            if (_vaultTimeoutDisplayValue == AppResources.Custom)
            {
                securityItems.Insert(1, new SettingsPageListItem
                {
                    Name = AppResources.Custom,
                    Time = TimeSpan.FromMinutes(Math.Abs((double)_vaultTimeout.GetValueOrDefault())),
                });
            }
            if (_vaultTimeoutPolicy != null)
            {
                var maximumTimeout = _policyService.GetPolicyInt(_vaultTimeoutPolicy, "minutes").GetValueOrDefault();
                securityItems.Insert(0, new SettingsPageListItem
                {
                    Name = string.Format(AppResources.VaultTimeoutPolicyInEffect,
                        Math.Floor((float)maximumTimeout / 60),
                        maximumTimeout % 60),
                    UseFrame = true,
                });
            }
            if (Device.RuntimePlatform == Device.Android)
            {
                securityItems.Add(new SettingsPageListItem
                {
                    Name = AppResources.AllowScreenCapture,
                    SubLabel = _screenCaptureAllowed ? AppResources.On : AppResources.Off,
                    ExecuteAsync = () => SetScreenCaptureAllowedAsync()
                });
            }
            var accountItems = new List<SettingsPageListItem>
            {
                // Cozy customization: we deactivate
                // - fingerprint phrase functionality
                /*
                new SettingsPageListItem
                {
                    Name = AppResources.FingerprintPhrase,
                    ExecuteAsync = () => FingerprintAsync()
                },
                //*/
                new SettingsPageListItem
                {
                    Name = AppResources.LogOut,
                    ExecuteAsync = () => LogOutAsync()
                }
            };
            if (_showChangeMasterPassword)
            {
                accountItems.Insert(0, new SettingsPageListItem
                {
                    Name = AppResources.ChangeMasterPassword,
                    ExecuteAsync = () => ChangePasswordAsync()
                });
            }
            var toolsItems = new List<SettingsPageListItem>
            {
                new SettingsPageListItem
                {
                    Name = AppResources.ImportItems,
                    ExecuteAsync = () => Device.InvokeOnMainThreadAsync(() => Import())
                },
                new SettingsPageListItem
                {
                    Name = AppResources.ExportVault,
                    ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new ExportVaultPage()))
                }
            };
            if (IncludeLinksWithSubscriptionInfo())
            {
                toolsItems.Add(new SettingsPageListItem
                {
                    Name = AppResources.LearnOrg,
                    ExecuteAsync = () => ShareAsync()
                });
                toolsItems.Add(new SettingsPageListItem
                {
                    Name = AppResources.WebVault,
                    ExecuteAsync = () => Device.InvokeOnMainThreadAsync(() => WebVault())
                });
            }

            var otherItems = new List<SettingsPageListItem>
            {
                new SettingsPageListItem
                {
                    Name = AppResources.Options,
                    ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new OptionsPage()))
                },
                new SettingsPageListItem
                {
                    Name = AppResources.About,
                    ExecuteAsync = () => AboutAsync()
                },
                new SettingsPageListItem
                {
                    // Cozy customization: We don't want to mention Feedback here as they are handle by another scenario at Cozy 
                    /*
                    Name = AppResources.HelpAndFeedback,
                    /*/
                    Name = AppResources.Help,
                    //*/
                    ExecuteAsync = () => Device.InvokeOnMainThreadAsync(() => Help())
                },
#if !FDROID 
                new SettingsPageListItem
                {
                    Name = AppResources.SubmitCrashLogs,
                    SubLabel = _reportLoggingEnabled ? AppResources.On : AppResources.Off,
                    ExecuteAsync = () => LoggerReportingAsync()
                },
#endif
                new SettingsPageListItem
                {
                    Name = AppResources.RateTheApp,
                    ExecuteAsync = () => Device.InvokeOnMainThreadAsync(() => Rate())
                },
                new SettingsPageListItem
                {
                    Name = AppResources.DeleteAccount,
                    ExecuteAsync = () => Page.Navigation.PushModalAsync(new NavigationPage(new DeleteAccountPage()))
                }
            };

            // TODO: improve this. Leaving this as is to reduce error possibility on the hotfix.
            var settingsListGroupItems = new List<SettingsPageListGroup>()
            {
                new SettingsPageListGroup(autofillItems, AppResources.Autofill, doUpper, true),
                new SettingsPageListGroup(manageItems, AppResources.Manage, doUpper),
                new SettingsPageListGroup(securityItems, AppResources.Security, doUpper),
                new SettingsPageListGroup(accountItems, AppResources.Account, doUpper),
                new SettingsPageListGroup(toolsItems, AppResources.Tools, doUpper),
                new SettingsPageListGroup(otherItems, AppResources.Other, doUpper)
            };

            // TODO: refactor this
            if (Device.RuntimePlatform == Device.Android
                ||
                GroupedItems.Any())
            {
                var items = new List<ISettingsPageListItem>();
                foreach (var itemGroup in settingsListGroupItems)
                {
                    items.Add(new SettingsPageHeaderListItem(itemGroup.Name));
                    items.AddRange(itemGroup);
                }

                GroupedItems.ReplaceRange(items);
            }
            else
            {
                // HACK: we need this on iOS, so that it doesn't crash when adding coming from an empty list
                var first = true;
                var items = new List<ISettingsPageListItem>();
                foreach (var itemGroup in settingsListGroupItems)
                {
                    if (!first)
                    {
                        items.Add(new SettingsPageHeaderListItem(itemGroup.Name));
                    }
                    else
                    {
                        first = false;
                    }
                    items.AddRange(itemGroup);
                }

                if (settingsListGroupItems.Any())
                {
                    GroupedItems.ReplaceRange(new List<ISettingsPageListItem> { new SettingsPageHeaderListItem(settingsListGroupItems[0].Name) });
                    GroupedItems.AddRange(items);
                }
                else
                {
                    GroupedItems.Clear();
                }
            }
        }

        private bool IncludeLinksWithSubscriptionInfo()
        {
            if (Device.RuntimePlatform == Device.iOS)
            {
                return false;
            }
            return true;
        }

        private VaultTimeoutAction GetVaultTimeoutActionFromKey(string key)
        {
            return _vaultTimeoutActions.FirstOrDefault(o => o.Key == key).Value;
        }

        private int? GetVaultTimeoutFromKey(string key)
        {
            return _vaultTimeouts.FirstOrDefault(o => o.Key == key).Value;
        }

        private string CreateSelectableOption(string option, bool selected) => selected ? $"✓ {option}" : option;

        private bool CompareSelection(string selection, string compareTo) => selection == compareTo || selection == $"✓ {compareTo}";

        public async Task SetScreenCaptureAllowedAsync()
        {
            if (CoreHelpers.ForceScreenCaptureEnabled())
            {
                return;
            }

            try
            {
                if (!_screenCaptureAllowed
                    &&
                    !await Page.DisplayAlert(AppResources.AllowScreenCapture, AppResources.AreYouSureYouWantToEnableScreenCapture, AppResources.Yes, AppResources.No))
                {
                    return;
                }

                await _stateService.SetScreenCaptureAllowedAsync(!_screenCaptureAllowed);
                _screenCaptureAllowed = !_screenCaptureAllowed;
                await _deviceActionService.SetScreenCaptureAllowedAsync();
                BuildList();
            }
            catch (Exception ex)
            {
                _loggerService.Exception(ex);
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred, AppResources.GenericErrorMessage, AppResources.Ok);
            }
        }
    }
}
