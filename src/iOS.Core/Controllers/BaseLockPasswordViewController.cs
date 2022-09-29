﻿using System;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Models;
using Bit.App.Pages;
using Bit.App.Resources;
using Bit.App.Utilities;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.Domain;
using Bit.Core.Utilities;
using Bit.iOS.Core.Utilities;
using Bit.iOS.Core.Views;
using Foundation;
using UIKit;
using Xamarin.Forms;

namespace Bit.iOS.Core.Controllers
{
    public abstract class BaseLockPasswordViewController : ExtendedUIViewController
    {
        private IVaultTimeoutService _vaultTimeoutService;
        private ICryptoService _cryptoService;
        private IDeviceActionService _deviceActionService;
        private IStateService _stateService;
        private IStorageService _secureStorageService;
        private IPlatformUtilsService _platformUtilsService;
        private IBiometricService _biometricService;
        private IKeyConnectorService _keyConnectorService;
        private IAccountsManager _accountManager;
        private bool _isPinProtected;
        private bool _isPinProtectedWithKey;
        private bool _pinLock;
        private bool _biometricLock;
        private bool _biometricIntegrityValid = true;
        private bool _passwordReprompt = false;
        private bool _usesKeyConnector;
        private bool _biometricUnlockOnly = false;

        protected bool autofillExtension = false;

        public BaseLockPasswordViewController()
        {
        }

        public BaseLockPasswordViewController(IntPtr handle)
            : base(handle)
        { }

        public abstract UINavigationItem BaseNavItem { get; }
        public abstract UIBarButtonItem BaseCancelButton { get; }
        public abstract UIBarButtonItem BaseSubmitButton { get; }
        public abstract Action Success { get; }
        public abstract Action Cancel { get; }

        public FormEntryTableViewCell MasterPasswordCell { get; set; } = new FormEntryTableViewCell(
            AppResources.MasterPassword, useButton: true);

        public string BiometricIntegrityKey { get; set; }

        public UITableViewCell BiometricCell
        {
            get
            {
                var cell = new UITableViewCell();
                cell.BackgroundColor = ThemeHelpers.BackgroundColor;
                if (_biometricIntegrityValid)
                {
                    var biometricButtonText = _deviceActionService.SupportsFaceBiometric() ?
                    AppResources.UseFaceIDToUnlock : AppResources.UseFingerprintToUnlock;
                    cell.TextLabel.TextColor = ThemeHelpers.PrimaryColor;
                    cell.TextLabel.Text = biometricButtonText;
                }
                else
                {
                    cell.TextLabel.TextColor = ThemeHelpers.DangerColor;
                    cell.TextLabel.Font = ThemeHelpers.GetDangerFont();
                    cell.TextLabel.Lines = 0;
                    cell.TextLabel.LineBreakMode = UILineBreakMode.WordWrap;
                    cell.TextLabel.Text = AppResources.BiometricInvalidatedExtension;
                }
                return cell;
            }
        }

        public abstract UITableView TableView { get; }
        
        public override async void ViewDidLoad()
        {
            _vaultTimeoutService = ServiceContainer.Resolve<IVaultTimeoutService>("vaultTimeoutService");
            _cryptoService = ServiceContainer.Resolve<ICryptoService>("cryptoService");
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");
            _secureStorageService = ServiceContainer.Resolve<IStorageService>("secureStorageService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _biometricService = ServiceContainer.Resolve<IBiometricService>("biometricService");
            _keyConnectorService = ServiceContainer.Resolve<IKeyConnectorService>("keyConnectorService");
            _accountManager = ServiceContainer.Resolve<IAccountsManager>("accountsManager");

            // We re-use the lock screen for autofill extension to verify master password
            // when trying to access protected items.
            if (autofillExtension && await _stateService.GetPasswordRepromptAutofillAsync())
            {
                _passwordReprompt = true;
                _isPinProtected = false;
                _isPinProtectedWithKey = false;
                _pinLock = false;
                _biometricLock = false;
            }
            else
            {
                (_isPinProtected, _isPinProtectedWithKey) = await _vaultTimeoutService.IsPinLockSetAsync();
                _pinLock = (_isPinProtected && await _stateService.GetPinProtectedKeyAsync() != null) ||
                           _isPinProtectedWithKey;
                _biometricLock = await _vaultTimeoutService.IsBiometricLockSetAsync() &&
                                 await _cryptoService.HasKeyAsync();
                _biometricIntegrityValid = await _biometricService.ValidateIntegrityAsync(BiometricIntegrityKey);
                _usesKeyConnector = await _keyConnectorService.GetUsesKeyConnector();
                _biometricUnlockOnly = _usesKeyConnector && _biometricLock && !_pinLock;
            }

            if (_pinLock)
            {
                BaseNavItem.Title = AppResources.VerifyPIN;
            }
            else if (_usesKeyConnector)
            {
                BaseNavItem.Title = AppResources.UnlockVault;
            }
            else
            {
                BaseNavItem.Title = AppResources.VerifyMasterPassword;
            }

            BaseCancelButton.Title = AppResources.Cancel;

            if (_biometricUnlockOnly)
            {
                BaseSubmitButton.Title = null;
                BaseSubmitButton.Enabled = false;
            }
            else
            {
                BaseSubmitButton.Title = AppResources.Submit;
            }

            var descriptor = UIFontDescriptor.PreferredBody;

            if (!_biometricUnlockOnly)
            {
                MasterPasswordCell.Label.Text = _pinLock ? AppResources.PIN : AppResources.MasterPassword;
                MasterPasswordCell.TextField.SecureTextEntry = true;
                MasterPasswordCell.TextField.ReturnKeyType = UIReturnKeyType.Go;
                MasterPasswordCell.TextField.ShouldReturn += (UITextField tf) =>
                {
                    CheckPasswordAsync().GetAwaiter().GetResult();
                    return true;
                };
                if (_pinLock)
                {
                    MasterPasswordCell.TextField.KeyboardType = UIKeyboardType.NumberPad;
                }
                MasterPasswordCell.Button.TitleLabel.Font = UIFont.FromName("bwi-font", 28f);
                MasterPasswordCell.Button.SetTitle(BitwardenIcons.Eye, UIControlState.Normal);
                MasterPasswordCell.Button.TouchUpInside += (sender, e) =>
                {
                    MasterPasswordCell.TextField.SecureTextEntry = !MasterPasswordCell.TextField.SecureTextEntry;
                    MasterPasswordCell.Button.SetTitle(MasterPasswordCell.TextField.SecureTextEntry ? BitwardenIcons.Eye : BitwardenIcons.EyeSlash, UIControlState.Normal);
                };
            }

            if (TableView != null)
            {
                TableView.BackgroundColor = ThemeHelpers.BackgroundColor;
                TableView.SeparatorColor = ThemeHelpers.SeparatorColor;
                TableView.RowHeight = UITableView.AutomaticDimension;
                TableView.EstimatedRowHeight = 70;
                TableView.Source = new TableSource(this);
                TableView.AllowsSelection = true;
            }

            base.ViewDidLoad();

            if (_biometricLock)
            {
                if (!_biometricIntegrityValid)
                {
                    return;
                }
                var tasks = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    NSRunLoop.Main.BeginInvokeOnMainThread(async () => await PromptBiometricAsync());
                });
            }
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            // Users with key connector and without biometric or pin has no MP to unlock with
            if (_usesKeyConnector)
            {
                if (!(_pinLock || _biometricLock) ||
                    (_biometricLock && !_biometricIntegrityValid))
                {
                    PromptSSO();
                }
            }
            else if (!_biometricLock || !_biometricIntegrityValid)
            {
                MasterPasswordCell.TextField.BecomeFirstResponder();
            }
        }

        protected async Task CheckPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(MasterPasswordCell.TextField.Text))
            {
                var alert = Dialogs.CreateAlert(AppResources.AnErrorHasOccurred,
                    string.Format(AppResources.ValidationFieldRequired,
                        _pinLock ? AppResources.PIN : AppResources.MasterPassword),
                    AppResources.Ok);
                PresentViewController(alert, true, null);
                return;
            }

            var email = await _stateService.GetEmailAsync();
            var kdf = await _stateService.GetKdfTypeAsync();
            var kdfIterations = await _stateService.GetKdfIterationsAsync();
            var inputtedValue = MasterPasswordCell.TextField.Text;

            if (_pinLock)
            {
                var failed = true;
                try
                {
                    if (_isPinProtected)
                    {
                        var key = await _cryptoService.MakeKeyFromPinAsync(inputtedValue, email,
                            kdf.GetValueOrDefault(KdfType.PBKDF2_SHA256), kdfIterations.GetValueOrDefault(5000),
                            await _stateService.GetPinProtectedKeyAsync());
                        var encKey = await _cryptoService.GetEncKeyAsync(key);
                        var protectedPin = await _stateService.GetProtectedPinAsync();
                        var decPin = await _cryptoService.DecryptToUtf8Async(new EncString(protectedPin), encKey);
                        failed = decPin != inputtedValue;
                        if (!failed)
                        {
                            await AppHelpers.ResetInvalidUnlockAttemptsAsync();
                            await SetKeyAndContinueAsync(key);
                        }
                    }
                    else
                    {
                        var key2 = await _cryptoService.MakeKeyFromPinAsync(inputtedValue, email,
                            kdf.GetValueOrDefault(KdfType.PBKDF2_SHA256), kdfIterations.GetValueOrDefault(5000));
                        failed = false;
                        await AppHelpers.ResetInvalidUnlockAttemptsAsync();
                        await SetKeyAndContinueAsync(key2);
                    }
                }
                catch
                {
                    failed = true;
                }
                if (failed)
                {
                    await HandleFailedCredentialsAsync();
                }
            }
            else
            {
                var key2 = await _cryptoService.MakeKeyAsync(inputtedValue, email, kdf, kdfIterations);

                var storedKeyHash = await _cryptoService.GetKeyHashAsync();
                if (storedKeyHash == null)
                {
                    var oldKey = await _secureStorageService.GetAsync<string>("oldKey");
                    if (key2.KeyB64 == oldKey)
                    {
                        var localKeyHash = await _cryptoService.HashPasswordAsync(inputtedValue, key2, HashPurpose.LocalAuthorization);
                        await _secureStorageService.RemoveAsync("oldKey");
                        await _cryptoService.SetKeyHashAsync(localKeyHash);
                    }
                }
                var passwordValid = await _cryptoService.CompareAndUpdateKeyHashAsync(inputtedValue, key2);
                if (passwordValid)
                {
                    if (_isPinProtected)
                    {
                        var protectedPin = await _stateService.GetProtectedPinAsync();
                        var encKey = await _cryptoService.GetEncKeyAsync(key2);
                        var decPin = await _cryptoService.DecryptToUtf8Async(new EncString(protectedPin), encKey);
                        var pinKey = await _cryptoService.MakePinKeyAysnc(decPin, email,
                            kdf.GetValueOrDefault(KdfType.PBKDF2_SHA256), kdfIterations.GetValueOrDefault(5000));
                        await _stateService.SetPinProtectedKeyAsync(await _cryptoService.EncryptAsync(key2.Key, pinKey));
                    }
                    await AppHelpers.ResetInvalidUnlockAttemptsAsync();
                    await SetKeyAndContinueAsync(key2, true);
                }
                else
                {
                    await HandleFailedCredentialsAsync();
                }
            }
        }

        private async Task HandleFailedCredentialsAsync()
        {
            var invalidUnlockAttempts = await AppHelpers.IncrementInvalidUnlockAttemptsAsync();
            if (invalidUnlockAttempts >= 5)
            {
                await _accountManager.LogOutAsync(await _stateService.GetActiveUserIdAsync(), false, false);
                return;
            }
            InvalidValue();
        }

        public async Task PromptBiometricAsync()
        {
            if (!_biometricLock || !_biometricIntegrityValid)
            {
                return;
            }
            var success = await _platformUtilsService.AuthenticateBiometricAsync(null,
                _pinLock ? AppResources.PIN : AppResources.MasterPassword,
                () => MasterPasswordCell.TextField.BecomeFirstResponder());
            await _stateService.SetBiometricLockedAsync(!success);
            if (success)
            {
                DoContinue();
            }
        }

        public void PromptSSO()
        {
            var loginPage = new LoginSsoPage();
            var app = new App.App(new AppOptions { IosExtension = true });
            ThemeManager.SetTheme(app.Resources);
            ThemeManager.ApplyResourcesTo(loginPage);
            if (loginPage.BindingContext is LoginSsoPageViewModel vm)
            {
                vm.SsoAuthSuccessAction = () => DoContinue();
                vm.CloseAction = Cancel;
            }

            var navigationPage = new NavigationPage(loginPage);
            var loginController = navigationPage.CreateViewController();
            loginController.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
            PresentViewController(loginController, true, null);
        }

        private async Task SetKeyAndContinueAsync(SymmetricCryptoKey key, bool masterPassword = false)
        {
            var hasKey = await _cryptoService.HasKeyAsync();
            if (!hasKey)
            {
                await _cryptoService.SetKeyAsync(key);
            }
            DoContinue(masterPassword);
        }

        private async void DoContinue(bool masterPassword = false)
        {
            if (masterPassword)
            {
                await _stateService.SetPasswordVerifiedAutofillAsync(true);
            }
            await EnableBiometricsIfNeeded();
            await _stateService.SetBiometricLockedAsync(false);
            MasterPasswordCell.TextField.ResignFirstResponder();
            Success();
        }

        private async Task EnableBiometricsIfNeeded()
        {
            // Re-enable biometrics if initial use
            if (_biometricLock & !_biometricIntegrityValid)
            {
                await _biometricService.SetupBiometricAsync(BiometricIntegrityKey);
            }
        }

        private void InvalidValue()
        {
            var alert = Dialogs.CreateAlert(AppResources.AnErrorHasOccurred,
                string.Format(null, _pinLock ? AppResources.PIN : AppResources.InvalidMasterPassword),
                AppResources.Ok, (a) =>
                {

                    MasterPasswordCell.TextField.Text = string.Empty;
                    MasterPasswordCell.TextField.BecomeFirstResponder();
                });
            PresentViewController(alert, true, null);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            MasterPasswordCell?.Dispose();
            MasterPasswordCell = null;

            TableView?.Dispose();
        }

        public class TableSource : ExtendedUITableViewSource
        {
            private readonly WeakReference<BaseLockPasswordViewController> _controller;

            public TableSource(BaseLockPasswordViewController controller)
            {
                _controller = new WeakReference<BaseLockPasswordViewController>(controller);
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                if (!_controller.TryGetTarget(out var controller))
                {
                    return new ExtendedUITableViewCell();
                }

                if (indexPath.Section == 0)
                {
                    if (indexPath.Row == 0)
                    {
                        if (controller._biometricUnlockOnly)
                        {
                            return controller.BiometricCell;
                        }
                        else
                        {
                            return controller.MasterPasswordCell;
                        }
                    }
                }
                else if (indexPath.Section == 1)
                {
                    if (indexPath.Row == 0)
                    {
                        if (controller._passwordReprompt)
                        {
                            var cell = new ExtendedUITableViewCell();
                            cell.TextLabel.TextColor = ThemeHelpers.DangerColor;
                            cell.TextLabel.Font = ThemeHelpers.GetDangerFont();
                            cell.TextLabel.Lines = 0;
                            cell.TextLabel.LineBreakMode = UILineBreakMode.WordWrap;
                            cell.TextLabel.Text = AppResources.PasswordConfirmationDesc;
                            return cell;
                        }
                        else if (!controller._biometricUnlockOnly)
                        {
                            return controller.BiometricCell;
                        }
                    }
                }
                return new ExtendedUITableViewCell();
            }

            public override nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
            {
                return UITableView.AutomaticDimension;
            }

            public override nint NumberOfSections(UITableView tableView)
            {
                if (!_controller.TryGetTarget(out var controller))
                {
                    return 0;
                }

                return (!controller._biometricUnlockOnly && controller._biometricLock) ||
                    controller._passwordReprompt
                    ? 2
                    : 1;
            }

            public override nint RowsInSection(UITableView tableview, nint section)
            {
                if (section <= 1)
                {
                    return 1;
                }
                return 0;
            }

            public override nfloat GetHeightForHeader(UITableView tableView, nint section)
            {
                return section == 1 ? 0.00001f : UITableView.AutomaticDimension;
            }

            public override string TitleForHeader(UITableView tableView, nint section)
            {
                return null;
            }

            public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                if (!_controller.TryGetTarget(out var controller))
                {
                    return;
                }

                tableView.DeselectRow(indexPath, true);
                tableView.EndEditing(true);
                if (indexPath.Row == 0 &&
                    ((controller._biometricUnlockOnly && indexPath.Section == 0) ||
                    indexPath.Section == 1))
                {
                    var task = controller.PromptBiometricAsync();
                    return;
                }
                var cell = tableView.CellAt(indexPath);
                if (cell == null)
                {
                    return;
                }
                if (cell is ISelectable selectableCell)
                {
                    selectableCell.Select();
                }
            }
        }
    }
}

