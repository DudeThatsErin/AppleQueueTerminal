using AppleQueue.Cli.Commands;
using AppleQueue.Cli.Console;

namespace AppleQueue.Cli;

/// <summary>Top-level dispatch: argv in, exit code out. No I/O of its own beyond help.</summary>
public sealed class Cli
{
    public const string Version = "2.0.0";

    private readonly CommandContext _context;

    public Cli(CommandContext context) => _context = context;

    public static string HelpText => $"""
        applequeue {Version} - queue Apple Journal, Notes, Reminders, and Calendar items from the terminal.

        Usage
          applequeue configure                      Save the backend URL and API key
          applequeue doctor [--json]                Check the connection and show enabled modules
          applequeue note add <title> [options]     Queue an Apple Note
          applequeue reminder add <title> [options] Queue a Reminder
          applequeue event add <title> --start ...  Queue a Calendar event
          applequeue journal add <title> [options]  Queue an Apple Journal entry
          applequeue list <journal|notes|reminders|events>
                                                    Show items still waiting for your Shortcut
          applequeue remove <kind> <id...>          Discard queued items without creating them

        note add
          --title <text>      Title (or pass it as positional words)
          --body <text>       Note body
          --stdin             Read the body from piped stdin
          --folder <name>     Notes folder (defaults to the backend's setting)

        reminder add
          --title <text>      Title (or pass it as positional words)
          --notes <text>      Notes body       --stdin  Read notes from stdin
          --list <name>       Reminders list   --url <url>
          --due <when>        "tomorrow 9am", "+2h", or 2026-09-15T09:00
          --priority <p>      none | low | medium | high

        journal add
          --title <text>      Title (or pass it as positional words)
          --body <text>       Entry body        --stdin  Read the body from stdin
          --date <when>       Day to file under (default today); "yesterday" works

        event add
          --title <text>      Title (or pass it as positional words)
          --start <when>      Required. Same formats as --due
          --end <when>        End time, or use --duration 90m (default 1h)
          --calendar <name>   --location <text>   --notes <text>   --url <url>
          --all-day           --invitee <a,b>     --alert <30m>   (repeatable)

        Global
          --json              Machine-readable output on stdout only
          --help, --version

        Configuration
          Values are read from the environment first, then the saved config file:
            APPLE_QUEUE_URL, APPLE_QUEUE_API_KEY, APPLE_QUEUE_TIMEOUT_MS

        Exit codes
          0 ok   1 usage   2 unauthorized   3 module disabled   4 network   5 backend   6 not configured

        Queued items are written into Apple's apps by your existing Apple Shortcut.

        """;

    public async Task<int> RunAsync(IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        try
        {
            return await DispatchAsync(argv, ct).ConfigureAwait(false);
        }
        catch (CliException err)
        {
            _context.Io.Note($"applequeue: {err.Message}");
            if (err.Code == ExitCode.Config) _context.Io.Note("Run `applequeue configure` to get started.");
            if (err.Code == ExitCode.Auth) _context.Io.Note("Re-run `applequeue configure` with a current API key.");
            return err.Code;
        }
    }

    private Task<int> DispatchAsync(IReadOnlyList<string> argv, CancellationToken ct)
    {
        var command = argv.Count > 0 ? argv[0] : null;
        var rest = argv.Skip(1).ToArray();

        if (command is null or "--help" or "-h" or "help")
        {
            _context.Io.Out(HelpText);
            return Task.FromResult(ExitCode.Ok);
        }

        if (command is "--version" or "-v" or "version")
        {
            _context.Io.Out(Version);
            return Task.FromResult(ExitCode.Ok);
        }

        return command switch
        {
            "configure" => new ConfigureCommand().RunAsync(_context, rest, ct),
            "doctor" => new DoctorCommand().RunAsync(_context, rest, ct),
            "note" => Group("note", rest, ct, ("add", new NoteAddCommand())),
            "reminder" => Group("reminder", rest, ct, ("add", new ReminderAddCommand())),
            "event" => Group("event", rest, ct, ("add", new EventAddCommand())),
            "journal" => Group("journal", rest, ct, ("add", new JournalAddCommand())),
            "list" => new QueueListCommand().RunAsync(_context, rest, ct),
            "remove" => new QueueRemoveCommand().RunAsync(_context, rest, ct),
            _ => throw new CliException($"unknown command \"{command}\". Run `applequeue --help`.", ExitCode.Usage),
        };
    }

    private Task<int> Group(
        string name,
        IReadOnlyList<string> argv,
        CancellationToken ct,
        params (string Sub, ICommand Command)[] subcommands)
    {
        var sub = argv.Count > 0 ? argv[0] : null;
        foreach (var (candidate, handler) in subcommands)
        {
            if (candidate == sub) return handler.RunAsync(_context, argv.Skip(1).ToArray(), ct);
        }

        throw new CliException(
            $"usage: applequeue {name} {string.Join('|', subcommands.Select(s => s.Sub))} ...",
            ExitCode.Usage);
    }
}
