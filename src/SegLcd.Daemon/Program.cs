using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

using SegLcd.Daemon;
using SegLcd.Daemon.DBus;
using SegLcd.Localization;

// seglcdd [--config <path>]   (default: $XDG_CONFIG_HOME/seglcd/config.toml)

// Display language from LC_MESSAGES / LANG: used for D-Bus error messages and the console log in a terminal.
var language = DisplayLanguage.Resolve(Environment.GetEnvironmentVariable);
DisplayLanguage.ApplyToUserInterfaceCulture(language);
var localization = new PoStringLocalizerFactory(language);
Texts.Current = localization.Localizer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSystemd(); // Type=notify + journald log format
builder.Services.AddSingleton(localization);
builder.Services.AddSingleton<IStringLocalizerFactory>(localization);
builder.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(TypedStringLocalizer<>));

// The journal stays English (systemd formatter); only an interactive console shows translated messages.
if (Environment.GetEnvironmentVariable("JOURNAL_STREAM") is null)
{
    builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
    builder.Logging.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ConsoleFormatter, LocalizingConsoleFormatter>(p =>
        new LocalizingConsoleFormatter(
            localization.Localizer,
            () => p.GetServices<ConsoleFormatter>().First(f => f.Name == ConsoleFormatterNames.Simple))));
    builder.Logging.Services.Configure<ConsoleLoggerOptions>(o => o.FormatterName = LocalizingConsoleFormatter.FormatterName);
}

builder.Services.AddSingleton(new ConfigStore(builder.Configuration["config"] ?? ConfigStore.DefaultPath));
builder.Services.AddSingleton<DisplayController>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DisplayController>());
builder.Services.AddHostedService<DBusService>();

using var host = builder.Build();
if (localization.LoadError is { } error)
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Localization")
        .LogWarning("Catalog {Catalog} unreadable, using English: {Reason}", error.Catalog, error.Reason);
await host.RunAsync();
HidApi.Hid.Exit();