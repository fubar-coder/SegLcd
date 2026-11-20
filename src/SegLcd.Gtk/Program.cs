using SegLcd.Client;
using SegLcd.Gui;
using SegLcd.Localization;

// seglcd-gtk: shows the state of seglcdd and configures its sensors over D-Bus. Everything runs on the GTK thread (the
// process main thread); awaits in signal handlers resume there through the GLib synchronization context.
const int noDisplayExitCode = 3;
const int usageExitCode = 2;

var language = DisplayLanguage.Resolve(Environment.GetEnvironmentVariable);
DisplayLanguage.ApplyToUserInterfaceCulture(language);
Texts.Current = new PoStringLocalizerFactory(language).Localizer;

// seglcd-gtk [--system | --session]: only look for the service on that bus (default: system, then session).
ServiceBus? bus = null;
foreach (var arg in args)
{
    switch (arg)
    {
        case "--system":
            bus = ServiceBus.System;
            break;
        case "--session":
            bus = ServiceBus.Session;
            break;
        default:
            Console.Error.WriteLine(Texts.Current.T("Usage: seglcd-gtk [--system | --session]"));
            return usageExitCode;
    }
}

if (!NativeGtk.InitCheck())
{
    Console.Error.WriteLine(Texts.Current.T("seglcd-gtk: cannot open a display."));
    return noDisplayExitCode;
}

Adw.Module.Initialize();
var app = Adw.Application.New(MainWindow.ApplicationId, Gio.ApplicationFlags.DefaultFlags);
MainWindow? window = null;
app.OnActivate += (_, _) =>
{
    window ??= new MainWindow(app, bus);
    window.Present();
};
// argv stays with this process; GApplication gets none (a second start only activates the first window).
return app.RunWithSynchronizationContext(null);
