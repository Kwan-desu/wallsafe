using System;
using Microsoft.UI.Dispatching;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Marshals actions onto the UI thread's DispatcherQueue. Replaces the WPF
    /// Application.Current.Dispatcher.Invoke pattern the original build used.
    /// </summary>
    public static class UiDispatch
    {
        public static DispatcherQueue? Queue { get; set; }

        public static void Post(Action action)
        {
            var q = Queue;
            if (q == null) { try { action(); } catch { } return; }
            if (q.HasThreadAccess) { try { action(); } catch { } }
            else q.TryEnqueue(() => { try { action(); } catch { } });
        }
    }
}
