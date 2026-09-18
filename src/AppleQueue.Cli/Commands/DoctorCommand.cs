using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;
using AppleQueue.Cli.Parsing;

namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue doctor [--json]
/// Reports the resolved URL/key (and where each came from), GET /health, and the
/// enabled modules and defaults from GET /config. Returns ExitCode.Config — not a
/// throw — when nothing is configured yet, so --json still emits a usable object.
/// </summary>
public sealed class DoctorCommand : ICommand
{
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["json"] = FlagKind.Boolean,
    };

    public string Name => "doctor";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var settings = context.Settings.Load();

        if (!settings.IsConfigured)
        {
            if (args.Bool("json"))
            {
                context.Io.Json(new JsonObject
                {
                    ["ok"] = false,
                    ["configured"] = false,
                    ["error"] = "no backend URL or API key configured",
                    ["configFile"] = settings.File,
                });
            }
            else
            {
                context.Io.Out($"Backend URL: {(string.IsNullOrEmpty(settings.Url) ? "(unset)" : settings.Url)}");
                context.Io.Out($"API key:     {(string.IsNullOrEmpty(settings.ApiKey) ? "(unset)" : "set")}");
                context.Io.Note("Run `applequeue configure` to set them.");
            }

            return ExitCode.Config;
        }

        var client = context.ClientFactory();
        try
        {
            var health = await client.GetAsync("/health", ct).ConfigureAwait(false);
            var configResponse = await client.GetAsync("/config", ct).ConfigureAwait(false);
            var config = Http.BackendConfig.From(configResponse);

            if (args.Bool("json"))
            {
                context.Io.Json(new JsonObject
                {
                    ["ok"] = true,
                    ["url"] = settings.Url,
                    ["sources"] = new JsonObject
                    {
                        ["url"] = settings.Source.Url,
                        ["apiKey"] = settings.Source.ApiKey,
                    },
                    ["health"] = health.DeepClone(),
                    ["config"] = configResponse.DeepClone(),
                });
                return ExitCode.Ok;
            }

            context.Io.Out($"Backend URL:  {settings.Url}  (from {settings.Source.Url})");
            context.Io.Out($"API key:      set (from {settings.Source.ApiKey})");
            context.Io.Out($"Service:      {health.Str("service") ?? "unknown"} @ {health.Str("time") ?? "unknown"}");

            var storage = health.Obj("storage");
            if (storage is not null)
            {
                context.Io.Out($"Storage:      queues={storage.Str("queues")}, attachments={storage.Str("attachments")}");
            }

            context.Io.Out($"Modules:      {config.EnabledList()}");
            context.Io.Out(
                $"Defaults:     notes folder={config.Default("notesFolder")}, "
                + $"reminder list={config.Default("reminderList")}, calendar={config.Default("calendar")}");
            context.Io.Note("Queued items are delivered to Apple apps by your configured Apple Shortcut.");
            return ExitCode.Ok;
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }
}
