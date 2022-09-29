﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Domain;
using Bit.Core.Models.Request;
using Bit.Core.Utilities;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class SetPasswordPageViewModel : BaseViewModel
    {
        private readonly IDeviceActionService _deviceActionService;
        private readonly IApiService _apiService;
        private readonly ICryptoService _cryptoService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly IStateService _stateService;
        private readonly IPolicyService _policyService;
        private readonly IPasswordGenerationService _passwordGenerationService;
        private readonly II18nService _i18nService;

        private bool _showPassword;
        private bool _isPolicyInEffect;
        private bool _resetPasswordAutoEnroll;
        private string _policySummary;
        private MasterPasswordPolicyOptions _policy;

        public SetPasswordPageViewModel()
        {
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _apiService = ServiceContainer.Resolve<IApiService>("apiService");
            _cryptoService = ServiceContainer.Resolve<ICryptoService>("cryptoService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _stateService = ServiceContainer.Resolve<IStateService>("stateService");
            _policyService = ServiceContainer.Resolve<IPolicyService>("policyService");
            _passwordGenerationService =
                ServiceContainer.Resolve<IPasswordGenerationService>("passwordGenerationService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");

            PageTitle = AppResources.SetMasterPassword;
            TogglePasswordCommand = new Command(TogglePassword);
            ToggleConfirmPasswordCommand = new Command(ToggleConfirmPassword);
            SubmitCommand = new Command(async () => await SubmitAsync());
        }
        public bool ShowPassword
        {
            get => _showPassword;
            set => SetProperty(ref _showPassword, value,
                additionalPropertyNames: new[]
                {
                    nameof(ShowPasswordIcon),
                    nameof(PasswordVisibilityAccessibilityText)
                });
        }

        public bool IsPolicyInEffect
        {
            get => _isPolicyInEffect;
            set => SetProperty(ref _isPolicyInEffect, value);
        }

        public bool ResetPasswordAutoEnroll
        {
            get => _resetPasswordAutoEnroll;
            set => SetProperty(ref _resetPasswordAutoEnroll, value);
        }

        public string PolicySummary
        {
            get => _policySummary;
            set => SetProperty(ref _policySummary, value);
        }

        public MasterPasswordPolicyOptions Policy
        {
            get => _policy;
            set => SetProperty(ref _policy, value);
        }

        public Command SubmitCommand { get; }
        public Command TogglePasswordCommand { get; }
        public Command ToggleConfirmPasswordCommand { get; }
        public string ShowPasswordIcon => ShowPassword ? BitwardenIcons.EyeSlash : BitwardenIcons.Eye;
        public string PasswordVisibilityAccessibilityText => ShowPassword ? AppResources.PasswordIsVisibleTapToHide : AppResources.PasswordIsNotVisibleTapToShow;
        public string MasterPassword { get; set; }
        public string ConfirmMasterPassword { get; set; }
        public string Hint { get; set; }
        public Action SetPasswordSuccessAction { get; set; }
        public Action UpdateTempPasswordAction { get; set; }
        public Action CloseAction { get; set; }
        public string OrgIdentifier { get; set; }
        public string OrgId { get; set; }

        public async Task InitAsync()
        {
            await CheckPasswordPolicy();

            try
            {
                var response = await _apiService.GetOrganizationAutoEnrollStatusAsync(OrgIdentifier);
                OrgId = response.Id;
                ResetPasswordAutoEnroll = response.ResetPasswordEnabled;
            }
            catch (ApiException e)
            {
                if (e?.Error != null)
                {
                    await _platformUtilsService.ShowDialogAsync(e.Error.GetSingleMessage(),
                        AppResources.AnErrorHasOccurred);
                }
            }
        }

        public async Task SubmitAsync()
        {
            if (Connectivity.NetworkAccess == NetworkAccess.None)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InternetConnectionRequiredMessage,
                    AppResources.InternetConnectionRequiredTitle);
                return;
            }
            if (string.IsNullOrWhiteSpace(MasterPassword))
            {
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred,
                    string.Format(AppResources.ValidationFieldRequired, AppResources.MasterPassword),
                    AppResources.Ok);
                return;
            }
            if (IsPolicyInEffect)
            {
                var userInput = await GetPasswordStrengthUserInput();
                var passwordStrength = _passwordGenerationService.PasswordStrength(MasterPassword, userInput);
                if (!await _policyService.EvaluateMasterPassword(passwordStrength.Score, MasterPassword, Policy))
                {
                    await Page.DisplayAlert(AppResources.MasterPasswordPolicyValidationTitle,
                        AppResources.MasterPasswordPolicyValidationMessage, AppResources.Ok);
                    return;
                }
            }
            else
            {
                if (MasterPassword.Length < 8)
                {
                    await Page.DisplayAlert(AppResources.MasterPasswordPolicyValidationTitle,
                        AppResources.MasterPasswordLengthValMessage, AppResources.Ok);
                    return;
                }
            }
            if (MasterPassword != ConfirmMasterPassword)
            {
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred,
                    AppResources.MasterPasswordConfirmationValMessage, AppResources.Ok);
                return;
            }

            var kdf = KdfType.PBKDF2_SHA256;
            var kdfIterations = 100000;
            var email = await _stateService.GetEmailAsync();
            var key = await _cryptoService.MakeKeyAsync(MasterPassword, email, kdf, kdfIterations);
            var masterPasswordHash = await _cryptoService.HashPasswordAsync(MasterPassword, key, HashPurpose.ServerAuthorization);
            var localMasterPasswordHash = await _cryptoService.HashPasswordAsync(MasterPassword, key, HashPurpose.LocalAuthorization);

            Tuple<SymmetricCryptoKey, EncString> encKey;
            var existingEncKey = await _cryptoService.GetEncKeyAsync();
            if (existingEncKey == null)
            {
                encKey = await _cryptoService.MakeEncKeyAsync(key);
            }
            else
            {
                encKey = await _cryptoService.RemakeEncKeyAsync(key);
            }

            var keys = await _cryptoService.MakeKeyPairAsync(encKey.Item1);
            var request = new SetPasswordRequest
            {
                MasterPasswordHash = masterPasswordHash,
                Key = encKey.Item2.EncryptedString,
                MasterPasswordHint = Hint,
                Kdf = kdf,
                KdfIterations = kdfIterations,
                OrgIdentifier = OrgIdentifier,
                Keys = new KeysRequest
                {
                    PublicKey = keys.Item1,
                    EncryptedPrivateKey = keys.Item2.EncryptedString
                }
            };

            try
            {
                await _deviceActionService.ShowLoadingAsync(AppResources.CreatingAccount);
                // Set Password and relevant information
                await _apiService.SetPasswordAsync(request);
                await _stateService.SetKdfTypeAsync(kdf);
                await _stateService.SetKdfIterationsAsync(kdfIterations);
                await _cryptoService.SetKeyAsync(key);
                await _cryptoService.SetKeyHashAsync(localMasterPasswordHash);
                await _cryptoService.SetEncKeyAsync(encKey.Item2.EncryptedString);
                await _cryptoService.SetEncPrivateKeyAsync(keys.Item2.EncryptedString);

                if (ResetPasswordAutoEnroll)
                {
                    // Grab Organization Keys
                    var response = await _apiService.GetOrganizationKeysAsync(OrgId);
                    var publicKey = CoreHelpers.Base64UrlDecode(response.PublicKey);
                    // Grab user's Encryption Key and encrypt with Org Public Key
                    var userEncKey = await _cryptoService.GetEncKeyAsync();
                    var encryptedKey = await _cryptoService.RsaEncryptAsync(userEncKey.Key, publicKey);
                    // Request
                    var resetRequest = new OrganizationUserResetPasswordEnrollmentRequest
                    {
                        ResetPasswordKey = encryptedKey.EncryptedString,
                        MasterPasswordHash = masterPasswordHash,
                    };
                    var userId = await _stateService.GetActiveUserIdAsync();
                    // Enroll user
                    await _apiService.PutOrganizationUserResetPasswordEnrollmentAsync(OrgId, userId, resetRequest);
                }

                await _deviceActionService.HideLoadingAsync();
                SetPasswordSuccessAction?.Invoke();
            }
            catch (ApiException e)
            {
                await _deviceActionService.HideLoadingAsync();
                if (e?.Error != null)
                {
                    await _platformUtilsService.ShowDialogAsync(e.Error.GetSingleMessage(),
                        AppResources.AnErrorHasOccurred);
                }
            }
        }

        public void TogglePassword()
        {
            ShowPassword = !ShowPassword;
            (Page as SetPasswordPage).MasterPasswordEntry.Focus();
        }

        public void ToggleConfirmPassword()
        {
            ShowPassword = !ShowPassword;
            (Page as SetPasswordPage).ConfirmMasterPasswordEntry.Focus();
        }

        private async Task CheckPasswordPolicy()
        {
            Policy = await _policyService.GetMasterPasswordPolicyOptions();
            IsPolicyInEffect = Policy?.InEffect() ?? false;
            if (!IsPolicyInEffect)
            {
                return;
            }

            var bullet = "\n" + "".PadLeft(4) + "\u2022 ";
            var sb = new StringBuilder();
            sb.Append(_i18nService.T("MasterPasswordPolicyInEffect"));
            if (Policy.MinComplexity > 0)
            {
                sb.Append(bullet)
                    .Append(string.Format(_i18nService.T("PolicyInEffectMinComplexity"), Policy.MinComplexity));
            }
            if (Policy.MinLength > 0)
            {
                sb.Append(bullet).Append(string.Format(_i18nService.T("PolicyInEffectMinLength"), Policy.MinLength));
            }
            if (Policy.RequireUpper)
            {
                sb.Append(bullet).Append(_i18nService.T("PolicyInEffectUppercase"));
            }
            if (Policy.RequireLower)
            {
                sb.Append(bullet).Append(_i18nService.T("PolicyInEffectLowercase"));
            }
            if (Policy.RequireNumbers)
            {
                sb.Append(bullet).Append(_i18nService.T("PolicyInEffectNumbers"));
            }
            if (Policy.RequireSpecial)
            {
                sb.Append(bullet).Append(string.Format(_i18nService.T("PolicyInEffectSpecial"), "!@#$%^&*"));
            }
            PolicySummary = sb.ToString();
        }

        private async Task<List<string>> GetPasswordStrengthUserInput()
        {
            var email = await _stateService.GetEmailAsync();
            List<string> userInput = null;
            var atPosition = email.IndexOf('@');
            if (atPosition > -1)
            {
                var rx = new Regex("/[^A-Za-z0-9]/", RegexOptions.Compiled);
                var data = rx.Split(email.Substring(0, atPosition).Trim().ToLower());
                userInput = new List<string>(data);
            }
            return userInput;
        }
    }
}
