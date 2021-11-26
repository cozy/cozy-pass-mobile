﻿using Bit.Core.Abstractions;
using Bit.Core.Utilities;
using System;
using System.Threading.Tasks;
using Bit.App.Models;
using Bit.App.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class SetPasswordPage : BaseContentPage
    {
        private readonly IMessagingService _messagingService;
        private readonly SetPasswordPageViewModel _vm;
        private readonly AppOptions _appOptions;

        public SetPasswordPage(AppOptions appOptions = null, string orgIdentifier = null)
        {
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _messagingService.Send("showStatusBar", true);
            _appOptions = appOptions;
            InitializeComponent();
            _vm = BindingContext as SetPasswordPageViewModel;
            _vm.Page = this;
            _vm.SetPasswordSuccessAction =
                () => Device.BeginInvokeOnMainThread(async () => await SetPasswordSuccessAsync());
            _vm.CloseAction = async () =>
            {
                _messagingService.Send("showStatusBar", false);
                await Navigation.PopModalAsync();
            };
            _vm.OrgIdentifier = orgIdentifier;
            if (Device.RuntimePlatform == Device.Android)
            {
                ToolbarItems.RemoveAt(0);
            }

            MasterPasswordEntry = _masterPassword;
            ConfirmMasterPasswordEntry = _confirmMasterPassword;

            _masterPassword.ReturnType = ReturnType.Next;
            _masterPassword.ReturnCommand = new Command(() => _confirmMasterPassword.Focus());
            _confirmMasterPassword.ReturnType = ReturnType.Next;
            _confirmMasterPassword.ReturnCommand = new Command(() => _hint.Focus());
        }

        public Entry MasterPasswordEntry { get; set; }
        public Entry ConfirmMasterPasswordEntry { get; set; }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitAsync();
            RequestFocus(_masterPassword);
        }

        private async void Submit_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.SubmitAsync();
            }
        }

        private async void Close_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.CloseAction();
            }
        }

        private async Task SetPasswordSuccessAsync()
        {
            if (AppHelpers.SetAlternateMainPage(_appOptions))
            {
                return;
            }
            var previousPage = await AppHelpers.ClearPreviousPage();
            Application.Current.MainPage = new TabsPage(_appOptions, previousPage);
        }
    }
}
