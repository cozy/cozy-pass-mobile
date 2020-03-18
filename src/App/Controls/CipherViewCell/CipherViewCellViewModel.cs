using System;
using System.ComponentModel;
using Bit.Core.Abstractions;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using Xamarin.Forms;

namespace Bit.App.Controls
{
    public class CipherViewCellViewModel : ExtendedViewModel
    {
        private CipherView _cipher;

        public CipherView Cipher
        {
            get => _cipher;
            set {
                SetProperty(ref _cipher, value, () => {
                    #region cozy
                    TriggerPropertyChanged(nameof(SharedIconText));
                    #endregion
                });
            }
        }


        #region cozy
        private static readonly string shareIcon = "\uf1e0";
        private static readonly string cloudIcon = "\uf0c2";

        private IUserService _userService;

        public CipherViewCellViewModel()
        {
            _userService = ServiceContainer.Resolve<IUserService>("userService"); ;
        }

        public static readonly BindableProperty SharedIconTextProperty = BindableProperty.Create(
           nameof(SharedIconText),
           typeof(string),
           typeof(CipherViewCellViewModel),
           string.Empty
       );

        public string SharedIconText {
            get
            {
                var cozyOrganizationId = _userService.CozyOrganizationId;
                if (_cipher.OrganizationId == cozyOrganizationId)
                {
                    return CipherViewCellViewModel.cloudIcon;
                } else
                {
                    return CipherViewCellViewModel.shareIcon;
                }
                
            }
        }
        #endregion
    }
}
