using System;
using System.Collections.Generic;
using System.Globalization;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using Flurl;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace Bit.App.Pages.Accounts
{    
    public partial class CozyNoOnboardingPage : BaseContentPage
    {
        private readonly IMessagingService _messagingService;

        public CozyNoOnboardingPage()
        {
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _messagingService.Send("showStatusBar", true);
            InitializeComponent ();
        }

        private async void Close_Clicked(object sender, EventArgs e)
        {
            _messagingService.Send("showStatusBar", false);
            await Navigation.PopModalAsync();
        }

        private async void Flagship_Clicked(object sender, EventArgs e)
        {
            string url = string.Empty;
            if (Device.RuntimePlatform == Device.iOS)
            {
                url = $"itms-apps://apps.apple.com/id/app/cozy/id1600636174?l=id";
            }
            else if (Device.RuntimePlatform == Device.Android)
            {
                url = $"https://play.google.com/store/apps/details?id=io.cozy.flagship.mobile";
            }
            await Browser.OpenAsync(url, BrowserLaunchMode.External);
        }
    }
}
