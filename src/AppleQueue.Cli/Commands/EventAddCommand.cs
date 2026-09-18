namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue event add &lt;title&gt; --start &lt;when&gt; [--end | --duration]
///        [--calendar] [--notes] [--location] [--url] [--all-day]
///        [--invitee a,b ...] [--alert 30m ...] [--json]
/// POST /calendar after requiring the "calendar" module.
///
/// Local validation to keep:
///   --start required; --end and --duration are mutually exclusive
///   no --end/--duration -> 1 hour; --end before --start -> ExitCode.Usage
///   each --alert must resolve to 0..40320 minutes (4 weeks)
///
/// The backend turns invitees into a Reminders nudge because Shortcuts cannot
/// send invitations — surface response.inviteReminder and response.warning on
/// stderr instead of letting either be a surprise.
/// </summary>
public sealed class EventAddCommand : ICommand
{
    public const int MaxAlertMinutes = 40320;
    public const int DefaultDurationMinutes = 60;

    public string Name => "event add";

    public Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
        => throw new NotImplementedException();
}
