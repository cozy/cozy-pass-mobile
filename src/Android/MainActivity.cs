using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.OS;
using Bit.Core;
using System.Linq;
using Bit.App.Abstractions;
using Bit.Core.Utilities;
using Bit.Core.Abstractions;
using System.IO;
using System;
using Android.Content;
using Bit.Droid.Utilities;
using Bit.Droid.Receivers;
using Bit.App.Models;
using Bit.Core.Enums;
using Android.Nfc;
using System.Threading.Tasks;
using AndroidX.Core.Content;
using Bit.App.Utilities;
using ZXing.Net.Mobile.Android;
using Android.Views;
using Xamarin.Forms.Platform.Android.AppLinks;

namespace Bit.Droid
{
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "cozypass")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "https",
        DataHost = "links.mycozy.cloud",
        DataPathPrefix = "/pass",
        AutoVerify = true)]
    [Activity(
        Label = "Twake Pass",
        Icon = "@mipmap/ic_launcher",
        Theme = "@style/CozyTheme.Splash",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                               ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden |
                               ConfigChanges.Navigation | ConfigChanges.UiMode)]
    [IntentFilter(
        new[] { Intent.ActionSend },
        Categories = new[] { Intent.CategoryDefault },
        DataMimeTypes = new[]
        {
            @"application/*",
            @"image/*",
            @"video/*",
            @"text/*"
        })]
    [Register("io.cozy.pass.MainActivity")]
    public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        private IDeviceActionService _deviceActionService;
        private IMessagingService _messagingService;
        private IBroadcasterService _broadcasterService;
        private IUserService _userService;
        private IAppIdService _appIdService;
        private IEventService _eventService;
        private ICozyClientService _cozyClientService;
        private PendingIntent _eventUploadPendingIntent;
        private AppOptions _appOptions;
        private string _activityKey = $"{nameof(MainActivity)}_{Java.Lang.JavaSystem.CurrentTimeMillis().ToString()}";
        private Java.Util.Regex.Pattern _otpPattern =
            Java.Util.Regex.Pattern.Compile("^.*?([cbdefghijklnrtuv]{32,64})$");

        protected override void OnCreate(Bundle savedInstanceState)
        {
            AndroidAppLinks.Init(this);
            var eventUploadIntent = new Intent(this, typeof(EventUploadReceiver));
            _eventUploadPendingIntent = PendingIntent.GetBroadcast(this, 0, eventUploadIntent,
                AndroidHelpers.AddPendingIntentMutabilityFlag(PendingIntentFlags.UpdateCurrent, false));

            var policy = new StrictMode.ThreadPolicy.Builder().PermitAll().Build();
            StrictMode.SetThreadPolicy(policy);

            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            _userService = ServiceContainer.Resolve<IUserService>("userService");
            _appIdService = ServiceContainer.Resolve<IAppIdService>("appIdService");
            _eventService = ServiceContainer.Resolve<IEventService>("eventService");
            _cozyClientService = ServiceContainer.Resolve<ICozyClientService>("cozyClientService");

            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;

            base.OnCreate(savedInstanceState);
            if (!CoreHelpers.InDebugMode())
            {
                Window.AddFlags(Android.Views.WindowManagerFlags.Secure);
            }
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            Xamarin.Forms.Forms.Init(this, savedInstanceState);
            _appOptions = GetOptions();
            LoadApplication(new App.App(_appOptions));

            AppearanceAdjustments();

            _broadcasterService.Subscribe(_activityKey, (message) =>
            {
                if (message.Command == "startEventTimer")
                {
                    StartEventAlarm();
                }
                else if (message.Command == "stopEventTimer")
                {
                    var task = StopEventAlarmAsync();
                }
                else if (message.Command == "finishMainActivity")
                {
                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() => Finish());
                }
                else if (message.Command == "listenYubiKeyOTP")
                {
                    ListenYubiKey((bool)message.Data);
                }
                else if (message.Command == "updatedTheme")
                {
                    Xamarin.Forms.Device.BeginInvokeOnMainThread(() => AppearanceAdjustments());
                }
                else if (message.Command == "exit")
                {
                    ExitApp();
                }
            });
        }

        protected override void OnPause()
        {
            base.OnPause();
            ListenYubiKey(false);
        }

        protected override void OnResume()
        {
            base.OnResume();
            Xamarin.Essentials.Platform.OnResume();
            AppearanceAdjustments();

            if (Intent.Data?.Scheme == "cozypass")
            {
                OnOpenURL(Intent.DataString);
            }

            if (_deviceActionService.SupportsNfc())
            {
                try
                {
                    _messagingService.Send("resumeYubiKey");
                }
                catch { }
            }
            AndroidHelpers.SetPreconfiguredRestrictionSettingsAsync(this)
                .GetAwaiter()
                .GetResult();
        }

        public void OnOpenURL(string urlStr)
        {
            if (urlStr.Contains("onboarded"))
            {
                _cozyClientService.OnboardedURL = new Uri(urlStr);
                _messagingService.Send("onboarded");
            }
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            try
            {
                if (intent.GetBooleanExtra("generatorTile", false))
                {
                    _messagingService.Send("popAllAndGoToTabGenerator");
                    if (_appOptions != null)
                    {
                        _appOptions.GeneratorTile = true;
                    }
                }
                else if (intent.GetBooleanExtra("myVaultTile", false))
                {
                    _messagingService.Send("popAllAndGoToTabMyVault");
                    if (_appOptions != null)
                    {
                        _appOptions.MyVaultTile = true;
                    }
                }
                else if (intent.Action == Intent.ActionSend && intent.Type != null)
                {
                    if (_appOptions != null)
                    {
                        _appOptions.CreateSend = GetCreateSendRequest(intent);
                    }
                    _messagingService.Send("popAllAndGoToTabSend");
                }
                else
                {
                    ParseYubiKey(intent.DataString);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(">>> {0}: {1}", e.GetType(), e.StackTrace);
            }
        }

        public async override void OnRequestPermissionsResult(int requestCode, string[] permissions,
            [GeneratedEnum] Permission[] grantResults)
        {
            if (requestCode == Constants.SelectFilePermissionRequestCode)
            {
                if (grantResults.Any(r => r != Permission.Granted))
                {
                    _messagingService.Send("selectFileCameraPermissionDenied");
                }
                await _deviceActionService.SelectFileAsync();
            }
            else
            {
                Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
                PermissionsHandler.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            }
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (resultCode == Result.Ok &&
               (requestCode == Constants.SelectFileRequestCode || requestCode == Constants.SaveFileRequestCode))
            {
                Android.Net.Uri uri = null;
                string fileName = null;
                if (data != null && data.Data != null)
                {
                    uri = data.Data;
                    fileName = AndroidHelpers.GetFileName(ApplicationContext, uri);
                }
                else
                {
                    // camera
                    var file = new Java.IO.File(FilesDir, "temp_camera_photo.jpg");
                    uri = FileProvider.GetUriForFile(this, "io.cozy.pass.fileprovider", file);
                    fileName = $"photo_{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.jpg";
                }

                if (uri == null)
                {
                    return;
                }

                if (requestCode == Constants.SaveFileRequestCode)
                {
                    _messagingService.Send("selectSaveFileResult",
                        new Tuple<string, string>(uri.ToString(), fileName));
                    return;
                }
                
                try
                {
                    using (var stream = ContentResolver.OpenInputStream(uri))
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        _messagingService.Send("selectFileResult",
                            new Tuple<byte[], string>(memoryStream.ToArray(), fileName ?? "unknown_file_name"));
                    }
                }
                catch (Java.IO.FileNotFoundException)
                {
                    return;
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _broadcasterService.Unsubscribe(_activityKey);
        }

        private void ListenYubiKey(bool listen)
        {
            if (!_deviceActionService.SupportsNfc())
            {
                return;
            }
            var adapter = NfcAdapter.GetDefaultAdapter(this);
            if (listen)
            {
                var intent = new Intent(this, Class);
                intent.AddFlags(ActivityFlags.SingleTop);
                var pendingIntent = PendingIntent.GetActivity(this, 0, intent, AndroidHelpers.AddPendingIntentMutabilityFlag(0, false));
                // register for all NDEF tags starting with http och https
                var ndef = new IntentFilter(NfcAdapter.ActionNdefDiscovered);
                ndef.AddDataScheme("http");
                ndef.AddDataScheme("https");
                var filters = new IntentFilter[] { ndef };
                try
                {
                    // register for foreground dispatch so we'll receive tags according to our intent filters
                    adapter.EnableForegroundDispatch(this, pendingIntent, filters, null);
                }
                catch { }
            }
            else
            {
                try
                {
                    adapter.DisableForegroundDispatch(this);
                }
                catch { }
            }
        }

        private AppOptions GetOptions()
        {
            var options = new AppOptions
            {
                Uri = Intent.GetStringExtra("uri") ?? Intent.GetStringExtra("autofillFrameworkUri"),
                MyVaultTile = Intent.GetBooleanExtra("myVaultTile", false),
                GeneratorTile = Intent.GetBooleanExtra("generatorTile", false),
                FromAutofillFramework = Intent.GetBooleanExtra("autofillFramework", false),
                CreateSend = GetCreateSendRequest(Intent)
            };
            var fillType = Intent.GetIntExtra("autofillFrameworkFillType", 0);
            if (fillType > 0)
            {
                options.FillType = (CipherType)fillType;
            }
            if (Intent.GetBooleanExtra("autofillFrameworkSave", false))
            {
                options.SaveType = (CipherType)Intent.GetIntExtra("autofillFrameworkType", 0);
                options.SaveName = Intent.GetStringExtra("autofillFrameworkName");
                options.SaveUsername = Intent.GetStringExtra("autofillFrameworkUsername");
                options.SavePassword = Intent.GetStringExtra("autofillFrameworkPassword");
                options.SaveCardName = Intent.GetStringExtra("autofillFrameworkCardName");
                options.SaveCardNumber = Intent.GetStringExtra("autofillFrameworkCardNumber");
                options.SaveCardExpMonth = Intent.GetStringExtra("autofillFrameworkCardExpMonth");
                options.SaveCardExpYear = Intent.GetStringExtra("autofillFrameworkCardExpYear");
                options.SaveCardCode = Intent.GetStringExtra("autofillFrameworkCardCode");
            }
            return options;
        }

        private Tuple<SendType, string, byte[], string> GetCreateSendRequest(Intent intent)
        {
            if (intent.Action == Intent.ActionSend && intent.Type != null)
            {
                if ((intent.Flags & ActivityFlags.LaunchedFromHistory) == ActivityFlags.LaunchedFromHistory)
                {
                    // don't re-deliver intent if resuming from app switcher
                    return null;
                }
                var type = intent.Type;
                if (type.Contains("text/"))
                {
                    var subject = intent.GetStringExtra(Intent.ExtraSubject);
                    var text = intent.GetStringExtra(Intent.ExtraText);
                    return new Tuple<SendType, string, byte[], string>(SendType.Text, subject, null, text);
                }
                else
                {
                    var data = intent.ClipData?.GetItemAt(0);
                    var uri = data?.Uri;
                    var filename = AndroidHelpers.GetFileName(ApplicationContext, uri);
                    try
                    {
                        using (var stream = ContentResolver.OpenInputStream(uri))
                        using (var memoryStream = new MemoryStream())
                        {
                            stream.CopyTo(memoryStream);
                            return new Tuple<SendType, string, byte[], string>(SendType.File, filename, memoryStream.ToArray(), null);
                        }
                    }
                    catch (Java.IO.FileNotFoundException) { }
                }
            }
            return null;
        }

        private void ParseYubiKey(string data)
        {
            if (data == null)
            {
                return;
            }
            var otpMatch = _otpPattern.Matcher(data);
            if (otpMatch.Matches())
            {
                var otp = otpMatch.Group(1);
                _messagingService.Send("gotYubiKeyOTP", otp);
            }
        }

        private void AppearanceAdjustments()
        {
            Window?.SetStatusBarColor(ThemeHelpers.NavBarBackgroundColor);
            Window?.DecorView.SetBackgroundColor(ThemeHelpers.BackgroundColor);
            var theme = ThemeManager.GetTheme(true);
            ThemeHelpers.SetAppearance(theme, ThemeManager.OsDarkModeEnabled());

            // Cozy customization, set status bar to dark content if theme is cozy because of the white background for the status bar
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var decorView = Window.DecorView;
                var flags = (StatusBarVisibility)decorView.SystemUiVisibility;

                if (theme == "cozy" || theme == null)
                {
                    // Add the LIGHT_STATUS_BAR flag for dark icons
                    flags |= (StatusBarVisibility)SystemUiFlags.LightStatusBar;
                }
                else
                {
                    // Remove the LIGHT_STATUS_BAR flag for light icons
                    flags &= ~(StatusBarVisibility)SystemUiFlags.LightStatusBar;
                }
                decorView.SystemUiVisibility = (StatusBarVisibility)flags;
            }
        }

        private void ExitApp()
        {
            FinishAffinity();
            Java.Lang.JavaSystem.Exit(0);
        }

        private void StartEventAlarm()
        {
            var alarmManager = GetSystemService(AlarmService) as AlarmManager;
            alarmManager.SetInexactRepeating(AlarmType.ElapsedRealtime, 120000, 300000, _eventUploadPendingIntent);
        }

        private async Task StopEventAlarmAsync()
        {
            var alarmManager = GetSystemService(AlarmService) as AlarmManager;
            alarmManager.Cancel(_eventUploadPendingIntent);
            await _eventService.UploadEventsAsync();
        }
    }
}
