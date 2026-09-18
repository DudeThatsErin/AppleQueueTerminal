using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;
using AppleQueue.Cli.Parsing;

namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue note add &lt;title&gt; [--title] [--body] [--stdin] [--folder] [--json]
/// POST /apple-notes after requiring the "notes" module.
/// </summary>
public sealed class NoteAddCommand : ICommand
{
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["title"] = FlagKind.String,
        ["body"] = FlagKind.String,
        ["folder"] = FlagKind.String,
        ["stdin"] = FlagKind.Boolean,
        ["json"] = FlagKind.Boolean,
    };

    public string Name => "note add";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var title = args.RequireTitle("note");

        var body = args.String("body") ?? string.Empty;
        if (args.Bool("stdin")) body = await context.Io.ReadStdinAsync(ct).ConfigureAwait(false);

        var client = context.ClientFactory();
        try
        {
            var config = await context.FetchConfigAsync(client, ct).ConfigureAwait(false);
            config.Require("notes", "Notes");

            var payload = new JsonObject { ["title"] = title, ["body"] = body };
            var folder = args.String("folder");
            if (!string.IsNullOrEmpty(folder)) payload["folder"] = folder;

            var response = await client.PostAsync("/apple-notes", payload, ct).ConfigureAwait(false);
            return context.Report(args.Bool("json"), response, () =>
            {
                var item = response.Obj("note");
                context.Io.Out($"Queued note \"{item.Str("title") ?? title}\" to folder \"{item.Str("folder")}\"");
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
