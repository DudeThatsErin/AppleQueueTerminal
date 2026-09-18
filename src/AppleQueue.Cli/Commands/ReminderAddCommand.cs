using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;
using AppleQueue.Cli.Parsing;

namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue reminder add &lt;title&gt; [--notes] [--stdin] [--list] [--url]
///                                   [--due &lt;when&gt;] [--priority] [--json]
/// POST /reminders after requiring the "reminders" module.
/// </summary>
public sealed class ReminderAddCommand : ICommand
{
    public static readonly IReadOnlySet<string> Priorities =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "none", "low", "medium", "high" };

    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["title"] = FlagKind.String,
        ["notes"] = FlagKind.String,
        ["list"] = FlagKind.String,
        ["url"] = FlagKind.String,
        ["due"] = FlagKind.String,
        ["priority"] = FlagKind.String,
        ["stdin"] = FlagKind.Boolean,
        ["json"] = FlagKind.Boolean,
    };

    public string Name => "reminder add";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var title = args.RequireTitle("reminder");

        var notes = args.String("notes") ?? string.Empty;
        if (args.Bool("stdin")) notes = await context.Io.ReadStdinAsync(ct).ConfigureAwait(false);

        var payload = new JsonObject { ["title"] = title, ["notes"] = notes };

        var list = args.String("list");
        if (!string.IsNullOrEmpty(list)) payload["list"] = list;

        var url = args.String("url");
        if (!string.IsNullOrEmpty(url)) payload["url"] = url;

        var due = args.String("due");
        if (!string.IsNullOrEmpty(due)) payload["dueDate"] = TimeInput.ToWire(TimeInput.Parse(due, "--due"));

        var priority = args.String("priority");
        if (!string.IsNullOrEmpty(priority))
        {
            if (!Priorities.Contains(priority))
            {
                throw new CliException(
                    $"--priority must be one of {string.Join(", ", Priorities)}",
                    ExitCode.Usage);
            }

            payload["priority"] = priority.ToLowerInvariant();
        }

        var client = context.ClientFactory();
        try
        {
            var config = await context.FetchConfigAsync(client, ct).ConfigureAwait(false);
            config.Require("reminders", "Reminders");

            var response = await client.PostAsync("/reminders", payload, ct).ConfigureAwait(false);
            return context.Report(args.Bool("json"), response, () =>
            {
                var item = response.Obj("reminder");
                context.Io.Out($"Queued reminder \"{item.Str("title") ?? title}\" to list \"{item.Str("list")}\"");
                var dueDate = item.Str("dueDate");
                if (dueDate is not null) context.Io.Out($"due: {TimeInput.FormatLocal(dueDate)}");
                var id = item.Str("id");
                if (id is not null) context.Io.Out($"id: {id}");
            });
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }
}
