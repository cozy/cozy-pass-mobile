﻿using System;
using Bit.App.Abstractions;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using Xamarin.Forms;

namespace Bit.App.Controls
{
    public partial class CipherViewCell : ExtendedGrid
    {
        private const int ICON_COLUMN_DEFAULT_WIDTH = 40;
        private const int ICON_IMAGE_DEFAULT_WIDTH = 22;

        public static readonly BindableProperty CipherProperty = BindableProperty.Create(
            nameof(Cipher), typeof(CipherView), typeof(CipherViewCell), default(CipherView), BindingMode.OneWay);

        public static readonly BindableProperty WebsiteIconsEnabledProperty = BindableProperty.Create(
            nameof(WebsiteIconsEnabled), typeof(bool?), typeof(CipherViewCell));

        public static readonly BindableProperty ButtonCommandProperty = BindableProperty.Create(
            nameof(ButtonCommand), typeof(Command<CipherView>), typeof(CipherViewCell));

        public CipherViewCell()
        {
            InitializeComponent();

            var fontScale = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService").GetSystemFontSizeScale();
            _iconColumn.Width = new GridLength(ICON_COLUMN_DEFAULT_WIDTH * fontScale, GridUnitType.Absolute);
            _iconImage.WidthRequest = ICON_IMAGE_DEFAULT_WIDTH * fontScale;
            _iconImage.HeightRequest = ICON_IMAGE_DEFAULT_WIDTH * fontScale;
        }

        public bool? WebsiteIconsEnabled
        {
            get => (bool)GetValue(WebsiteIconsEnabledProperty);
            set => SetValue(WebsiteIconsEnabledProperty, value);
        }

        public CipherView Cipher
        {
            get => GetValue(CipherProperty) as CipherView;
            set => SetValue(CipherProperty, value);
        }

        public Command<CipherView> ButtonCommand
        {
            get => GetValue(ButtonCommandProperty) as Command<CipherView>;
            set => SetValue(ButtonCommandProperty, value);
        }

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == CipherProperty.PropertyName)
            {
                if (Cipher == null)
                {
                    return;
                }
                BindingContext = new CipherViewCellViewModel(Cipher, WebsiteIconsEnabled ?? false);
            }
            else if (propertyName == WebsiteIconsEnabledProperty.PropertyName)
            {
                if (Cipher == null)
                {
                    return;
                }
                ((CipherViewCellViewModel)BindingContext).WebsiteIconsEnabled = WebsiteIconsEnabled ?? false;
            }
        }

        private void MoreButton_Clicked(object sender, EventArgs e)
        {
            var cipher = ((sender as MiButton)?.BindingContext as CipherViewCellViewModel)?.Cipher;
            if (cipher != null)
            {
                ButtonCommand?.Execute(cipher);
            }
        }
    }
}
