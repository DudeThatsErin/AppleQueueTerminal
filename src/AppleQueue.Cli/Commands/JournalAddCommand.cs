namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue journal add &lt;title&gt; [--title] [--body] [--stdin] [--date] [--json]
/// POST /apple-journal after requiring the "journal" module.
/// Sends a plain calendar date (YYYY-MM-DD, default today) rather than a
/// timestamp: the Shortcut files the entry under that day.
/// </summary>
public sealed class JournalAddCommand : ICommand
{
    public string Name => "journal add";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}
