﻿using Android.Content;
using Bit.Core.Abstractions;
using Bit.Core.Utilities;

namespace Bit.Droid.Receivers
{
    [BroadcastReceiver(Name = "io.cozy.pass.LockAlarmReceiver", Exported = false)]
    public class LockAlarmReceiver : BroadcastReceiver
    {
        public async override void OnReceive(Context context, Intent intent)
        {
            var lockService = ServiceContainer.Resolve<ILockService>("lockService");
            await lockService.CheckLockAsync();
        }
    }
}
