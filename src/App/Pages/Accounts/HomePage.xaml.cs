using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Bit.App.Resources;

namespace Bit.App.Pages
{
    public partial class HomePage : BaseContentPage
    {
        private readonly IMessagingService _messagingService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly II18nService _i18nService;
        private readonly ICozyClientService _cozyClientService;
        private readonly IBroadcasterService _broadcasterService;

        public HomePage()
        {
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");

            _messagingService.Send("showStatusBar", false);
            InitializeComponent();
            _logo.Source = !ThemeManager.UsingLightTheme ? "logo_white.png" : "logo.png";
        }

        public async Task DismissRegisterPageAndLogInAsync(string email)
        {
            await Navigation.PopModalAsync();
            await Navigation.PushModalAsync(new NavigationPage(new LoginPage(email)));
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _messagingService.Send("showStatusBar", false);
            CheckOnboarded();
            _broadcasterService.Subscribe(nameof(HomePage), (message) =>
            {
                if (message.Command == "onboarded")
                {
                    CheckOnboarded();
                }
            });
        }

        public void CheckOnboarded ()
        {
            if (_cozyClientService.CheckStateAndSecretInOnboardingCallbackURL())
            {
                _cozyClientService.OnboardedURL = null;
                HasOnboarded();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _broadcasterService.Unsubscribe(nameof(HomePage));
        }

        private void LogIn_Clicked(object sender, EventArgs e)
        {
            if(DoOnce())
            {
                Navigation.PushModalAsync(new NavigationPage(new LoginPage()));
            }
        }


        private void Register_Clicked(object sender, EventArgs e)
        {
            if(DoOnce())
            {
                #region cozy
                var lang = _i18nService.Culture.TwoLetterISOLanguageName;
                var uri = _cozyClientService.GetRegistrationURL(lang: lang);
                _platformUtilsService.LaunchUri(uri);
                // Navigation.PushModalAsync(new NavigationPage(new RegisterPage(this)));
                #endregion
            }
        }

        private void HasOnboarded()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                await DisplayOnboardedDialogAsync();
                await Navigation.PushModalAsync(new NavigationPage(new LoginPage()));
            });
   
        }

        #region cozy
        private async Task DisplayOnboardedDialogAsync()
        {
            await _platformUtilsService.ShowDialogAsync(AppResources.RegistrationSuccess, AppResources.CozyPass,
                            AppResources.Close);
        }
        #endregion


        private void Settings_Clicked(object sender, EventArgs e)
        {
            if(DoOnce())
            {
                Navigation.PushModalAsync(new NavigationPage(new EnvironmentPage()));
            }
        }
    }
}
