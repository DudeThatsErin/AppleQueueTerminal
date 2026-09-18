using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;
using AppleQueue.Cli.Parsing;

namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue journal add &lt;title&gt; [--title] [--body] [--stdin] [--date] [--json]
/// POST /apple-journal after requiring the "journal" module.
/// Sends a plain calendar date (YYYY-MM-DD, default today) rather than a
/// timestamp: the Shortcut files the entry under that day.
/// </summary>
public sealed class JournalAddCommand : ICommand
{
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["title"] = FlagKind.String,
        ["body"] = FlagKind.String,
        ["date"] = FlagKind.String,
        ["stdin"] = FlagKind.Boolean,
        ["json"] = FlagKind.Boolean,
    };

    public string Name => "journal add";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var title = args.RequireTitle("journal");

        var body = args.String("body") ?? string.Empty;
        if (args.Bool("stdin")) body = await context.Io.ReadStdinAsync(ct).ConfigureAwait(false);

        var raw = args.String("date");
        var date = TimeInput.LocalDate(raw is null ? DateTimeOffset.Now : TimeInput.Parse(raw, "--date"));

        var client = context.ClientFactory();
        try
        {
            var config = await context.FetchConfigAsync(client, ct).ConfigureAwait(false);
            config.Require("journal", "Journal");

            var payload = new JsonObject { ["title"] = title, ["body"] = body, ["date"] = date };
            var response = await client.PostAsync("/apple-journal", payload, ct).ConfigureAwait(false);

            return context.Report(args.Bool("json"), response, () =>
            {
                // The deployed backend echoes the item; be tolerant of its key name.
                var item = response.FirstObj("entry", "journal");
                context.Io.Out($"Queued journal entry \"{item.Str("title") ?? title}\" for {item.Str("date") ?? date}");
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
