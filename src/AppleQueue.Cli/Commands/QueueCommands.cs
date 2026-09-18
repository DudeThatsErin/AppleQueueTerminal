namespace AppleQueue.Cli.Commands;

/// <summary>One queue the CLI can read back or discard from.</summary>
public sealed record QueueSpec(string Path, string CollectionKey, string Module, string Label);

public static class Queues
{
    public static readonly IReadOnlyDictionary<string, QueueSpec> All =
        new Dictionary<string, QueueSpec>(StringComparer.Ordinal)
        {
            ["journal"] = new("/apple-journal", "entries", "journal", "Journal"),
            ["notes"] = new("/apple-notes", "notes", "notes", "Notes"),
            ["reminders"] = new("/reminders", "reminders", "reminders", "Reminders"),
            ["events"] = new("/calendar", "events", "calendar", "Calendar"),
        };

    public static string KindList() => string.Join('|', All.Keys);
}

/// <summary>
/// applequeue list &lt;journal|notes|reminders|events&gt; [--json]
/// Shows items still waiting for the Shortcut to collect them.
/// </summary>
public sealed class QueueListCommand : ICommand
{
    public string Name => "list";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}

/// <summary>
/// applequeue remove &lt;kind&gt; &lt;id...&gt; [--json]
/// DELETE with {"ids": [...]} — discards queued items without creating them.
/// </summary>
public sealed class QueueRemoveCommand : ICommand
{
    public string Name => "remove";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}
