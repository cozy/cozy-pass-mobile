﻿using Android.Content;
using Android.Views;
using Bit.App.Pages;
using Bit.Droid.Renderers;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using Xamarin.Forms.Platform.Android.AppCompat;

[assembly: ExportRenderer(typeof(TabbedPage), typeof(CustomTabbedRenderer))]
namespace Bit.Droid.Renderers
{
    public class CustomTabbedRenderer : TabbedPageRenderer, NavigationBarView.IOnItemReselectedListener
    {
        private TabbedPage _page;

        public CustomTabbedRenderer(Context context) : base(context) { }

        protected override void OnElementChanged(ElementChangedEventArgs<TabbedPage> e)
        {
            base.OnElementChanged(e);
            if (e.NewElement != null)
            {
                _page = e.NewElement;
                GetBottomNavigationView()?.SetOnItemReselectedListener(this);
            }
            else
            {
                _page = e.OldElement;
            }
        }

        private BottomNavigationView GetBottomNavigationView()
        {
            for (var i = 0; i < ViewGroup.ChildCount; i++)
            {
                var childView = ViewGroup.GetChildAt(i);
                if (childView is ViewGroup viewGroup)
                {
                    for (var j = 0; j < viewGroup.ChildCount; j++)
                    {
                        var childRelativeLayoutView = viewGroup.GetChildAt(j);
                        if (childRelativeLayoutView is BottomNavigationView bottomNavigationView)
                        {
                            return bottomNavigationView;
                        }
                    }
                }
            }
            return null;
        }

        public void OnNavigationItemReselected(IMenuItem item)
        {
            if (_page?.CurrentPage?.Navigation != null && _page.CurrentPage.Navigation.NavigationStack.Count > 0)
            {
                if (_page is TabsPage tabsPage)
                {
                    tabsPage.OnPageReselected();
                }
                Device.BeginInvokeOnMainThread(async () => await _page.CurrentPage.Navigation.PopToRootAsync());
            }
        }
    }
}
