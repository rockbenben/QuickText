using System.Windows;
using System.Windows.Threading;

namespace QuickText.App.Interop;

/// <summary>Run a paste only once the target window actually holds the foreground. At the timeout
/// it runs the fallback instead of blindly sending Ctrl+V into whatever happens to be focused.</summary>
internal static class ForegroundTarget
{
    public static void WhenForeground(IntPtr target, Action onReady, Action onTimeout, int triesLeft = 12)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) { onTimeout(); return; }

        try
        {
            if (target != IntPtr.Zero && NativeMethods.GetForegroundWindow() == target)
            {
                dispatcher.BeginInvoke(onReady, DispatcherPriority.Background);
                return;
            }
            if (triesLeft <= 0)
            {
                dispatcher.BeginInvoke(onTimeout, DispatcherPriority.Background);
                return;
            }
            System.Threading.Tasks.Task.Delay(20).ContinueWith(_ =>
            {
                try { dispatcher.Invoke(() => WhenForeground(target, onReady, onTimeout, triesLeft - 1)); }
                catch { /* shutting down */ }
            });
        }
        catch { onTimeout(); }
    }
}
