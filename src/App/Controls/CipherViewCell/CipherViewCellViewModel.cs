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
                    TriggerPropertyChanged(nameof(CozyShared));
                    #endregion
                });
            }
        }


        #region cozy
        private IUserService _userService;

        public CipherViewCellViewModel()
        {
            _userService = ServiceContainer.Resolve<IUserService>("userService"); ;
        }

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
        #endregion
    }
}
