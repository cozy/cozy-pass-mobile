using Bit.App.Utilities;
using Bit.Core.Models.View;
using Bit.Core.Utilities;

namespace Bit.App.Controls
{
    public class CipherViewCellViewModel : ExtendedViewModel
    {
        private CipherView _cipher;
        private bool _websiteIconsEnabled;
        private string _iconImageSource = string.Empty;

        public CipherViewCellViewModel(CipherView cipherView, bool websiteIconsEnabled)
        {
            // Cozy customization, show "Cozy shared" icon next to Cozy's shared ciphers
            //*
            _organizationService = ServiceContainer.Resolve<IOrganizationService>("organizationService");
            //*/
            Cipher = cipherView;
            WebsiteIconsEnabled = websiteIconsEnabled;
        }

        public CipherView Cipher
        {
            get => _cipher;
            // Cozy customization, show "Cozy shared" icon next to Cozy's shared ciphers
            /*
            set => SetProperty(ref _cipher, value);
            /*/
            set
            {
                SetProperty(ref _cipher, value, () => {
                    #region cozy
                    TriggerPropertyChanged(nameof(CozyShared));
                    #endregion
                });
            }
            //*/
        }

        public bool WebsiteIconsEnabled
        {
            get => _websiteIconsEnabled;
            set => SetProperty(ref _websiteIconsEnabled, value);
        }

        public bool ShowIconImage
        {
            get => WebsiteIconsEnabled
                && !string.IsNullOrWhiteSpace(Cipher.LaunchUri)
                && IconImageSource != null;
        }

        public string IconImageSource
        {
            get
            {
                if (_iconImageSource == string.Empty) // default value since icon source can return null
                {
                    _iconImageSource = IconImageHelper.GetIconImage(Cipher);
                }
                return _iconImageSource;
            }

        }

        // Cozy customization, show "Cozy shared" icon next to Cozy's shared ciphers
        //*
        private IOrganizationService _organizationService;

        public static readonly BindableProperty CozySharedProperty = BindableProperty.Create(
           nameof(CozyShared),
           typeof(bool),
           typeof(CipherViewCellViewModel),
           false
        );

        public bool CozyShared
        {
            get
            {
                var cozyOrganizationId = _organizationService.CozyOrganizationId;
                return _cipher.OrganizationId == cozyOrganizationId;
            }
        }
        //*/
    }
}
