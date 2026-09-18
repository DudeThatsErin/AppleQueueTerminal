namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue note add &lt;title&gt; [--title] [--body] [--stdin] [--folder] [--json]
/// POST /apple-notes after requiring the "notes" module.
/// Title comes from --title or the joined positionals; missing -> ExitCode.Usage.
/// </summary>
public sealed class NoteAddCommand : ICommand
{
    public string Name => "note add";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}
