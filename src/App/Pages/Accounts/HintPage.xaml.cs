﻿using Xamarin.Forms;
using Bit.App.Utilities;

namespace Bit.App.Pages
{
    public partial class HintPage : BaseContentPage
    {
        private HintPageViewModel _vm;

        public HintPage()
        {
            InitializeComponent();
            _vm = BindingContext as HintPageViewModel;
            _vm.Page = this;
            if (Device.RuntimePlatform == Device.Android)
            {
                ToolbarItems.RemoveAt(0);
            }
        }

        protected override void OnAppearing()
        {
            ThemeManager.SetInvertedTheme();
            base.OnAppearing();
            RequestFocus(_email);
        }

        private async void Close_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PopModalAsync();
            }
        }
    }
}
