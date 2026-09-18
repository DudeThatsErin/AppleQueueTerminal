namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue configure [--url ...] [--api-key ...] [--no-verify]
/// Prompts for anything not passed, verifies the backend unless --no-verify,
/// then saves the config file and reports its path. The key is never echoed.
/// </summary>
public sealed class ConfigureCommand : ICommand
{
    public string Name => "configure";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}
