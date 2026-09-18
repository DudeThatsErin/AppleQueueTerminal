namespace AppleQueue.Cli.Parsing;

/// <summary>
/// The human-friendly time formats the CLI accepts on --due / --start / --end /
/// --date, plus the duration syntax used by --duration and --alert.
/// </summary>
public static class TimeInput
{
    /// <summary>
    /// Accepts ISO-8601 ("2026-09-15T09:00"), plain dates ("2026-09-15"),
    /// relative offsets ("+2h", "+30m"), and the words "now", "today",
    /// "tomorrow", "yesterday", optionally followed by a clock time ("tomorrow 9am").
    /// Returns an offset in the machine's local zone.
    /// <paramref name="flagName"/> is quoted back in the error message.
    /// </summary>
    public static DateTimeOffset Parse(string raw, string flagName)
        => throw new NotImplementedException();

    /// <summary>"30m", "2h", "1d" -> minutes. Bare numbers mean minutes.</summary>
    public static int DurationMinutes(string raw)
        => throw new NotImplementedException();

    /// <summary>YYYY-MM-DD in local time — what the journal queue files entries under.</summary>
    public static string LocalDate(DateTimeOffset value)
        => throw new NotImplementedException();

    /// <summary>Human-readable local rendering used when printing queued items.</summary>
    public static string FormatLocal(string isoTimestamp)
        => throw new NotImplementedException();
}
