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

    public Task<BackendConfig> FetchConfigAsync(IAppleQueueClient client, CancellationToken ct = default)
        => throw new NotImplementedException();
}

public interface ICommand
{
    /// <summary>"note add", "doctor", … — used in usage errors.</summary>
    string Name { get; }

    Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default);
}
