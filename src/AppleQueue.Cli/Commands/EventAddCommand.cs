using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;
using AppleQueue.Cli.Parsing;

namespace AppleQueue.Cli.Commands;

/// <summary>
/// applequeue event add &lt;title&gt; --start &lt;when&gt; [--end | --duration]
///        [--calendar] [--notes] [--location] [--url] [--all-day]
///        [--invitee a,b ...] [--alert 30m ...] [--json]
/// POST /calendar after requiring the "calendar" module.
/// </summary>
public sealed class EventAddCommand : ICommand
{
    public const int MaxAlertMinutes = 40320; // 4 weeks
    public const int DefaultDurationMinutes = 60;

    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["title"] = FlagKind.String,
        ["start"] = FlagKind.String,
        ["end"] = FlagKind.String,
        ["duration"] = FlagKind.String,
        ["calendar"] = FlagKind.String,
        ["notes"] = FlagKind.String,
        ["location"] = FlagKind.String,
        ["url"] = FlagKind.String,
        ["invitee"] = FlagKind.List,
        ["alert"] = FlagKind.List,
        ["all-day"] = FlagKind.Boolean,
        ["json"] = FlagKind.Boolean,
    };

    public string Name => "event add";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var title = args.RequireTitle("event");

        var start = args.String("start");
        if (string.IsNullOrEmpty(start)) throw new CliException("--start is required", ExitCode.Usage);

        var end = args.String("end");
        var duration = args.String("duration");
        if (!string.IsNullOrEmpty(end) && !string.IsNullOrEmpty(duration))
        {
            throw new CliException("pass either --end or --duration, not both", ExitCode.Usage);
        }

        var startDate = TimeInput.Parse(start, "--start");
        var endDate = !string.IsNullOrEmpty(end)
            ? TimeInput.Parse(end, "--end")
            : startDate.AddMinutes(string.IsNullOrEmpty(duration)
                ? DefaultDurationMinutes
                : TimeInput.DurationMinutes(duration));

        if (endDate < startDate) throw new CliException("--end must not be before --start", ExitCode.Usage);

        var alerts = new List<int>();
        foreach (var raw in args.List("alert"))
        {
            var minutes = TimeInput.DurationMinutes(raw);
            if (minutes is < 0 or > MaxAlertMinutes)
            {
                throw new CliException($"--alert {raw} must be between 0 and 4 weeks", ExitCode.Usage);
            }

            alerts.Add(minutes);
        }

        var payload = new JsonObject
        {
            ["title"] = title,
            ["startDate"] = TimeInput.ToWire(startDate),
            ["endDate"] = TimeInput.ToWire(endDate),
            ["allDay"] = args.Bool("all-day"),
        };

        foreach (var (flag, key) in new[]
                 {
                     ("calendar", "calendar"), ("notes", "notes"),
                     ("location", "location"), ("url", "url"),
                 })
        {
            var value = args.String(flag);
            if (!string.IsNullOrEmpty(value)) payload[key] = value;
        }

        var invitees = args.List("invitee");
        if (invitees.Count > 0) payload["invitees"] = invitees.ToJsonArray();
        if (alerts.Count > 0) payload["alerts"] = alerts.ToJsonArray();

        var client = context.ClientFactory();
        try
        {
            var config = await context.FetchConfigAsync(client, ct).ConfigureAwait(false);
            config.Require("calendar", "Calendar");

            var response = await client.PostAsync("/calendar", payload, ct).ConfigureAwait(false);
            return context.Report(args.Bool("json"), response, () =>
            {
                var item = response.Obj("event");
                context.Io.Out($"Queued event \"{item.Str("title") ?? title}\" to calendar \"{item.Str("calendar")}\"");
                context.Io.Out($"{TimeInput.FormatLocal(item.Str("startDate"))} -> {TimeInput.FormatLocal(item.Str("endDate"))}");
                var id = item.Str("id");
                if (id is not null) context.Io.Out($"id: {id}");

                // The backend turns invitees into a Reminders nudge because Shortcuts
                // cannot send invitations; surface that instead of letting it be a surprise.
                if (response.Bool("inviteReminder") == true)
                {
                    var names = item.Arr("invitees").Strings();
                    if (names.Count == 0) names = invitees;
                    context.Io.Note($"Also queued a reminder to invite: {string.Join(", ", names)}");
                }

                var warning = response.Str("warning");
                if (warning is not null) context.Io.Note($"Warning: {warning}");
            });
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }
}
