using System;
using QuickText.App.Interop;

namespace QuickText.App;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Intercept elevated task registration/unregistration child calls before starting WPF,
        // exiting immediately with the child result code without loading UI, single-instance mutex, or hooks.
        if (ElevatedStartup.TryHandleHelper(args, Autostart.DefaultOptions,
            ex => QuickText.Core.Log.Error("elevated startup helper", ex), out int code))
            return code;

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
