using AppleQueue.Cli.Configuration;
using AppleQueue.Cli.Http;
using AppleQueue.Cli.Parsing;

namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue configure [--url ...] [--api-key ...] [--no-verify]
/// Prompts for anything not passed, verifies the backend unless --no-verify,
/// then saves the config file and reports its path. The key is never echoed.
/// </summary>
public sealed class ConfigureCommand : ICommand
{
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["url"] = FlagKind.String,
        ["api-key"] = FlagKind.String,
        ["no-verify"] = FlagKind.Boolean,
    };

    public string Name => "configure";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);

        var rawUrl = args.String("url") ?? await context.Io.AskAsync("Apple Queue backend URL: ", ct: ct).ConfigureAwait(false);
        var url = SettingsStore.NormalizeUrl(rawUrl);

        var apiKey = args.String("api-key")
            ?? await context.Io.AskAsync("API key (input hidden): ", secret: true, ct: ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey)) throw new CliException("an API key is required", ExitCode.Usage);

        if (!args.Bool("no-verify"))
        {
            context.Io.Note("Checking the backend...");
            using var client = new AppleQueueClient(url, apiKey);
            var config = await context.FetchConfigAsync(client, ct).ConfigureAwait(false); // throws with a mapped exit code
            context.Io.Note($"Connected. Enabled modules: {config.EnabledList()}");
        }

        var file = context.Settings.Save(url, apiKey);
        context.Io.Out($"Saved configuration to {file}");
        context.Io.Note("The API key is stored in that file with owner-only permissions and is never printed.");
        return ExitCode.Ok;
    }
}
