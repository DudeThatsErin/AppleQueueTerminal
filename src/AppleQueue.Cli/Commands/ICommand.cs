using System.Text.Json.Nodes;
using AppleQueue.Cli.Configuration;
using AppleQueue.Cli.Console;
using AppleQueue.Cli.Http;

namespace AppleQueue.Cli.Commands;

/// <summary>Everything a command is allowed to touch. Keeps commands testable.</summary>
public sealed class CommandContext
{
    public required IConsoleIo Io { get; init; }
    public required ISettingsStore Settings { get; init; }

    /// <summary>Builds a client from the resolved settings; throws ExitCode.Config when unset.</summary>
    public required Func<IAppleQueueClient> ClientFactory { get; init; }

    public async Task<BackendConfig> FetchConfigAsync(IAppleQueueClient client, CancellationToken ct = default)
        => BackendConfig.From(await client.GetAsync("/config", ct).ConfigureAwait(false));

    /// <summary>--json emits the backend's own response and nothing else.</summary>
    public int Report(bool asJson, JsonObject response, Action print)
    {
        if (asJson) Io.Json(response);
        else print();
        return ExitCode.Ok;
    }
}

public interface ICommand
{
    /// <summary>"note add", "doctor", … — used in usage errors.</summary>
    string Name { get; }

    Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default);
}
