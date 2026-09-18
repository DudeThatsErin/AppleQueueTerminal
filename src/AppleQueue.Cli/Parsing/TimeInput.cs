using System.Globalization;
using System.Text.RegularExpressions;

namespace AppleQueue.Cli.Parsing;

/// <summary>
/// The human-friendly time formats the CLI accepts on --due / --start / --end /
/// --date, plus the duration syntax used by --duration and --alert.
/// </summary>
public static partial class TimeInput
{
    /// <summary>Overridable so tests get a fixed "now" for relative input.</summary>
    internal static Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.Now;

    [GeneratedRegex(@"^([+-])\s*(\d+)\s*([a-z]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeRegex();

    [GeneratedRegex(@"^(\d+)\s*([a-z]*)$", RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"^(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$", RegexOptions.IgnoreCase)]
    private static partial Regex ClockRegex();

    /// <summary>
    /// Accepts ISO-8601 ("2026-09-15T09:00"), plain dates ("2026-09-15"),
    /// relative offsets ("+2h", "-30m"), and the words "now", "today",
    /// "tomorrow", "yesterday", optionally followed by a clock time ("tomorrow 9am").
    /// Returns an offset in the machine's local zone.
    /// </summary>
    public static DateTimeOffset Parse(string raw, string flagName)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0) throw Invalid(raw ?? string.Empty, flagName);

        var lower = value.ToLowerInvariant();

        if (lower == "now") return Now();

        var relative = RelativeRegex().Match(lower);
        if (relative.Success)
        {
            var minutes = UnitMinutes(relative.Groups[3].Value, lower, flagName) * int.Parse(relative.Groups[2].Value, CultureInfo.InvariantCulture);
            return relative.Groups[1].Value == "-" ? Now().AddMinutes(-minutes) : Now().AddMinutes(minutes);
        }

        // "today", "tomorrow 9am", "yesterday 17:30"
        foreach (var (word, dayOffset) in new[] { ("today", 0), ("tomorrow", 1), ("yesterday", -1) })
        {
            if (!lower.StartsWith(word, StringComparison.Ordinal)) continue;

            var remainder = lower[word.Length..].Trim();
            var day = Now().Date.AddDays(dayOffset);
            var timeOfDay = remainder.Length == 0 ? TimeSpan.Zero : ParseClock(remainder, value, flagName);
            return ToLocal(day.Add(timeOfDay));
        }

        // A bare clock time means today at that time.
        var bareClock = ClockRegex().Match(lower);
        if (bareClock.Success && (lower.Contains(':') || lower.EndsWith("am", StringComparison.Ordinal) || lower.EndsWith("pm", StringComparison.Ordinal)))
        {
            return ToLocal(Now().Date.Add(ParseClock(lower, value, flagName)));
        }

        // ISO-8601 and plain dates. AssumeLocal so a stamp without an offset means
        // the machine's own zone; an explicit offset or Z is respected.
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        throw Invalid(value, flagName);
    }

    /// <summary>"30m", "2h", "1d" -> minutes. Bare numbers mean minutes.</summary>
    public static int DurationMinutes(string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        var match = DurationRegex().Match(value);
        if (!match.Success) throw NotADuration(value);

        var n = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        return n * UnitMinutes(match.Groups[2].Value, value, null);
    }

    /// <summary>YYYY-MM-DD in local time — what the journal queue files entries under.</summary>
    public static string LocalDate(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The wire format for timestamps: ISO-8601 including the local offset.</summary>
    public static string ToWire(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>Human-readable local rendering used when printing queued items.</summary>
    public static string FormatLocal(string? isoTimestamp)
    {
        if (string.IsNullOrWhiteSpace(isoTimestamp)) return string.Empty;
        return DateTimeOffset.TryParse(isoTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : isoTimestamp;
    }

    private static TimeSpan ParseClock(string clock, string original, string flagName)
    {
        var match = ClockRegex().Match(clock);
        if (!match.Success) throw Invalid(original, flagName);

        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        var suffix = match.Groups[3].Value.ToLowerInvariant();

        if (suffix == "am")
        {
            if (hour is < 1 or > 12) throw Invalid(original, flagName);
            if (hour == 12) hour = 0;
        }
        else if (suffix == "pm")
        {
            if (hour is < 1 or > 12) throw Invalid(original, flagName);
            if (hour != 12) hour += 12;
        }

        if (hour > 23 || minute > 59) throw Invalid(original, flagName);
        return new TimeSpan(hour, minute, 0);
    }

    private static int UnitMinutes(string unit, string original, string? flagName)
    {
        var u = unit.ToLowerInvariant();
        if (u.Length == 0 || u.StartsWith('m')) return 1;
        if (u.StartsWith('h')) return 60;
        if (u.StartsWith('d')) return 1440;
        throw flagName is null ? NotADuration(original) : Invalid(original, flagName);
    }

    private static DateTimeOffset ToLocal(DateTime naive) =>
        new(DateTime.SpecifyKind(naive, DateTimeKind.Local));

    private static CliException Invalid(string raw, string flagName) => new(
        $"\"{raw}\" is not a date or time for {flagName}. Try 2026-09-15T09:00, \"tomorrow 9am\", or +2h.",
        ExitCode.Usage);

    private static CliException NotADuration(string raw) => new(
        $"\"{raw}\" is not a duration. Use 30m, 2h, or 1d.",
        ExitCode.Usage);
}
