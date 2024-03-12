using System;
using System.Threading.Tasks;
using Bit.App.Models;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class HomePage : BaseContentPage
    {
        private bool _checkRememberedEmail;
        private readonly HomeViewModel _vm;
        private readonly AppOptions _appOptions;
        private IBroadcasterService _broadcasterService;
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly II18nService _i18nService;
        private readonly ICozyClientService _cozyClientService;

        readonly LazyResolve<ILogger> _logger = new LazyResolve<ILogger>();

        public HomePage(AppOptions appOptions = null)
        {
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            _appOptions = appOptions;
            InitializeComponent();
            _vm = BindingContext as HomeViewModel;
            _vm.Page = this;
            _vm.ShowCancelButton = _appOptions?.IosExtension ?? false;
            _vm.StartLoginAction = async () => await StartLoginAsync();
            _vm.StartRegisterAction = () => Device.BeginInvokeOnMainThread(async () => await StartRegisterAsync());
            _vm.StartSsoLoginAction = () => Device.BeginInvokeOnMainThread(async () => await StartSsoLoginAsync());
            _vm.StartEnvironmentAction = () => Device.BeginInvokeOnMainThread(async () => await StartEnvironmentAsync());
            _vm.CloseAction = async () =>
            {
                await _accountListOverlay.HideAsync();
                await Navigation.PopModalAsync();
            };
            // Cozy customization, disable UpdateLogo()
            // Since we force Inverted theme for HomePage, there is no need to compute Logo based on theme
            /*
            UpdateLogo();
            //*/

            if (!_vm.ShowCancelButton)
            {
                ToolbarItems.Remove(_closeButton);
            }
            if (_appOptions?.HideAccountSwitcher ?? false)
            {
                ToolbarItems.Remove(_accountAvatar);
            }
        }

        public async Task DismissRegisterPageAndLogInAsync(string email)
        {
            await Navigation.PopModalAsync();
            await Navigation.PushModalAsync(new NavigationPage(new LoginPage(email, _appOptions)));
        }

        protected override async void OnAppearing()
        {
            ThemeManager.SetInvertedTheme();
            base.OnAppearing();
            _mainContent.Content = _mainLayout;
            _accountAvatar?.OnAppearing();
            CheckOnboarded();

            if (!_appOptions?.HideAccountSwitcher ?? false)
            {
                _vm.AvatarImageSource = await GetAvatarImageSourceAsync(false);
            }
            _broadcasterService.Subscribe(nameof(HomePage), (message) =>
            {
                if (message.Command == "onboarded")
                {
                    CheckOnboarded();
                }

                if (message.Command is ThemeManager.UPDATED_THEME_MESSAGE_KEY)
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
            try
            {
                await _vm.UpdateEnvironmentAsync();
            }
            catch (Exception ex)
            {
                _logger.Value?.Exception(ex);
            }
        }

        public void CheckOnboarded()
        {
            if (_cozyClientService.CheckStateAndSecretInOnboardingCallbackURL())
            {
                _cozyClientService.OnboardedURL = null;
                HasOnboarded();
            }
        }

        protected override bool OnBackButtonPressed()
        {
            if (_accountListOverlay.IsVisible)
            {
                _accountListOverlay.HideAsync().FireAndForget();
                return true;
            }
            return false;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _broadcasterService.Unsubscribe(nameof(HomePage));
            _accountAvatar?.OnDisappearing();
        }

        private void UpdateLogo()
        {
            _logo.Source = !ThemeManager.UsingLightTheme ? "logo_white.png" : "logo.png";
        }

        private void Cancel_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.CloseAction();
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

        private async Task StartLoginAsync()
        {
            // Cozy customization, Call ClouderyView from InAppBrowser
            /*
            var page = new LoginPage(_vm.Email, _appOptions);
            await Navigation.PushModalAsync(new NavigationPage(page));
            /*/
            var url = await _cozyClouderyEnvService.GetClouderyUrl();

            await Browser.OpenAsync(url, new BrowserLaunchOptions
            {
                LaunchMode = BrowserLaunchMode.SystemPreferred,
                TitleMode = BrowserTitleMode.Show,
                Flags = BrowserLaunchFlags.PresentAsPageSheet
            });
            //*/
        }

        private async Task StartRegisterAsync()
        {
            // Cozy customization:
            // - Registration is not made in app but from cozy website
            /*
            var page = new RegisterPage(this);
            await Navigation.PushModalAsync(new NavigationPage(page));
            /*/
            OpenRegistrationPage();
            //*/
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

        private async Task StartEnvironmentAsync()
        {
            await _accountListOverlay.HideAsync();
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
