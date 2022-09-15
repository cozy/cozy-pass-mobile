using System;
using Bit.Core;
using Xamarin.Forms;

namespace Bit.App.Utilities
{
    public class ToggleArrowConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool? isToggled = System.Convert.ToBoolean(value);
            if (isToggled.HasValue && isToggled.Value == true)
            {
                return BitwardenIcons.AngleRight;
            }
            return BitwardenIcons.AngleDown;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}