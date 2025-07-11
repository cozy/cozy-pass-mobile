﻿using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class GroupingsPageListItemSelector : DataTemplateSelector
    {
        public DataTemplate CipherTemplate { get; set; }
        public DataTemplate GroupTemplate { get; set; }
        public DataTemplate HeaderTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is GroupingsPageListGroup)
            {
                return HeaderTemplate;
            }
            if (item is GroupingsPageListItem listItem)
            {
                return listItem.Cipher != null ? CipherTemplate : GroupTemplate;
            }
            return null;
        }
    }
}
