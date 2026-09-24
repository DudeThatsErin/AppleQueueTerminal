using System.Text;
using System.Text.Json.Nodes;
using AppleQueue.Cli.Parsing;

namespace AppleQueue.Cli.Commands;

public sealed class FreeformAddCommand : ICommand
{
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal) { ["title"] = FlagKind.String, ["board"] = FlagKind.String, ["body"] = FlagKind.String, ["stdin"] = FlagKind.Boolean, ["file"] = FlagKind.List, ["json"] = FlagKind.Boolean };
    public string Name => "freeform add";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var title = args.RequireTitle("Freeform");
        var board = args.String("board")?.Trim();
        if (string.IsNullOrEmpty(board)) throw new CliException("--board is required", ExitCode.Usage);
        var body = args.Bool("stdin") ? await context.Io.ReadStdinAsync(ct).ConfigureAwait(false) : args.String("body") ?? string.Empty;
        var client = context.ClientFactory();
        try
        {
            var attachments = new JsonArray { await Upload(client, SafeName(title), Encoding.UTF8.GetBytes(body), "text/markdown; charset=utf-8", ct) };
            foreach (var path in args.List("file"))
            {
                if (!File.Exists(path)) throw new CliException($"could not read attachment \"{path}\"", ExitCode.Usage);
                attachments.Add(await Upload(client, Path.GetFileName(path), await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false), "application/octet-stream", ct));
            }
            var response = await client.PostAsync("/freeform", new JsonObject { ["title"] = title, ["board"] = board, ["attachments"] = attachments }, ct).ConfigureAwait(false);
            return context.Report(args.Bool("json"), response, () => context.Io.Out($"Queued Freeform files for board \"{board}\""));
        }
        finally { (client as IDisposable)?.Dispose(); }
    }

    private static async Task<JsonObject> Upload(AppleQueue.Cli.Http.IAppleQueueClient client, string name, byte[] bytes, string mime, CancellationToken ct) => await client.UploadAsync("/apple-notes/upload", name, bytes, mime, ct).ConfigureAwait(false);
    private static string SafeName(string title)
    {
        var stem = string.Concat(title.Normalize().Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c)).Trim().TrimEnd('.');
        if (string.IsNullOrEmpty(stem)) stem = "Untitled";
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase)) stem = "_" + stem;
        return stem[..Math.Min(stem.Length, 120)] + ".md";
    }
}
