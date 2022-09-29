using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class CipherDetailsPage : BaseContentPage
    {
        private readonly IBroadcasterService _broadcasterService;
        private readonly ISyncService _syncService;
        private CipherDetailsPageViewModel _vm;

        public CipherDetailsPage(string cipherId)
        {
            InitializeComponent();
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            _syncService = ServiceContainer.Resolve<ISyncService>("syncService");
            _vm = BindingContext as CipherDetailsPageViewModel;
            _vm.Page = this;
            _vm.CipherId = cipherId;
            SetActivityIndicator(_mainContent);

            if (Device.RuntimePlatform == Device.iOS)
            {
                _absLayout.Children.Remove(_fab);
                ToolbarItems.Add(_closeItem);
                ToolbarItems.Add(_editItem);
                ToolbarItems.Add(_moreItem);
            }
            else
            {
                _mainLayout.Padding = new Thickness(0, 0, 0, 75);
                // Cozy customization, disable attachment
                // Disable attachment as they are not implemented in Cozy Stack
                /*
                ToolbarItems.Add(_attachmentsItem);
                //*/
                ToolbarItems.Add(_deleteItem);
            }
        }

        public CipherDetailsPageViewModel ViewModel => _vm;

        public void UpdateCipherId(string cipherId)
        {
            _vm.CipherId = cipherId;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_syncService.SyncInProgress)
            {
                IsBusy = true;
            }

            _broadcasterService.Subscribe(nameof(CipherDetailsPage), async (message) =>
            {
                try
                {
                    if (message.Command == "syncStarted")
                    {
                        Device.BeginInvokeOnMainThread(() => IsBusy = true);
                    }
                    else if (message.Command == "syncCompleted")
                    {
                        await Task.Delay(500);
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            IsBusy = false;
                            if (message.Data is Dictionary<string, object> data && data.ContainsKey("successfully"))
                            {
                                var success = data["successfully"] as bool?;
                                if (success.GetValueOrDefault())
                                {
                                    var task = _vm.LoadAsync(() => AdjustToolbar());
                                }
                            }
                        });
                    }
                    else if (message.Command == "selectSaveFileResult")
                    {
                        Device.BeginInvokeOnMainThread(() =>
                        {
                            var data = message.Data as Tuple<string, string>;
                            if (data == null)
                            {
                                return;
                            }
                            _vm.SaveFileSelected(data.Item1, data.Item2);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LoggerHelper.LogEvenIfCantBeResolved(ex);
                }
            });
            await LoadOnAppearedAsync(_scrollView, true, async () =>
            {
                var success = await _vm.LoadAsync(() => AdjustToolbar());
                if (!success)
                {
                    await Navigation.PopModalAsync();
                }
            }, _mainContent);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            IsBusy = false;
            _vm.StopCiphersTotpTick().FireAndForget();
            _broadcasterService.Unsubscribe(nameof(CipherDetailsPage));
        }

        private async void PasswordHistory_Tapped(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PushModalAsync(new NavigationPage(new PasswordHistoryPage(_vm.CipherId)));
            }
        }

        private async void EditToolbarItem_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                if (_vm.IsDeleted)
                {
                    if (await _vm.RestoreAsync())
                    {
                        await Navigation.PopModalAsync();
                    }
                }
                else
                {
                    if (!await _vm.PromptPasswordAsync())
                    {
                        return;
                    }
                    // Cozy customization, synchronize organization and collection ids after a ShareCipher action
                    /*
                    await Navigation.PushModalAsync(new NavigationPage(new CipherAddEditPage(_vm.CipherId)));
                    /*/
                    var page = new CipherAddEditPage(_vm.CipherId);
                    page.Disappearing += async (object sender2, EventArgs e2) =>
                    {
                        _vm.Cipher.OrganizationId = page.GetSavedOrganizationId();
                        _vm.Cipher.CollectionIds = page.GetSavedCollectionId();
                        await _vm.UpdateOrganizationName();
                    };
                    await Navigation.PushModalAsync(new NavigationPage(page));
                    //*/
                }
            }
        }

        private void EditButton_Clicked(object sender, System.EventArgs e)
        {
            EditToolbarItem_Clicked(sender, e);
        }

        private async void Attachments_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                if (!await _vm.PromptPasswordAsync())
                {
                    return;
                }
                var page = new AttachmentsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }

        private async void Share_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                if (!await _vm.PromptPasswordAsync())
                {
                    return;
                }
                var page = new SharePage(_vm.CipherId);
                // Cozy customization, synchronize organization and collection ids after a ShareCipher action
                //*
                page.OnShared += async (object sender2, OnSharedEventArgs args) =>
                {
                    _vm.Cipher.OrganizationId = args.OrganizationId;
                    _vm.Cipher.CollectionIds = args.CollectionIds;
                    await _vm.UpdateOrganizationName();
                };
                //*/
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }

        private async void Delete_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                if (!await _vm.PromptPasswordAsync())
                {
                    return;
                }
                if (await _vm.DeleteAsync())
                {
                    await Navigation.PopModalAsync();
                }
            }
        }

        private async void Collections_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                if (!await _vm.PromptPasswordAsync())
                {
                    return;
                }
                var page = new CollectionsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }

        private async void Clone_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                if (!await _vm.PromptPasswordAsync())
                {
                    return;
                }
                var page = new CipherAddEditPage(_vm.CipherId, cloneMode: true, cipherDetailsPage: this);
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }

        private async void More_Clicked(object sender, System.EventArgs e)
        {
            if (!DoOnce())
            {
                return;
            }

            // Cozy customization, disable attachment
            // Disable attachment as they are not implemented in Cozy Stack
            /*
            var options = new List<string> {AppResources.Attachments};
            /*/
            var options = new List<string> { };
            //*/
            if (_vm.Cipher.OrganizationId == null)
            {
                options.Add(AppResources.Clone);
                options.Add(AppResources.MoveToOrganization);
            }
            else
            {
                // Cozy customization, replace collection action by move action
                // Collections and Organizations are merged into a single concept (folders) at Cozy
                // so there is no need to use a different action
                /*
                options.Add(AppResources.Collections);
                /*/
                options.Add(AppResources.MoveToOrganization);
                //*/
            }

            var selection = await DisplayActionSheet(AppResources.Options, AppResources.Cancel,
                AppResources.Delete, options.ToArray());

            if (!await _vm.PromptPasswordAsync())
            {
                return;
            }

            if (selection == AppResources.Delete)
            {
                if (await _vm.DeleteAsync())
                {
                    await Navigation.PopModalAsync();
                }
            }
            else if (selection == AppResources.Attachments)
            {
                var page = new AttachmentsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
            else if (selection == AppResources.Collections)
            {
                var page = new CollectionsPage(_vm.CipherId);
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
            else if (selection == AppResources.MoveToOrganization)
            {
                var page = new SharePage(_vm.CipherId);
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
            else if (selection == AppResources.Clone)
            {
                var page = new CipherAddEditPage(_vm.CipherId, cloneMode: true, cipherDetailsPage: this);
                await Navigation.PushModalAsync(new NavigationPage(page));
            }
        }

        private async void Close_Clicked(object sender, System.EventArgs e)
        {
            if (DoOnce())
            {
                await Navigation.PopModalAsync();
            }
        }

        private void AdjustToolbar()
        {
            if (_vm.Cipher == null)
            {
                return;
            }
            _editItem.Text = _vm.Cipher.IsDeleted ? AppResources.Restore :
                AppResources.Edit;
            if (_vm.Cipher.IsDeleted)
            {
                _absLayout.Children.Remove(_fab);
            }
            if (Device.RuntimePlatform != Device.Android)
            {
                return;
            }
            if (_vm.Cipher.OrganizationId == null)
            {
                // Cozy customization, remove collection action
                // Collections and Organizations are merged into a single concept (folders) at Cozy
                // so there is no need to use a different action
                /*
                if (ToolbarItems.Contains(_collectionsItem))
                {
                    ToolbarItems.Remove(_collectionsItem);
                }
                //*/
                if (!ToolbarItems.Contains(_cloneItem))
                {
                    ToolbarItems.Insert(1, _cloneItem);
                }
                if (!ToolbarItems.Contains(_shareItem))
                {
                    ToolbarItems.Insert(2, _shareItem);
                }
            }
            else
            {
                if (ToolbarItems.Contains(_cloneItem))
                {
                    ToolbarItems.Remove(_cloneItem);
                }
                // Cozy customization, remove collection action
                // Collections and Organizations are merged into a single concept (folders) at Cozy
                // so there is no need to use a different action
                /*
                if (ToolbarItems.Contains(_shareItem))
                {
                    ToolbarItems.Remove(_shareItem);
                }
                if (!ToolbarItems.Contains(_collectionsItem))
                {
                    ToolbarItems.Insert(1, _collectionsItem);
                }
                /*/
                if (!ToolbarItems.Contains(_shareItem))
                {
                    ToolbarItems.Insert(1, _shareItem);
                }
                //*/
            }
            if (_vm.Cipher.IsDeleted && !ToolbarItems.Contains(_editItem))
            {
                ToolbarItems.Insert(1, _editItem);
            }
        }
    }
}
