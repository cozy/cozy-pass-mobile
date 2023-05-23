using Bit.App.Models;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Bit.App.Resources;
using Xamarin.Essentials;

#if __IOS__
using SafariServices;
using Foundation;
using UIKit;
#endif

namespace Bit.App.Pages
{
    public partial class HomePage : BaseContentPage
    {
        private readonly HomeViewModel _vm;
        private readonly AppOptions _appOptions;
        private IMessagingService _messagingService;
        private IBroadcasterService _broadcasterService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly II18nService _i18nService;
        private readonly ICozyClientService _cozyClientService;

        public HomePage(AppOptions appOptions = null)
        {
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");

            _messagingService.Send("showStatusBar", false);
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            _appOptions = appOptions;
            InitializeComponent();
            _vm = BindingContext as HomeViewModel;
            _vm.Page = this;
            _vm.StartLoginAction = () => Device.BeginInvokeOnMainThread(async () => await StartLoginAsync());
            _vm.StartRegisterAction = () => Device.BeginInvokeOnMainThread(async () => await StartRegisterAsync());
            _vm.StartSsoLoginAction = () => Device.BeginInvokeOnMainThread(async () => await StartSsoLoginAsync());
            _vm.StartEnvironmentAction = () => Device.BeginInvokeOnMainThread(async () => await StartEnvironmentAsync());
            // Cozy customization, disable UpdateLogo()
            // Since we force Inverted theme for HomePage, there is no need to compute Logo based on theme
            /*
            UpdateLogo();
            //*/
        }

        public async Task DismissRegisterPageAndLogInAsync(string email)
        {
            await Navigation.PopModalAsync();
            await Navigation.PushModalAsync(new NavigationPage(new LoginPage(email, _appOptions)));
        }

        protected override void OnAppearing()
        {
            ThemeManager.SetInvertedTheme();
            base.OnAppearing();
            _messagingService.Send("showStatusBar", false);
            CheckOnboarded();
            _broadcasterService.Subscribe(nameof(HomePage), async (message) =>
            {
                if (message.Command == "onboarded")
                {
                    CheckOnboarded();
                }

                if (message.Command == "updatedTheme")
                {
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        // Cozy customization, disable UpdateLogo()
                        // Since we force Inverted theme for HomePage, there is no need to compute Logo based on theme
                        /*
                        UpdateLogo();
                        //*/
                    });
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

        private void UpdateLogo()
        {
            _logo.Source = !ThemeManager.UsingLightTheme ? "logo_white.png" : "logo.png";
        }
        
        private void Close_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.CloseAction();
            }
        }

        private void LogIn_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.StartLoginAction();
            }
        }

        
        private void OpenRegistrationPageIOS(string url) {
#if __IOS__
            var window = UIApplication.SharedApplication.KeyWindow;
            var vc = window.RootViewController;
            var sfvc = new SFSafariViewController(new NSUrl(url), true);
            vc.PresentViewController(sfvc, true, null);
#endif

            // After subscribing, the user is redirected to a URL custom scheme
            // that is picked up in AppDelegate. The SafariViewController is
            // closed there since messages on the broadcast service are not
            // received by the HomePage while the SafariViewController is
            // presented
        }


        private void OpenRegistrationPageAndroid(string url)
        {
            _platformUtilsService.LaunchUri(url);
        }

        private void OpenRegistrationPage()
        {
            var lang = _i18nService.Culture.TwoLetterISOLanguageName;
            var url = _cozyClientService.GetRegistrationURL(lang: lang);
            if (Device.RuntimePlatform == Device.iOS) {
                OpenRegistrationPageIOS(url);
            } else {
                OpenRegistrationPageAndroid(url);
            }
        }

        // Cozy customization:
        // - Call ClouderyView from InAppBrowser
        //*
        private async Task StartLoginAsync()
        {
            var clouderyBaseUri = "https://manager.cozycloud.cc";
            var clouderyLoginRelativeUri = "/v2/neutral/start";
            var clouderyQueryString = "redirect_after_email=cozypass%3A%2F%2Fpass%2Fonboarding&redirect_after_login=cozypass%3A%2F%2Fpass%2Flogin";

            var url = $"{clouderyBaseUri}{clouderyLoginRelativeUri}?{clouderyQueryString}";

            await Browser.OpenAsync(url, new BrowserLaunchOptions
            {
                LaunchMode = BrowserLaunchMode.SystemPreferred,
                TitleMode = BrowserTitleMode.Show,
                Flags = BrowserLaunchFlags.PresentAsPageSheet
            });
        }
        //*/

        private void Register_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                // Cozy customization:
                // - Registration is not made in app but from cozy website
                //*
                OpenRegistrationPage();
                /*/
                _vm.StartRegisterAction();
                //*/
            }
        }
        
        private async Task StartRegisterAsync()
        {
            var page = new RegisterPage(this);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private void LogInSso_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.StartSsoLoginAction();
            }
        }

        private async Task StartSsoLoginAsync()
        {
            var page = new LoginSsoPage(_appOptions);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private void Environment_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.StartEnvironmentAction();
            }
        }
        
        private async Task StartEnvironmentAsync()
        {
            var page = new EnvironmentPage();
            await Navigation.PushModalAsync(new NavigationPage(page));
        }
        private void HasOnboarded()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                // A delay is needed here since otherwise we can show the Dialog
                // while a splashscreen is showing, and this prevents the splashscreen
                // to be removed.
                await Task.Delay(500);
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
    }
}
