using System;
using System.Threading;

namespace WallSafeWeb.Services
{
    /// <summary>
    /// Marshals actions onto the WinForms UI thread. Same API as the WinUI build so
    /// the shared services (WifiWatcher, LocationWatcher, ShutdownProtection) compile unchanged.
    /// </summary>
    public static class UiDispatch
    {
        public static SynchronizationContext? Context { get; set; }
        public static int UiThreadId { get; set; }

        public static void Post(Action action)
        {
            var ctx = Context;
            if (ctx == null || Environment.CurrentManagedThreadId == UiThreadId)
            {
                try { action(); } catch (Exception ex) { App.LogCrash("UiDispatch", ex); }
                return;
            }
            ctx.Post(_ => { try { action(); } catch (Exception ex) { App.LogCrash("UiDispatch", ex); } }, null);
        }
    }
}
