// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace Bit.iOS.ShareExtension
{
	[Register ("LoadingViewController")]
	partial class LoadingViewController
	{
		[Outlet]
		UIKit.UIImageView Logo { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Logo != null) {
				Logo.Dispose ();
				Logo = null;
			}
		}
	}
}
