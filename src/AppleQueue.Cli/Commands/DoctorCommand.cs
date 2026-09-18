namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue doctor [--json]
/// Reports the resolved URL/key (and where each came from), GET /health, and the
/// enabled modules and defaults from GET /config. Returns ExitCode.Config — not a
/// throw — when nothing is configured yet, so --json still emits a usable object.
/// </summary>
public sealed class DoctorCommand : ICommand
{
    public string Name => "doctor";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}
