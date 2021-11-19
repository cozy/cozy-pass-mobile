using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bit.App.Pages
{
    public class SharePageViewModel : BaseViewModel
    {
        private readonly IDeviceActionService _deviceActionService;
        private readonly ICipherService _cipherService;
        private readonly ICollectionService _collectionService;
        private readonly IUserService _userService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private CipherView _cipher;
        private int _organizationSelectedIndex;
        private bool _hasCollections;
        private bool _hasOrganizations;
        private List<Core.Models.View.CollectionView> _writeableCollections;

        public SharePageViewModel()
        {
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _cipherService = ServiceContainer.Resolve<ICipherService>("cipherService");
            _userService = ServiceContainer.Resolve<IUserService>("userService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _collectionService = ServiceContainer.Resolve<ICollectionService>("collectionService");
            Collections = new ExtendedObservableCollection<CollectionViewModel>();
            OrganizationOptions = new List<KeyValuePair<string, string>>();
            PageTitle = AppResources.MoveToOrganization;
        }

        public string CipherId { get; set; }
        public string OrganizationId { get; set; }
        public List<KeyValuePair<string, string>> OrganizationOptions { get; set; }
        public ExtendedObservableCollection<CollectionViewModel> Collections { get; set; }
        public int OrganizationSelectedIndex
        {
            get => _organizationSelectedIndex;
            set
            {
                if (SetProperty(ref _organizationSelectedIndex, value))
                {
                    OrganizationChanged();
                }
            }
        }
        public bool HasCollections
        {
            get => _hasCollections;
            set => SetProperty(ref _hasCollections, value);
        }
        public bool HasOrganizations
        {
            get => _hasOrganizations;
            set => SetProperty(ref _hasOrganizations, value);
        }

        public async Task LoadAsync()
        {
            var allCollections = await _collectionService.GetAllDecryptedAsync();
            _writeableCollections = allCollections.Where(c => !c.ReadOnly).ToList();

            var orgs = await _userService.GetAllOrganizationAsync();
            OrganizationOptions = orgs.OrderBy(o => o.Name)
                .Where(o => o.Enabled && o.Status == OrganizationUserStatusType.Confirmed)
                .Select(o => new KeyValuePair<string, string>(o.Name, o.Id)).ToList();
            HasOrganizations = OrganizationOptions.Any();

            // Cozy customization, add "None" collection
            //*
            OrganizationOptions.Insert(0, new KeyValuePair<string, string>(AppResources.ShareNone, "None"));
            //*/

            var cipherDomain = await _cipherService.GetAsync(CipherId);
            _cipher = await cipherDomain.DecryptAsync();

            // Cozy customization, use HasOrganizations instead of OrganizationOptions.Any()
            // this is needed because we added "None" option
            // also try to synchronize with cipher current organization
            /*
            if (OrganizationId == null && OrganizationOptions.Any())
            /*/
            OrganizationId = _cipher.OrganizationId;
            if (OrganizationId == null && HasOrganizations)
            //*/
            {
                OrganizationId = OrganizationOptions.First().Value;
            }
            OrganizationSelectedIndex = string.IsNullOrWhiteSpace(OrganizationId) ? 0 :
                OrganizationOptions.FindIndex(k => k.Value == OrganizationId);
            FilterCollections();
        }

        public async Task<bool> SubmitAsync()
        {
            // Cozy customization, select ALL collections
            //*
            var cipherDomain = await _cipherService.GetAsync(CipherId);
            var cipherView = await cipherDomain.DecryptAsync();
            var isCipherInOrganization = !string.IsNullOrEmpty(cipherView.OrganizationId);
            //*/

            // Cozy customization, select ALL collections
            // At Cozy we force 1-1 relationship between Orgnanizations and Collections
            // so when an Organization is select we know that the corresponding Collection should be also selected
            /*
            var selectedCollectionIds = Collections?.Where(c => c.Checked).Select(c => c.Collection.Id);
            /*/
            var selectedCollectionIds = Collections?.Select(c => c.Collection.Id);
            //*/

            // Cozy customization, add Unshare feature
            /*
            if (!selectedCollectionIds?.Any() ?? true)
            /*/
            if (isCipherInOrganization && selectedCollectionIds?.Count() == 0)
            {
                return await Unshare(cipherView);
            }
            else if (!isCipherInOrganization && (!selectedCollectionIds?.Any() ?? true))
            //*/
            {
                await Page.DisplayAlert(AppResources.AnErrorHasOccurred, AppResources.SelectOneCollection,
                    AppResources.Ok);
                return false;
            }
            if (Xamarin.Essentials.Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.None)
            {
                await _platformUtilsService.ShowDialogAsync(AppResources.InternetConnectionRequiredMessage,
                    AppResources.InternetConnectionRequiredTitle);
                return false;
            }

            // Cozy customization, moved on function top
            /*
            var cipherDomain = await _cipherService.GetAsync(CipherId);
            var cipherView = await cipherDomain.DecryptAsync();
            //*/

            var checkedCollectionIds = new HashSet<string>(selectedCollectionIds);
            try
            {
                await _deviceActionService.ShowLoadingAsync(AppResources.Saving);
                await _cipherService.ShareWithServerAsync(cipherView, OrganizationId, checkedCollectionIds);
                await _deviceActionService.HideLoadingAsync();
                var movedItemToOrgText = string.Format(AppResources.MovedItemToOrg, cipherView.Name,
                   (await _userService.GetOrganizationAsync(OrganizationId)).Name);
                _platformUtilsService.ShowToast("success", null, movedItemToOrgText);
                await Page.Navigation.PopModalAsync();
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
            catch (System.Exception e)
            {
                await _deviceActionService.HideLoadingAsync();
                if (e.Message != null)
                {
                    await _platformUtilsService.ShowDialogAsync(e.Message, AppResources.AnErrorHasOccurred);
                }
            }
            return false;
        }

        private void OrganizationChanged()
        {
            if (OrganizationSelectedIndex > -1)
            {
                OrganizationId = OrganizationOptions[OrganizationSelectedIndex].Value;
                FilterCollections();
            }
        }

        private void FilterCollections()
        {
            if (OrganizationId == null || !_writeableCollections.Any())
            {
                Collections.ResetWithRange(new List<CollectionViewModel>());
            }
            else
            {
                var cols = _writeableCollections.Where(c => c.OrganizationId == OrganizationId)
                    .Select(c => new CollectionViewModel { Collection = c }).ToList();
                Collections.ResetWithRange(cols);
            }
            HasCollections = Collections.Any();
        }

        private async Task<bool> Unshare(CipherView cipherView)
        {
            try
            {
                await _cipherService.UnshareWithServerAsync(cipherView);
                _platformUtilsService.ShowToast("success", null, AppResources.ItemUnshared);
                await Page.Navigation.PopModalAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
