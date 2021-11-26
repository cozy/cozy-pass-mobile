using Bit.iOS.Core.Utilities;
using System;
using UIKit;

namespace Bit.iOS.Extension
{
    public partial class LockPasswordViewController : Core.Controllers.LockPasswordViewController
    {
        public LockPasswordViewController(IntPtr handle)
            : base(handle)
        {
            BiometricIntegrityKey = Bit.Core.Constants.iOSExtensionBiometricIntegrityKey;
            DismissModalAction = Cancel;
        }

        public LoadingViewController LoadingController { get; set; }
        public override UINavigationItem BaseNavItem => NavItem;
        public override UIBarButtonItem BaseCancelButton => CancelButton;
        public override UIBarButtonItem BaseSubmitButton => SubmitButton;
        public override Action Success => () => LoadingController.DismissLockAndContinue();
        public override Action Cancel => () => LoadingController.CompleteRequest(null, null);

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            CancelButton.TintColor = ThemeHelpers.NavBarTextColor;
            SubmitButton.TintColor = ThemeHelpers.NavBarTextColor;
        }

        partial void SubmitButton_Activated(UIBarButtonItem sender)
        {
            var task = CheckPasswordAsync();
        }

        partial void CancelButton_Activated(UIBarButtonItem sender)
        {
            Cancel();
        }
    }
}
