﻿using System;
using Bit.iOS.Autofill.Models;
using Foundation;
using UIKit;
using Bit.iOS.Core.Controllers;
using Bit.App.Resources;
using Bit.iOS.Core.Views;
using Bit.iOS.Autofill.Utilities;
using Bit.iOS.Core.Utilities;

namespace Bit.iOS.Autofill
{
    public partial class LoginSearchViewController : ExtendedUITableViewController
    {
        public LoginSearchViewController(IntPtr handle)
            : base(handle)
        { }

        public Context Context { get; set; }
        public CredentialProviderViewController CPViewController { get; set; }
        public bool FromList { get; set; }

        public async override void ViewDidLoad()
        {
            base.ViewDidLoad();
            NavItem.Title = AppResources.SearchVault;
            CancelBarButton.Title = AppResources.Cancel;
            SearchBar.Placeholder = AppResources.Search;
            SearchBar.BackgroundColor = SearchBar.BarTintColor = ThemeHelpers.ListHeaderBackgroundColor;
            if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0))
            {
#pragma warning disable XI0002 // Notifies you from using newer Apple APIs when targeting an older OS version
                SearchBar.SearchTextField.TextColor = ThemeHelpers.TextColor;
#pragma warning restore XI0002 // Notifies you from using newer Apple APIs when targeting an older OS version
            }
            
            if (!ThemeHelpers.LightTheme)
            {
                SearchBar.KeyboardAppearance = UIKeyboardAppearance.Dark;
            }

            TableView.RowHeight = UITableView.AutomaticDimension;
            TableView.EstimatedRowHeight = 44;
            TableView.Source = new TableSource(this);
            SearchBar.Delegate = new ExtensionSearchDelegate(TableView);
            await ((TableSource)TableView.Source).LoadItemsAsync(false, SearchBar.Text);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            SearchBar?.BecomeFirstResponder();
        }

        partial void CancelBarButton_Activated(UIBarButtonItem sender)
        {
            if(FromList)
            {
                DismissViewController(true, null);
            }
            else
            {
                CPViewController.CompleteRequest();
            }
        }

        partial void AddBarButton_Activated(UIBarButtonItem sender)
        {
            PerformSegue("loginAddFromSearchSegue", this);
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            if(segue.DestinationViewController is UINavigationController navController)
            {
                if(navController.TopViewController is LoginAddViewController addLoginController)
                {
                    addLoginController.Context = Context;
                    addLoginController.LoginSearchController = this;
                }
            }
        }

        public void DismissModal()
        {
            DismissViewController(true, async () =>
            {
                await ((TableSource)TableView.Source).LoadItemsAsync(false, SearchBar.Text);
                TableView.ReloadData();
            });
        }

        public class TableSource : ExtensionTableSource
        {
            private Context _context;
            private LoginSearchViewController _controller;

            public TableSource(LoginSearchViewController controller)
                : base(controller.Context, controller)
            {
                _context = controller.Context;
                _controller = controller;
            }

            public async override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                await AutofillHelpers.TableRowSelectedAsync(tableView, indexPath, this,
                    _controller.CPViewController, _controller, "loginAddFromSearchSegue");
            }
        }
    }
}
