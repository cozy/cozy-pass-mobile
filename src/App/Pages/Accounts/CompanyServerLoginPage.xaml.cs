using Bit.App.Models;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Bit.App.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class CompanyServerLoginPage : BaseContentPage
    {
        private readonly IMessagingService _messagingService;
        private readonly CompanyServerLoginPageModel _vm;

        public CompanyServerLoginPage()
        {
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _messagingService.Send("showStatusBar", true);
            InitializeComponent();
            _vm = BindingContext as CompanyServerLoginPageModel;
            _vm.Page = this;
            _vm.CloseAction = async () =>
            {
                _messagingService.Send("showStatusBar", false);
                await Navigation.PopModalAsync();
            };
            if (Device.RuntimePlatform == Device.Android)
            {
                ToolbarItems.RemoveAt(0);
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (string.IsNullOrWhiteSpace(_vm.Email))
            {
                RequestFocus(_email);
            }
        }

        private async void LogIn_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.LogInAsync();
            }
        }

        private void Close_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.CloseAction();
            }
        }

        private async void UseMyTwakeURL_Clicked(object sender, EventArgs e)
        {
            var page = new LoginPage(null, null);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }
    }
}
