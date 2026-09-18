namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue reminder add &lt;title&gt; [--notes] [--stdin] [--list] [--url]
///                                   [--due &lt;when&gt;] [--priority] [--json]
/// POST /reminders after requiring the "reminders" module.
/// </summary>
public sealed class ReminderAddCommand : ICommand
{
    public static readonly IReadOnlySet<string> Priorities =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "none", "low", "medium", "high" };

    public string Name => "reminder add";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}
