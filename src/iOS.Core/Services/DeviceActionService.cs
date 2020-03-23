﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Models.View;
using Bit.iOS.Core.Utilities;
using Bit.iOS.Core.Views;
using CoreGraphics;
using Foundation;
using LocalAuthentication;
using MobileCoreServices;
using Photos;
using Plugin.Fingerprint;
using UIKit;
using Xamarin.Forms;

namespace Bit.iOS.Core.Services
{
    public class DeviceActionService : IDeviceActionService
    {
        private readonly IStorageService _storageService;
        private readonly IMessagingService _messagingService;
        private Toast _toast;
        private UIAlertController _progressAlert;
        private string _userAgent;

        public DeviceActionService(
            IStorageService storageService,
            IMessagingService messagingService)
        {
            _storageService = storageService;
            _messagingService = messagingService;
        }

        public string DeviceUserAgent
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_userAgent))
                {
                    _userAgent = $"Bitwarden_Mobile/{Xamarin.Essentials.AppInfo.VersionString} " +
                        $"(iOS {UIDevice.CurrentDevice.SystemVersion}; Model {UIDevice.CurrentDevice.Model})";
                }
                return _userAgent;
            }
        }

        public DeviceType DeviceType => DeviceType.iOS;

        public bool LaunchApp(string appName)
        {
            throw new NotImplementedException();
        }

        public void Toast(string text, bool longDuration = false)
        {
            if(!_toast?.Dismissed ?? false)
            {
                _toast.Dismiss(false);
            }
            _toast = new Toast(text)
            {
                Duration = TimeSpan.FromSeconds(longDuration ? 5 : 3)
            };
            if(TabBarVisible())
            {
                _toast.BottomMargin = 55;
            }
            _toast.Show();
            _toast.DismissCallback = () =>
            {
                _toast?.Dispose();
                _toast = null;
            };
        }

        public Task ShowLoadingAsync(string text)
        {
            if(_progressAlert != null)
            {
                HideLoadingAsync().GetAwaiter().GetResult();
            }

            var result = new TaskCompletionSource<int>();

            var loadingIndicator = new UIActivityIndicatorView(new CGRect(10, 5, 50, 50));
            loadingIndicator.HidesWhenStopped = true;
            loadingIndicator.ActivityIndicatorViewStyle = UIActivityIndicatorViewStyle.Gray;
            loadingIndicator.StartAnimating();

            _progressAlert = UIAlertController.Create(null, text, UIAlertControllerStyle.Alert);
            _progressAlert.View.TintColor = UIColor.Black;
            _progressAlert.View.Add(loadingIndicator);

            var vc = GetPresentedViewController();
            vc?.PresentViewController(_progressAlert, false, () => result.TrySetResult(0));
            return result.Task;
        }

        public Task HideLoadingAsync()
        {
            var result = new TaskCompletionSource<int>();
            if(_progressAlert == null)
            {
                result.TrySetResult(0);
            }
            _progressAlert.DismissViewController(false, () => result.TrySetResult(0));
            _progressAlert.Dispose();
            _progressAlert = null;
            return result.Task;
        }

        public bool OpenFile(byte[] fileData, string id, string fileName)
        {
            var filePath = Path.Combine(GetTempPath(), fileName);
            File.WriteAllBytes(filePath, fileData);
            var url = NSUrl.FromFilename(filePath);
            var viewer = UIDocumentInteractionController.FromUrl(url);
            var controller = GetVisibleViewController();
            var rect = UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad ?
                new CGRect(100, 5, 320, 320) : controller.View.Frame;
            return viewer.PresentOpenInMenu(rect, controller.View, true);
        }

        public bool CanOpenFile(string fileName)
        {
            // Not sure of a way to check this ahead of time on iOS
            return true;
        }

        public bool SaveFile(byte[] fileData, string id, string fileName, string contentUri)
        {
            // OpenFile behavior is appropriate here as iOS prompts to save file
            return OpenFile(fileData, id, fileName);
        }

        public async Task ClearCacheAsync()
        {
            var url = new NSUrl(GetTempPath());
            var tmpFiles = NSFileManager.DefaultManager.GetDirectoryContent(url, null,
                NSDirectoryEnumerationOptions.SkipsHiddenFiles, out NSError error);
            if(error == null && tmpFiles.Length > 0)
            {
                foreach(var item in tmpFiles)
                {
                    NSFileManager.DefaultManager.Remove(item, out NSError itemError);
                }
            }
            await _storageService.SaveAsync(Bit.Core.Constants.LastFileCacheClearKey, DateTime.UtcNow);
        }

        public Task SelectFileAsync()
        {
            var controller = GetVisibleViewController();
            var picker = new UIDocumentMenuViewController(new string[] { UTType.Data }, UIDocumentPickerMode.Import);
            picker.AddOption(AppResources.Camera, UIImage.FromBundle("camera"), UIDocumentMenuOrder.First, () =>
            {
                var imagePicker = new UIImagePickerController
                {
                    SourceType = UIImagePickerControllerSourceType.Camera
                };
                imagePicker.FinishedPickingMedia += ImagePicker_FinishedPickingMedia;
                imagePicker.Canceled += ImagePicker_Canceled;
                controller.PresentModalViewController(imagePicker, true);
            });
            picker.AddOption(AppResources.Photos, UIImage.FromBundle("photo"), UIDocumentMenuOrder.First, () =>
            {
                var imagePicker = new UIImagePickerController
                {
                    SourceType = UIImagePickerControllerSourceType.PhotoLibrary
                };
                imagePicker.FinishedPickingMedia += ImagePicker_FinishedPickingMedia;
                imagePicker.Canceled += ImagePicker_Canceled;
                controller.PresentModalViewController(imagePicker, true);
            });
            picker.DidPickDocumentPicker += (sender, e) =>
            {
                if(SystemMajorVersion() < 11)
                {
                    e.DocumentPicker.DidPickDocument += DocumentPicker_DidPickDocument;
                }
                else
                {
                    e.DocumentPicker.Delegate = new PickerDelegate(this);
                }
                controller.PresentViewController(e.DocumentPicker, true, null);
            };
            var root = UIApplication.SharedApplication.KeyWindow.RootViewController;
            if(picker.PopoverPresentationController != null && root != null)
            {
                picker.PopoverPresentationController.SourceView = root.View;
                picker.PopoverPresentationController.SourceRect = root.View.Bounds;
            }
            controller.PresentViewController(picker, true, null);
            return Task.FromResult(0);
        }

        public Task<string> DisplayPromptAync(string title = null, string description = null,
            string text = null, string okButtonText = null, string cancelButtonText = null,
            bool numericKeyboard = false, bool autofocus = true)
        {
            var result = new TaskCompletionSource<string>();
            var alert = UIAlertController.Create(title ?? string.Empty, description, UIAlertControllerStyle.Alert);
            UITextField input = null;
            okButtonText = okButtonText ?? AppResources.Ok;
            cancelButtonText = cancelButtonText ?? AppResources.Cancel;
            alert.AddAction(UIAlertAction.Create(cancelButtonText, UIAlertActionStyle.Cancel, x =>
            {
                result.TrySetResult(null);
            }));
            alert.AddAction(UIAlertAction.Create(okButtonText, UIAlertActionStyle.Default, x =>
            {
                result.TrySetResult(input.Text ?? string.Empty);
            }));
            alert.AddTextField(x =>
            {
                input = x;
                input.Text = text ?? string.Empty;
                if(numericKeyboard)
                {
                    input.KeyboardType = UIKeyboardType.NumberPad;
                }
                if(!ThemeHelpers.LightTheme)
                {
                    input.KeyboardAppearance = UIKeyboardAppearance.Dark;
                }
            });
            var vc = GetPresentedViewController();
            vc?.PresentViewController(alert, true, null);
            return result.Task;
        }

        public void RateApp()
        {
            string uri = null;
            var appId = "1502262449";
            if (SystemMajorVersion() < 11)
            {
                uri = "itms-apps://itunes.apple.com/WebObjects/MZStore.woa/wa/viewContentsUserReviews" +
                   $"?id={appId}&onlyLatestVersion=true&pageNumber=0&sortOrdering=1&type=Purple+Software";
            }
            else
            {
                uri = $"itms-apps://itunes.apple.com/us/app/id{appId}?action=write-review";
            }
            Device.OpenUri(new Uri(uri));
        }

        public bool SupportsFaceBiometric()
        {
            if(SystemMajorVersion() < 11)
            {
                return false;
            }
            using(var context = new LAContext())
            {
                if(!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out var e))
                {
                    return false;
                }
                return context.BiometryType == LABiometryType.FaceId;
            }
        }

        public Task<bool> SupportsFaceBiometricAsync()
        {
            return Task.FromResult(SupportsFaceBiometric());
        }

        public async Task<bool> BiometricAvailableAsync()
        {
            try
            {
                return await CrossFingerprint.Current.IsAvailableAsync();
            }
            catch
            {
                return false;
            }
        }

        public bool UseNativeBiometric()
        {
            return false;
        }

        public Task<bool> AuthenticateBiometricAsync(string text = null)
        {
            throw new NotSupportedException();
        }

        public bool SupportsNfc()
        {
            return CoreNFC.NFCNdefReaderSession.ReadingAvailable;
        }

        public bool SupportsCamera()
        {
            return true;
        }

        public bool SupportsAutofillService()
        {
            return true;
        }

        public int SystemMajorVersion()
        {
            var versionParts = UIDevice.CurrentDevice.SystemVersion.Split('.');
            if(versionParts.Length > 0 && int.TryParse(versionParts[0], out var version))
            {
                return version;
            }
            // unable to determine version
            return -1;
        }

        public string SystemModel()
        {
            return UIDevice.CurrentDevice.Model;
        }

        public Task<string> DisplayAlertAsync(string title, string message, string cancel, params string[] buttons)
        {
            var result = new TaskCompletionSource<string>();
            var alert = UIAlertController.Create(title ?? string.Empty, message, UIAlertControllerStyle.Alert);
            if(!string.IsNullOrWhiteSpace(cancel))
            {
                alert.AddAction(UIAlertAction.Create(cancel, UIAlertActionStyle.Cancel, x =>
                {
                    result.TrySetResult(cancel);
                }));
            }
            foreach(var button in buttons)
            {
                alert.AddAction(UIAlertAction.Create(button, UIAlertActionStyle.Default, x =>
                {
                    result.TrySetResult(button);
                }));
            }
            var vc = GetPresentedViewController();
            vc?.PresentViewController(alert, true, null);
            return result.Task;
        }

        public void Autofill(CipherView cipher)
        {
            throw new NotImplementedException();
        }

        public void CloseAutofill()
        {
            throw new NotImplementedException();
        }

        public void Background()
        {
            throw new NotImplementedException();
        }

        public bool AutofillAccessibilityServiceRunning()
        {
            throw new NotImplementedException();
        }

        public bool AutofillServiceEnabled()
        {
            throw new NotImplementedException();
        }

        public string GetBuildNumber()
        {
            return NSBundle.MainBundle.InfoDictionary["CFBundleVersion"].ToString();
        }

        public void OpenAccessibilitySettings()
        {
            throw new NotImplementedException();
        }

        public void OpenAutofillSettings()
        {
            throw new NotImplementedException();
        }

        public bool UsingDarkTheme()
        {
            try
            {
                if(SystemMajorVersion() > 12)
                {
                    return UIScreen.MainScreen.TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark;
                }
            }
            catch { }
            return false;
        }

        private void ImagePicker_FinishedPickingMedia(object sender, UIImagePickerMediaPickedEventArgs e)
        {
            if(sender is UIImagePickerController picker)
            {
                string fileName = null;
                if(e.Info.TryGetValue(UIImagePickerController.ReferenceUrl, out NSObject urlObj))
                {
                    var result = PHAsset.FetchAssets(new NSUrl[] { (urlObj as NSUrl) }, null);
                    fileName = result?.firstObject?.ValueForKey(new NSString("filename"))?.ToString();
                }
                fileName = fileName ?? $"photo_{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.jpg";
                var lowerFilename = fileName?.ToLowerInvariant();
                byte[] data;
                if(lowerFilename != null && (lowerFilename.EndsWith(".jpg") || lowerFilename.EndsWith(".jpeg")))
                {
                    using(var imageData = e.OriginalImage.AsJPEG())
                    {
                        data = new byte[imageData.Length];
                        System.Runtime.InteropServices.Marshal.Copy(imageData.Bytes, data, 0,
                            Convert.ToInt32(imageData.Length));
                    }
                }
                else
                {
                    using(var imageData = e.OriginalImage.AsPNG())
                    {
                        data = new byte[imageData.Length];
                        System.Runtime.InteropServices.Marshal.Copy(imageData.Bytes, data, 0,
                            Convert.ToInt32(imageData.Length));
                    }
                }
                SelectFileResult(data, fileName);
                picker.DismissViewController(true, null);
            }
        }

        private void ImagePicker_Canceled(object sender, EventArgs e)
        {
            if(sender is UIImagePickerController picker)
            {
                picker.DismissViewController(true, null);
            }
        }

        private void DocumentPicker_DidPickDocument(object sender, UIDocumentPickedEventArgs e)
        {
            PickedDocument(e.Url);
        }

        private void SelectFileResult(byte[] data, string fileName)
        {
            _messagingService.Send("selectFileResult", new Tuple<byte[], string>(data, fileName));
        }

        private UIViewController GetVisibleViewController(UIViewController controller = null)
        {
            controller = controller ?? UIApplication.SharedApplication.KeyWindow.RootViewController;
            if(controller.PresentedViewController == null)
            {
                return controller;
            }
            if(controller.PresentedViewController is UINavigationController)
            {
                return ((UINavigationController)controller.PresentedViewController).VisibleViewController;
            }
            if(controller.PresentedViewController is UITabBarController)
            {
                return ((UITabBarController)controller.PresentedViewController).SelectedViewController;
            }
            return GetVisibleViewController(controller.PresentedViewController);
        }

        private UIViewController GetPresentedViewController()
        {
            var window = UIApplication.SharedApplication.KeyWindow;
            var vc = window.RootViewController;
            while(vc.PresentedViewController != null)
            {
                vc = vc.PresentedViewController;
            }
            return vc;
        }

        private bool TabBarVisible()
        {
            var vc = GetPresentedViewController();
            return vc != null && (vc is UITabBarController ||
                (vc.ChildViewControllers?.Any(c => c is UITabBarController) ?? false));
        }

        // ref:  //https://developer.xamarin.com/guides/ios/application_fundamentals/working_with_the_file_system/
        public string GetTempPath()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents, "..", "tmp");
        }

        public void PickedDocument(NSUrl url)
        {
            url.StartAccessingSecurityScopedResource();
            var doc = new UIDocument(url);
            var fileName = doc.LocalizedName;
            if(string.IsNullOrWhiteSpace(fileName))
            {
                var path = doc.FileUrl?.ToString();
                if(path != null)
                {
                    path = WebUtility.UrlDecode(path);
                    var split = path.LastIndexOf('/');
                    fileName = path.Substring(split + 1);
                }
            }
            var fileCoordinator = new NSFileCoordinator();
            fileCoordinator.CoordinateRead(url, NSFileCoordinatorReadingOptions.WithoutChanges,
                out NSError error, (u) =>
                {
                    var data = NSData.FromUrl(u).ToArray();
                    SelectFileResult(data, fileName ?? "unknown_file_name");
                });
            url.StopAccessingSecurityScopedResource();
        }

        public bool AutofillAccessibilityOverlayPermitted()
        {
            throw new NotImplementedException();
        }

        public void OpenAccessibilityOverlayPermissionSettings()
        {
            throw new NotImplementedException();
        }

        public class PickerDelegate : UIDocumentPickerDelegate
        {
            private readonly DeviceActionService _deviceActionService;

            public PickerDelegate(DeviceActionService deviceActionService)
            {
                _deviceActionService = deviceActionService;
            }

            public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
            {
                _deviceActionService.PickedDocument(url);
            }
        }
    }
}
