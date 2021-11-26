﻿using System;
using System.Collections.Generic;
using System.Linq;
using Bit.App.Resources;
using Bit.iOS.Core.Views;
using Foundation;
using UIKit;
using Bit.iOS.Core.Utilities;
using Bit.iOS.Core.Models;
using System.Threading.Tasks;
using AuthenticationServices;
using Bit.Core.Abstractions;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using Bit.Core.Exceptions;

namespace Bit.iOS.Core.Controllers
{
    public abstract class LoginAddViewController : ExtendedUITableViewController
    {
        private ICipherService _cipherService;
        private IFolderService _folderService;
        private IStorageService _storageService;
        private IEnumerable<FolderView> _folders;

        public LoginAddViewController(IntPtr handle)
            : base(handle)
        { }

        public AppExtensionContext Context { get; set; }
        public FormEntryTableViewCell NameCell { get; set; } = new FormEntryTableViewCell(AppResources.Name);
        public FormEntryTableViewCell UsernameCell { get; set; } = new FormEntryTableViewCell(AppResources.Username);
        public FormEntryTableViewCell PasswordCell { get; set; } = new FormEntryTableViewCell(AppResources.Password);
        public UITableViewCell GeneratePasswordCell { get; set; } = new ExtendedUITableViewCell(
            UITableViewCellStyle.Subtitle, "GeneratePasswordCell");
        public FormEntryTableViewCell UriCell { get; set; } = new FormEntryTableViewCell(AppResources.URI);
        public SwitchTableViewCell FavoriteCell { get; set; } = new SwitchTableViewCell(AppResources.Favorite);
        public FormEntryTableViewCell NotesCell { get; set; } = new FormEntryTableViewCell(
            useTextView: true, height: 180);
        public PickerTableViewCell FolderCell { get; set; } = new PickerTableViewCell(AppResources.Folder);

        public abstract UINavigationItem BaseNavItem { get; }
        public abstract UIBarButtonItem BaseCancelButton { get; }
        public abstract UIBarButtonItem BaseSaveButton { get; }
        public abstract Action<string> Success { get; }

        public override void ViewDidLoad()
        {
            _cipherService = ServiceContainer.Resolve<ICipherService>("cipherService");
            _folderService = ServiceContainer.Resolve<IFolderService>("folderService");
            _storageService = ServiceContainer.Resolve<IStorageService>("storageService");

            BaseNavItem.Title = AppResources.AddItem;
            BaseCancelButton.Title = AppResources.Cancel;
            BaseSaveButton.Title = AppResources.Save;
            View.BackgroundColor = ThemeHelpers.BackgroundColor;

            NameCell.TextField.Text = Context?.Uri?.Host ?? string.Empty;
            NameCell.TextField.ReturnKeyType = UIReturnKeyType.Next;
            NameCell.TextField.ShouldReturn += (UITextField tf) =>
            {
                UsernameCell.TextField.BecomeFirstResponder();
                return true;
            };

            UsernameCell.TextField.AutocapitalizationType = UITextAutocapitalizationType.None;
            UsernameCell.TextField.AutocorrectionType = UITextAutocorrectionType.No;
            UsernameCell.TextField.SpellCheckingType = UITextSpellCheckingType.No;
            UsernameCell.TextField.ReturnKeyType = UIReturnKeyType.Next;
            UsernameCell.TextField.ShouldReturn += (UITextField tf) =>
            {
                PasswordCell.TextField.BecomeFirstResponder();
                return true;
            };

            PasswordCell.TextField.SecureTextEntry = true;
            PasswordCell.TextField.ReturnKeyType = UIReturnKeyType.Next;
            PasswordCell.TextField.ShouldReturn += (UITextField tf) =>
            {
                UriCell.TextField.BecomeFirstResponder();
                return true;
            };

            GeneratePasswordCell.TextLabel.Text = AppResources.GeneratePassword;
            GeneratePasswordCell.TextLabel.TextColor = GeneratePasswordCell.TextLabel.TintColor =
                ThemeHelpers.TextColor;
            GeneratePasswordCell.Accessory = UITableViewCellAccessory.DisclosureIndicator;

            UriCell.TextField.Text = Context?.UrlString ?? string.Empty;
            UriCell.TextField.KeyboardType = UIKeyboardType.Url;
            UriCell.TextField.ReturnKeyType = UIReturnKeyType.Next;
            UriCell.TextField.ShouldReturn += (UITextField tf) =>
            {
                NotesCell.TextView.BecomeFirstResponder();
                return true;
            };

            _folders = _folderService.GetAllDecryptedAsync().GetAwaiter().GetResult();
            var folderNames = _folders.Select(s => s.Name).OrderBy(s => s).ToList();
            folderNames.Insert(0, AppResources.FolderNone);
            FolderCell.Items = folderNames;

            TableView.RowHeight = UITableView.AutomaticDimension;
            TableView.EstimatedRowHeight = 70;
            TableView.Source = new TableSource(this);
            TableView.AllowsSelection = true;

            base.ViewDidLoad();
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
        }

        protected async Task SaveAsync()
        {
            /*
            if (!_connectivity.IsConnected)
            {
                AlertNoConnection();
                return;
            }
            */

            if (string.IsNullOrWhiteSpace(PasswordCell?.TextField?.Text))
            {
                DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired,
                    AppResources.Password), AppResources.Ok);
                return;
            }

            if (string.IsNullOrWhiteSpace(NameCell?.TextField?.Text))
            {
                DisplayAlert(AppResources.AnErrorHasOccurred, string.Format(AppResources.ValidationFieldRequired,
                    AppResources.Name), AppResources.Ok);
                return;
            }

            var cipher = new CipherView
            {
                Name = NameCell.TextField.Text,
                Notes = string.IsNullOrWhiteSpace(NotesCell?.TextView?.Text) ? null : NotesCell.TextView.Text,
                Favorite = FavoriteCell.Switch.On,
                FolderId = FolderCell.SelectedIndex == 0 ?
                    null : _folders.ElementAtOrDefault(FolderCell.SelectedIndex - 1)?.Id,
                Type = Bit.Core.Enums.CipherType.Login,
                Login = new LoginView
                {
                    Uris = null,
                    Username = string.IsNullOrWhiteSpace(UsernameCell?.TextField?.Text) ?
                        null : UsernameCell.TextField.Text,
                    Password = string.IsNullOrWhiteSpace(PasswordCell.TextField.Text) ?
                        null : PasswordCell.TextField.Text,
                }
            };

            if (!string.IsNullOrWhiteSpace(UriCell?.TextField?.Text))
            {
                cipher.Login.Uris = new List<LoginUriView>
                {
                    new LoginUriView
                    {
                        Uri = UriCell.TextField.Text
                    }
                };
            }

            var loadingAlert = Dialogs.CreateLoadingAlert(AppResources.Saving);
            PresentViewController(loadingAlert, true, null);
            try
            {
                var cipherDomain = await _cipherService.EncryptAsync(cipher);
                await _cipherService.SaveWithServerAsync(cipherDomain);
                await loadingAlert.DismissViewControllerAsync(true);
                await _storageService.SaveAsync(Bit.Core.Constants.ClearCiphersCacheKey, true);
                if (await ASHelpers.IdentitiesCanIncremental())
                {
                    var identity = await ASHelpers.GetCipherIdentityAsync(cipherDomain.Id);
                    if (identity != null)
                    {
                        await ASCredentialIdentityStore.SharedStore.SaveCredentialIdentitiesAsync(
                            new ASPasswordCredentialIdentity[] { identity });
                    }
                }
                else
                {
                    await ASHelpers.ReplaceAllIdentities();
                }
                Success(cipherDomain.Id);
            }
            catch (ApiException e)
            {
                if (e?.Error != null)
                {
                    DisplayAlert(AppResources.AnErrorHasOccurred, e.Error.GetSingleMessage(), AppResources.Ok);
                }
            }
        }

        public void DisplayAlert(string title, string message, string accept)
        {
            var alert = Dialogs.CreateAlert(title, message, accept);
            PresentViewController(alert, true, null);
        }

        private void AlertNoConnection()
        {
            DisplayAlert(AppResources.InternetConnectionRequiredTitle,
                AppResources.InternetConnectionRequiredMessage, AppResources.Ok);
        }

        public class TableSource : ExtendedUITableViewSource
        {
            private LoginAddViewController _controller;

            public TableSource(LoginAddViewController controller)
            {
                _controller = controller;
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                if (indexPath.Section == 0)
                {
                    if (indexPath.Row == 0)
                    {
                        return _controller.NameCell;
                    }
                    else if (indexPath.Row == 1)
                    {
                        return _controller.UsernameCell;
                    }
                    else if (indexPath.Row == 2)
                    {
                        return _controller.PasswordCell;
                    }
                    else if (indexPath.Row == 3)
                    {
                        return _controller.GeneratePasswordCell;
                    }
                }
                else if (indexPath.Section == 1)
                {
                    return _controller.UriCell;
                }
                else if (indexPath.Section == 2)
                {
                    if (indexPath.Row == 0)
                    {
                        return _controller.FolderCell;
                    }
                    else if (indexPath.Row == 1)
                    {
                        return _controller.FavoriteCell;
                    }
                }
                else if (indexPath.Section == 3)
                {
                    return _controller.NotesCell;
                }

                return new ExtendedUITableViewCell();
            }

            public override nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
            {
                return UITableView.AutomaticDimension;
            }

            public override nint NumberOfSections(UITableView tableView)
            {
                return 4;
            }

            public override nint RowsInSection(UITableView tableview, nint section)
            {
                if (section == 0)
                {
                    return 4;
                }
                else if (section == 1)
                {
                    return 1;
                }
                else if (section == 2)
                {
                    return 2;
                }
                else
                {
                    return 1;
                }
            }

            public override nfloat GetHeightForHeader(UITableView tableView, nint section)
            {
                return section == 0 || section == 3 ? UITableView.AutomaticDimension : 0.00001f;
            }

            public override string TitleForHeader(UITableView tableView, nint section)
            {
                if (section == 0)
                {
                    return AppResources.ItemInformation;
                }
                else if (section == 3)
                {
                    return AppResources.Notes;
                }

                return string.Empty;
            }

            public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                tableView.DeselectRow(indexPath, true);
                tableView.EndEditing(true);

                if (indexPath.Section == 0 && indexPath.Row == 3)
                {
                    _controller.PerformSegue("passwordGeneratorSegue", this);
                }

                var cell = tableView.CellAt(indexPath);
                if (cell == null)
                {
                    return;
                }

                var selectableCell = cell as ISelectable;
                if (selectableCell != null)
                {
                    selectableCell.Select();
                }
            }
        }
    }
}
