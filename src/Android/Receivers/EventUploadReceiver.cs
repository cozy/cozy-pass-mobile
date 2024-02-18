using Android.Content;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;

namespace Bit.Droid.Receivers
{
    [BroadcastReceiver(Name = "io.cozy.pass.mobile.EventUploadReceiver", Exported = false)]
    public class EventUploadReceiver : BroadcastReceiver
    {
        public async override void OnReceive(Context context, Intent intent)
        {
            var eventService = ServiceContainer.Resolve<IEventService>("eventService");
            await eventService.UploadEventsAsync();
        }
    }
}
