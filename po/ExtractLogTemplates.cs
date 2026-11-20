// Writes the [LoggerMessage] templates of seglcdd as PO template entries to standard output.
// xgettext cannot see attribute arguments, so po/extract.sh runs this with `dotnet run --file`.
#:project ../src/SegLcd.Daemon/SegLcd.Daemon.csproj
#:property PublishAot=false

using System.Reflection;
using System.Text;

using Microsoft.Extensions.Logging;

var templates = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
foreach (var type in typeof(SegLcd.Daemon.DisplayController).Assembly.GetTypes())
{
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    {
        if (method.GetCustomAttribute<LoggerMessageAttribute>() is { Message: { Length: > 0 } message } attribute)
        {
            if (!templates.TryGetValue(message, out var events))
            {
                templates[message] = events = new SortedSet<string>(StringComparer.Ordinal);
            }

            events.Add($"{type.FullName}.{method.Name} (event {attribute.EventId})");
        }
    }
}

var output = new StringBuilder();
foreach (var (message, events) in templates)
{
    output.Append('\n');
    foreach (var name in events)
    {
        output.Append("#. Log message ").Append(name).Append(". Keep the {Name} placeholders.\n");
    }

    output.Append("msgid ").Append(Quote(message)).Append('\n');
    output.Append("msgstr \"\"\n");
}

Console.OutputEncoding = new UTF8Encoding(false);
Console.Out.Write(output.ToString());
return 0;

static string Quote(string text) =>
    "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
