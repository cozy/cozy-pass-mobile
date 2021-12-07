using System;
using System.ComponentModel;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using Xamarin.Forms;

namespace Bit.App.Controls
{
    public class CipherViewCellViewModel : ExtendedViewModel
    {
        private CipherView _cipher;
        private bool _websiteIconsEnabled;
        private string _iconImageSource = string.Empty;

        public CipherViewCellViewModel(CipherView cipherView, bool websiteIconsEnabled)
        {
            _userService = ServiceContainer.Resolve<IUserService>("userService");
            Cipher = cipherView;
            WebsiteIconsEnabled = websiteIconsEnabled;
        }

        public CipherView Cipher
        {
            get => _cipher;
            set {
                SetProperty(ref _cipher, value, () => {
                    #region cozy
                    TriggerPropertyChanged(nameof(CozyShared));
                    #endregion
                });
            }
        }

        public bool WebsiteIconsEnabled
        {
            get => _websiteIconsEnabled;
            set => SetProperty(ref _websiteIconsEnabled, value);
        }

        public bool ShowIconImage
        {
            get => WebsiteIconsEnabled
                && !string.IsNullOrWhiteSpace(Cipher.Login?.Uri)
                && IconImageSource != null;
        }

        public string IconImageSource
        {
            get
            {
                if (_iconImageSource == string.Empty) // default value since icon source can return null
                {
                    _iconImageSource = IconImageHelper.GetLoginIconImage(Cipher);
                }
                return _iconImageSource;
            }

        }

        #region cozy
        private IUserService _userService;

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
                var cozyOrganizationId = _userService.CozyOrganizationId;
                return _cipher.OrganizationId == cozyOrganizationId;
            }
        }

        public string CozySharedImage
        {
            get
            {
                return _cipher.IsKonnector ? "shared_with_cozy_icon.png" : "login.png";
            }
        }
        #endregion
    }
}
