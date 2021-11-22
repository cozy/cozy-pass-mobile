﻿using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Domain;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class SettingsPageViewModel : BaseViewModel
    {
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly ICryptoService _cryptoService;
        private readonly IUserService _userService;
        private readonly IDeviceActionService _deviceActionService;
        private readonly IEnvironmentService _environmentService;
        private readonly IMessagingService _messagingService;
        private readonly IVaultTimeoutService _vaultTimeoutService;
        private readonly IStorageService _storageService;
        private readonly ISyncService _syncService;
        private readonly IBiometricService _biometricService;
        private readonly IPolicyService _policyService;
        private readonly ICozyClientService _cozyClientService;
        private readonly II18nService _i18nService;

        private const int CustomVaultTimeoutValue = -100;

        private bool _supportsBiometric;
        private bool _pin;
        private bool _biometric;
        private string _lastSyncDate;
        private string _vaultTimeoutDisplayValue;
        private string _vaultTimeoutActionDisplayValue;
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
        private List<KeyValuePair<string, string>> _vaultTimeoutActions =
            new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(AppResources.Lock, "lock"),
                new KeyValuePair<string, string>(AppResources.LogOut, "logOut"),
            };

        private Policy _vaultTimeoutPolicy;
        private int _vaultTimeout;

        public SettingsPageViewModel()
        {
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _cryptoService = ServiceContainer.Resolve<ICryptoService>("cryptoService");
            _userService = ServiceContainer.Resolve<IUserService>("userService");
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _environmentService = ServiceContainer.Resolve<IEnvironmentService>("environmentService");
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _vaultTimeoutService = ServiceContainer.Resolve<IVaultTimeoutService>("vaultTimeoutService");
            _storageService = ServiceContainer.Resolve<IStorageService>("storageService");
            _syncService = ServiceContainer.Resolve<ISyncService>("syncService");
            _biometricService = ServiceContainer.Resolve<IBiometricService>("biometricService");
            _policyService = ServiceContainer.Resolve<IPolicyService>("policyService");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");

            GroupedItems = new ExtendedObservableCollection<SettingsPageListGroup>();
            PageTitle = AppResources.Settings;
        }

        public ExtendedObservableCollection<SettingsPageListGroup> GroupedItems { get; set; }

        public async Task InitAsync()
        {
            _supportsBiometric = await _platformUtilsService.SupportsBiometricAsync();
            var lastSync = await _syncService.GetLastSyncAsync();
            if (lastSync != null)
            {
                lastSync = lastSync.Value.ToLocalTime();
                _lastSyncDate = string.Format("{0} {1}", lastSync.Value.ToShortDateString(),
                    lastSync.Value.ToShortTimeString());
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
            var action = await _storageService.GetAsync<string>(Constants.VaultTimeoutActionKey) ?? "lock";
            _vaultTimeoutActionDisplayValue = _vaultTimeoutActions.FirstOrDefault(o => o.Value == action).Key;
            var pinSet = await _vaultTimeoutService.IsPinLockSetAsync();
            _pin = pinSet.Item1 || pinSet.Item2;
            _biometric = await _vaultTimeoutService.IsBiometricLockSetAsync();

            if (_vaultTimeoutDisplayValue == null)
            {
                _vaultTimeoutDisplayValue = AppResources.Custom;
            }

            BuildList();
        }

        public async Task AboutAsync()
        {
            var debugText = string.Format("{0}: {1} ({2})", AppResources.Version,
                _platformUtilsService.GetApplicationVersion(), _deviceActionService.GetBuildNumber());
            var text = string.Format("Based on Bitwarden Mobile © 8bit Solutions LLC 2015-{0}\n\n{1}", DateTime.Now.Year, debugText);
            var copy = await _platformUtilsService.ShowDialogAsync(text, AppResources.CozyPass, AppResources.Copy,
                AppResources.Close);
            if (copy)
            {
                await _platformUtilsService.CopyToClipboardAsync(debugText);
            }
        }

        public void Help()
        {  
	    var frSupportURL = "https://support.cozy.io/category/378-gestionnaire-de-mots-de-passe";
	    var enSupportURL = "https://help.cozy.io/category/395-password-manager";
	    var lang = _i18nService.Culture.TwoLetterISOLanguageName; 
            _platformUtilsService.LaunchUri(lang == "fr" ? frSupportURL : enSupportURL);
        }

        public async Task FingerprintAsync()
        {
            List<string> fingerprint;
            try
            {
                fingerprint = await _cryptoService.GetFingerprintAsync(await _userService.GetUserIdAsync());
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
                _platformUtilsService.LaunchUri("https://help.bitwarden.com/article/fingerprint-phrase/");
            }
        }

        public void Rate()
        {
            _deviceActionService.RateApp();
        }

        public async void Import()
        {
            var passwordsURL = await _cozyClientService.GetURLForApp("passwords", fragment: "/vault?action=import");
            _platformUtilsService.LaunchUri(passwordsURL);
        }

        public void WebVault()
        {
            var url = _environmentService.GetWebVaultUrl();
            if (url == null)
            {
                url = "https://vault.bitwarden.com";
            }
            _platformUtilsService.LaunchUri(url);
        }

        public async Task ShareAsync()
        {
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.LearnOrgConfirmation,
               AppResources.LearnOrg, AppResources.Yes, AppResources.Cancel);
            if (confirmed)
            {
                _platformUtilsService.LaunchUri("https://help.bitwarden.com/article/what-is-an-organization/");
            }
        }

        public async Task TwoStepAsync()
        {
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.TwoStepLoginConfirmation,
                AppResources.TwoStepLogin, AppResources.Yes, AppResources.Cancel);
            if (confirmed)
            {
                var twoFAURL = await _cozyClientService.GetURLForApp("settings", fragment: "/profile");
                _platformUtilsService.LaunchUri(twoFAURL);
            }
        }

        public async Task ChangePasswordAsync()
        {
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.ChangePasswordConfirmation,
                AppResources.ChangeMasterPassword, AppResources.Yes, AppResources.Cancel);
            if (confirmed)
            {
                _platformUtilsService.LaunchUri("https://help.bitwarden.com/article/change-your-master-password/");
            }
        }

        public void PurchasePremiumMembership()
        {
            var lang = _i18nService.Culture.TwoLetterISOLanguageName == "fr" ? "fr" : "en";
            var uri = $"https://cozy.io/{lang}/pricing/";
            _platformUtilsService.LaunchUri(uri);
        }

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

        public async Task VaultTimeoutAsync(bool promptOptions = true, int newTimeout = 0)
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
                _vaultTimeoutDisplayValue = selectionOption.Key;
                newTimeout = selectionOption.Value.GetValueOrDefault();
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
                    var masterPassOnRestart = await _platformUtilsService.ShowDialogAsync(
                       AppResources.PINRequireMasterPasswordRestart, AppResources.UnlockWithPIN,
                       AppResources.Yes, AppResources.No);

                    var kdf = await _userService.GetKdfAsync();
                    var kdfIterations = await _userService.GetKdfIterationsAsync();
                    var email = await _userService.GetEmailAsync();
                    var pinKey = await _cryptoService.MakePinKeyAysnc(pin, email,
                        kdf.GetValueOrDefault(Core.Enums.KdfType.PBKDF2_SHA256),
                        kdfIterations.GetValueOrDefault(5000));
                    var key = await _cryptoService.GetKeyAsync();
                    var pinProtectedKey = await _cryptoService.EncryptAsync(key.Key, pinKey);

                    if (masterPassOnRestart)
                    {
                        var encPin = await _cryptoService.EncryptAsync(pin);
                        await _storageService.SaveAsync(Constants.ProtectedPin, encPin.EncryptedString);
                        _vaultTimeoutService.PinProtectedKey = pinProtectedKey;
                    }
                    else
                    {
                        await _storageService.SaveAsync(Constants.PinProtectedKey, pinProtectedKey.EncryptedString);
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
                await _storageService.SaveAsync(Constants.BiometricUnlockKey, true);
            }
            else
            {
                await _storageService.RemoveAsync(Constants.BiometricUnlockKey);
            }
            _vaultTimeoutService.BiometricLocked = false;
            await _cryptoService.ToggleKeyAsync();
            BuildList();
        }

        public void BuildList()
        {
            var doUpper = Device.RuntimePlatform != Device.Android;
            var autofillItems = new List<SettingsPageListItem>();
            if (Device.RuntimePlatform == Device.Android)
            {
                autofillItems.Add(new SettingsPageListItem
                {
                    Name = AppResources.AutofillServices,
                    SubLabel = _deviceActionService.AutofillServicesEnabled() ?
                        AppResources.Enabled : AppResources.Disabled
                });
            }
            else
            {
                if (_deviceActionService.SystemMajorVersion() >= 12)
                {
                    autofillItems.Add(new SettingsPageListItem { Name = AppResources.PasswordAutofill });
                }
                autofillItems.Add(new SettingsPageListItem { Name = AppResources.AppExtension });
            }
            var manageItems = new List<SettingsPageListItem>
            {
                // Cozy customization: we deactivate folder management
                // new SettingsPageListItem { Name = AppResources.Folders },
                new SettingsPageListItem { Name = AppResources.Sync, SubLabel = _lastSyncDate }
            };
            var securityItems = new List<SettingsPageListItem>
            {
                new SettingsPageListItem { Name = AppResources.VaultTimeout, SubLabel = _vaultTimeoutDisplayValue },
                new SettingsPageListItem 
                {
                    Name = AppResources.VaultTimeoutAction,
                    SubLabel = _vaultTimeoutActionDisplayValue
                },
                new SettingsPageListItem
                {
                    Name = AppResources.UnlockWithPIN,
                    SubLabel = _pin ? AppResources.Enabled : AppResources.Disabled
                },
                new SettingsPageListItem { Name = AppResources.LockNow },
                new SettingsPageListItem { Name = AppResources.TwoStepLogin }
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
                    SubLabel = _biometric ? AppResources.Enabled : AppResources.Disabled
                };
                securityItems.Insert(2, item);
            }
            if (_vaultTimeoutDisplayValue == AppResources.Custom)
            {
                securityItems.Insert(1, new SettingsPageListItem
                {
                    Name = AppResources.Custom,
                    Time = TimeSpan.FromMinutes(Math.Abs((double)_vaultTimeout)),
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
            var accountItems = new List<SettingsPageListItem>
            {
                // Cozy customization: we deactivate
                // - fingerprint phrase functionality
                //new SettingsPageListItem { Name = AppResources.FingerprintPhrase },
                new SettingsPageListItem { Name = AppResources.LogOut }
            };
            if (IncludeLinksWithSubscriptionInfo())
            {
                accountItems.Insert(0, new SettingsPageListItem { Name = AppResources.ChangeMasterPassword });
            }
            var toolsItems = new List<SettingsPageListItem>
            {
                new SettingsPageListItem { Name = AppResources.ImportItems },
                new SettingsPageListItem { Name = AppResources.ExportVault }
            };
            if (IncludeLinksWithSubscriptionInfo())
            {
                toolsItems.Add(new SettingsPageListItem { Name = AppResources.LearnOrg });
                toolsItems.Add(new SettingsPageListItem { Name = AppResources.WebVault });
            }
            var otherItems = new List<SettingsPageListItem>
            {
                new SettingsPageListItem { Name = AppResources.Options },
                new SettingsPageListItem { Name = AppResources.About },
                new SettingsPageListItem { Name = AppResources.Help },
                new SettingsPageListItem { Name = AppResources.RateTheApp }
            };
            GroupedItems.ResetWithRange(new List<SettingsPageListGroup>
            {
                new SettingsPageListGroup(autofillItems, AppResources.Autofill, doUpper, true),
                new SettingsPageListGroup(manageItems, AppResources.Manage, doUpper),
                new SettingsPageListGroup(securityItems, AppResources.Security, doUpper),
                new SettingsPageListGroup(accountItems, AppResources.Account, doUpper),
                new SettingsPageListGroup(toolsItems, AppResources.Tools, doUpper),
                new SettingsPageListGroup(otherItems, AppResources.Other, doUpper)
            });
        }

        private bool IncludeLinksWithSubscriptionInfo()
        {
            if (Device.RuntimePlatform == Device.iOS)
            {
                return false;
            }
            return true;
        }

        private string GetVaultTimeoutActionFromKey(string key)
        {
            return _vaultTimeoutActions.FirstOrDefault(o => o.Key == key).Value;
        }

        private int? GetVaultTimeoutFromKey(string key)
        {
            return _vaultTimeouts.FirstOrDefault(o => o.Key == key).Value;
        }
    }
}
