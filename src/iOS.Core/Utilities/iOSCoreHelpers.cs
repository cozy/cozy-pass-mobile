using System;
using System.IO;
using System.Threading.Tasks;
using Bit.App.Abstractions;
using Bit.App.Models;
using Bit.App.Resources;
using Bit.App.Services;
using Bit.App.Utilities;
using Bit.Core.Abstractions;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.iOS.Core.Services;
using CoreNFC;
using Foundation;
using UIKit;

namespace Bit.iOS.Core.Utilities
{
    public static class iOSCoreHelpers
    {
        public static string AppId = "io.cozy.pass.mobile";
        public static string AppAutofillId = "io.cozy.pass.mobile.autofill";
        public static string AppExtensionId = "io.cozy.pass.mobile.find-login-action-extension";
        public static string AppGroupId = "group.io.cozy.pass.mobile";
        public static string AccessGroup = "3AKXFMV43J.io.cozy.pass.mobile";

        public static void RegisterAppCenter()
        {
            // Cozy customization, disable "AppCenter" functionality
            // We do not use it at Cozy
            /*
            var appCenterHelper = new AppCenterHelper(
                ServiceContainer.Resolve<IAppIdService>("appIdService"),
                ServiceContainer.Resolve<IUserService>("userService"));
            var appCenterTask = appCenterHelper.InitAsync();
            //*/
        }

        public static void RegisterLocalServices()
        {
            if (ServiceContainer.Resolve<ILogService>("logService", true) == null)
            {
                ServiceContainer.Register<ILogService>("logService", new ConsoleLogService());
            }

            var preferencesStorage = new PreferencesStorageService(AppGroupId);
            var appGroupContainer = new NSFileManager().GetContainerUrl(AppGroupId);
            var liteDbStorage = new LiteDbStorageService(
                Path.Combine(appGroupContainer.Path, "Library", "bitwarden.db"));
            var localizeService = new LocalizeService();
            var broadcasterService = new BroadcasterService();
            var messagingService = new MobileBroadcasterMessagingService(broadcasterService);
            var i18nService = new MobileI18nService(localizeService.GetCurrentCultureInfo());
            var secureStorageService = new KeyChainStorageService(AppId, AccessGroup,
                () => ServiceContainer.Resolve<IAppIdService>("appIdService").GetAppIdAsync());
            var cryptoPrimitiveService = new CryptoPrimitiveService();
            var mobileStorageService = new MobileStorageService(preferencesStorage, liteDbStorage);
            var deviceActionService = new DeviceActionService(mobileStorageService, messagingService);
            var clipboardService = new ClipboardService(mobileStorageService);
            var platformUtilsService = new MobilePlatformUtilsService(deviceActionService, clipboardService, messagingService,
                broadcasterService);
            var biometricService = new BiometricService(mobileStorageService);
            var cryptoFunctionService = new PclCryptoFunctionService(cryptoPrimitiveService);
            var cryptoService = new CryptoService(mobileStorageService, secureStorageService, cryptoFunctionService);
            var passwordRepromptService = new MobilePasswordRepromptService(platformUtilsService, cryptoService);

            ServiceContainer.Register<IBroadcasterService>("broadcasterService", broadcasterService);
            ServiceContainer.Register<IMessagingService>("messagingService", messagingService);
            ServiceContainer.Register<ILocalizeService>("localizeService", localizeService);
            ServiceContainer.Register<II18nService>("i18nService", i18nService);
            ServiceContainer.Register<ICryptoPrimitiveService>("cryptoPrimitiveService", cryptoPrimitiveService);
            ServiceContainer.Register<IStorageService>("storageService", mobileStorageService);
            ServiceContainer.Register<IStorageService>("secureStorageService", secureStorageService);
            ServiceContainer.Register<IDeviceActionService>("deviceActionService", deviceActionService);
            ServiceContainer.Register<IClipboardService>("clipboardService", clipboardService);
            ServiceContainer.Register<IPlatformUtilsService>("platformUtilsService", platformUtilsService);
            ServiceContainer.Register<IBiometricService>("biometricService", biometricService);
            ServiceContainer.Register<ICryptoFunctionService>("cryptoFunctionService", cryptoFunctionService);
            ServiceContainer.Register<ICryptoService>("cryptoService", cryptoService);
            ServiceContainer.Register<IPasswordRepromptService>("passwordRepromptService", passwordRepromptService);
        }

        public static void Bootstrap(Func<Task> postBootstrapFunc = null)
        {
            (ServiceContainer.Resolve<II18nService>("i18nService") as MobileI18nService).Init();
            ServiceContainer.Resolve<IAuthService>("authService").Init();
            (ServiceContainer.
                Resolve<IPlatformUtilsService>("platformUtilsService") as MobilePlatformUtilsService).Init();
            // Note: This is not awaited
            var bootstrapTask = BootstrapAsync(postBootstrapFunc);
        }

        // Cozy customization, set status bar to dark content if theme is cozy because of the white background for the status bar
        //*
        public static void AppearanceAdjustments()
        {
            var theme = ThemeManager.GetTheme(false);
            ThemeHelpers.SetAppearance(theme, ThemeManager.OsDarkModeEnabled());
            UIApplication.SharedApplication.StatusBarHidden = false;
            if (theme == null || theme == "cozy")
            {
                UIApplication.SharedApplication.StatusBarStyle = UIStatusBarStyle.DarkContent;
            }
            else
            {
                UIApplication.SharedApplication.StatusBarStyle = UIStatusBarStyle.LightContent;
            }
        }
        /*/
        public static void AppearanceAdjustments()
        {
            ThemeHelpers.SetAppearance(ThemeManager.GetTheme(false), ThemeManager.OsDarkModeEnabled());
            UIApplication.SharedApplication.StatusBarHidden = false;
            UIApplication.SharedApplication.StatusBarStyle = UIStatusBarStyle.LightContent;
        }
        //*/

        public static void SubscribeBroadcastReceiver(UIViewController controller, NFCNdefReaderSession nfcSession,
            NFCReaderDelegate nfcDelegate)
        {
            var broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            var messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            var deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            broadcasterService.Subscribe(nameof(controller), (message) =>
            {
                if (message.Command == "showDialog")
                {
                    var details = message.Data as DialogDetails;
                    var confirmText = string.IsNullOrWhiteSpace(details.ConfirmText) ?
                        AppResources.Ok : details.ConfirmText;

                    NSRunLoop.Main.BeginInvokeOnMainThread(async () =>
                    {
                        var result = await deviceActionService.DisplayAlertAsync(details.Title, details.Text,
                           details.CancelText, details.ConfirmText);
                        var confirmed = result == details.ConfirmText;
                        messagingService.Send("showDialogResolve", new Tuple<int, bool>(details.DialogId, confirmed));
                    });
                }
                else if (message.Command == "listenYubiKeyOTP")
                {
                    ListenYubiKey((bool)message.Data, deviceActionService, nfcSession, nfcDelegate);
                }
            });
        }

        public static void ListenYubiKey(bool listen, IDeviceActionService deviceActionService,
            NFCNdefReaderSession nfcSession, NFCReaderDelegate nfcDelegate)
        {
            if (deviceActionService.SupportsNfc())
            {
                nfcSession?.InvalidateSession();
                nfcSession?.Dispose();
                nfcSession = null;
                if (listen)
                {
                    nfcSession = new NFCNdefReaderSession(nfcDelegate, null, true)
                    {
                        AlertMessage = AppResources.HoldYubikeyNearTop
                    };
                    nfcSession.BeginSession();
                }
            }
        }

        private static async Task BootstrapAsync(Func<Task> postBootstrapFunc = null)
        {
            var disableFavicon = await ServiceContainer.Resolve<IStorageService>("storageService").GetAsync<bool?>(
                Bit.Core.Constants.DisableFaviconKey);
            await ServiceContainer.Resolve<IStateService>("stateService").SaveAsync(
                Bit.Core.Constants.DisableFaviconKey, disableFavicon);
            await ServiceContainer.Resolve<IEnvironmentService>("environmentService").SetUrlsFromStorageAsync();
            if (postBootstrapFunc != null)
            {
                await postBootstrapFunc.Invoke();
            }
        }
    }
}
