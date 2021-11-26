using Android.Content;

namespace Bit.Droid.Receivers
{
    [BroadcastReceiver(Name = "io.cozy.pass.ClearClipboardAlarmReceiver", Exported = false)]
    public class ClearClipboardAlarmReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var clipboardManager = context.GetSystemService(Context.ClipboardService) as ClipboardManager;
            clipboardManager.PrimaryClip = ClipData.NewPlainText("bitwarden", " ");
        }
    }
}
