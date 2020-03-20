using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class HomePage : BaseContentPage
    {
        private IMessagingService _messagingService;
        private IPlatformUtilsService _platformUtilsService;
        private II18nService _i18nService;

        public HomePage()
        {
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _i18nService = ServiceContainer.Resolve<II18nService>("i18nService");

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
                _platformUtilsService.LaunchUri($"https://manager.cozycloud.cc/cozy/create?lang={lang}");
                // Navigation.PushModalAsync(new NavigationPage(new RegisterPage(this)));
                #endregion
            }
        }

        private void Settings_Clicked(object sender, EventArgs e)
        {
            if(DoOnce())
            {
                Navigation.PushModalAsync(new NavigationPage(new EnvironmentPage()));
            }
        }
    }
}
