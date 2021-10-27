﻿using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Bit.Core.Exceptions;
using Bit.Core.Models.Domain;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class AttachmentsPageViewModel : BaseViewModel
    {
        private readonly IDeviceActionService _deviceActionService;
        private readonly ICipherService _cipherService;
        private readonly ICryptoService _cryptoService;
        private readonly IUserService _userService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private CipherView _cipher;
        private Cipher _cipherDomain;
        private bool _hasAttachments;
        private bool _hasUpdatedKey;
        private bool _canAccessAttachments;
        private string _fileName;

        public AttachmentsPageViewModel()
        {
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _cipherService = ServiceContainer.Resolve<ICipherService>("cipherService");
            _cryptoService = ServiceContainer.Resolve<ICryptoService>("cryptoService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _userService = ServiceContainer.Resolve<IUserService>("userService");
            Attachments = new ExtendedObservableCollection<AttachmentView>();
            DeleteAttachmentCommand = new Command<AttachmentView>(DeleteAsync);
            PageTitle = AppResources.Attachments;
        }

        public string CipherId { get; set; }
        public CipherView Cipher
        {
            get => _cipher;
            set => SetProperty(ref _cipher, value);
        }
        public ExtendedObservableCollection<AttachmentView> Attachments { get; set; }
        public bool HasAttachments
        {
            get => _hasAttachments;
            set => SetProperty(ref _hasAttachments, value);
        }
        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }
        public byte[] FileData { get; set; }
        public Command DeleteAttachmentCommand { get; set; }

        public async Task InitAsync()
        {
            _cipherDomain = await _cipherService.GetAsync(CipherId);
            Cipher = await _cipherDomain.DecryptAsync();
            LoadAttachments();
            _hasUpdatedKey = await _cryptoService.HasEncKeyAsync();
            var canAccessPremium = await _userService.CanAccessPremiumAsync();
            _canAccessAttachments = canAccessPremium || Cipher.OrganizationId != null;
            if (!_canAccessAttachments)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.PremiumRequired);
            }
            else if (!_hasUpdatedKey)
            {
                var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.UpdateKey,
                    AppResources.FeatureUnavailable, AppResources.LearnMore, AppResources.Cancel);
                if (confirmed)
                {
                    _platformUtilsService.LaunchUri("https://help.bitwarden.com/article/update-encryption-key/");
                }
            }
        }

        public async Task<bool> SubmitAsync()
        {
            if (Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.None)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InternetConnectionRequiredMessage,
                    AppResources.InternetConnectionRequiredTitle);
                return false;
            }
            if (!_hasUpdatedKey)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.UpdateKey,
                    AppResources.AnErrorHasOccurred);
                return false;
            }
            if (FileData == null)
            {
                await _platformUtilsService.ShowDialogAsync(
                    string.Format(AppResources.ValidationFieldRequired, AppResources.File),
                    AppResources.AnErrorHasOccurred);
                return false;
            }
            if (FileData.Length > 104857600) // 100 MB
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.MaxFileSize,
                    AppResources.AnErrorHasOccurred);
                return false;
            }
            try
            {
                await _deviceActionService.ShowLoadingAsync(AppResources.Saving);
                _cipherDomain = await _cipherService.SaveAttachmentRawWithServerAsync(
                    _cipherDomain, FileName, FileData);
                Cipher = await _cipherDomain.DecryptAsync();
                await _deviceActionService.HideLoadingAsync();
                _platformUtilsService.ShowToast("success", null, AppResources.AttachementAdded);
                LoadAttachments();
                FileData = null;
                FileName = null;
                return true;
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
            return false;
        }

        public async Task ChooseFileAsync()
        {
            await _deviceActionService.SelectFileAsync();
        }

        private async void DeleteAsync(AttachmentView attachment)
        {
            if (Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.None)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InternetConnectionRequiredMessage,
                    AppResources.InternetConnectionRequiredTitle);
                return;
            }
            var confirmed = await _platformUtilsService.ShowDialogAsync(AppResources.DoYouReallyWantToDelete,
                null, AppResources.Yes, AppResources.No);
            if (!confirmed)
            {
                return;
            }
            try
            {
                await _deviceActionService.ShowLoadingAsync(AppResources.Deleting);
                await _cipherService.DeleteAttachmentWithServerAsync(Cipher.Id, attachment.Id);
                await _deviceActionService.HideLoadingAsync();
                _platformUtilsService.ShowToast("success", null, AppResources.AttachmentDeleted);
                var attachmentToRemove = Cipher.Attachments.FirstOrDefault(a => a.Id == attachment.Id);
                if (attachmentToRemove != null)
                {
                    Cipher.Attachments.Remove(attachmentToRemove);
                    LoadAttachments();
                }
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

        private void LoadAttachments()
        {
            Attachments.ResetWithRange(Cipher.Attachments ?? new List<AttachmentView>());
            HasAttachments = Cipher.HasAttachments;
        }
    }
}
