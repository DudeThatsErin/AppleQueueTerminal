namespace AppleQueue.Cli.Parsing;

public enum FlagKind
{
    String,
    Boolean,
    /// <summary>Repeatable, and comma-separated values are split (e.g. --invitee a,b).</summary>
    List,
}

/// <summary>Result of parsing one command's argv: its flags and its bare words.</summary>
public sealed class ParsedArgs
{
    public required IReadOnlyDictionary<string, string> Strings { get; init; }
    public required IReadOnlySet<string> Booleans { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Lists { get; init; }
    public required IReadOnlyList<string> Positionals { get; init; }

    public string? String(string name) => throw new NotImplementedException();

    public bool Bool(string name) => throw new NotImplementedException();

    public IReadOnlyList<string> List(string name) => throw new NotImplementedException();

    /// <summary>Positionals joined with a single space — the "title as bare words" pattern.</summary>
    public string JoinedPositionals() => throw new NotImplementedException();
}

public static class ArgParser
{
    /// <summary>
    /// Parses <paramref name="argv"/> against <paramref name="spec"/>.
    /// Supports --flag value, --flag=value, and boolean --flag.
    /// Throws <see cref="CliException"/> (ExitCode.Usage) on an unknown flag or a
    /// string flag with no value; <paramref name="commandName"/> names the command
    /// in that message.
    /// </summary>
    public static ParsedArgs Parse(
        IReadOnlyList<string> argv,
        IReadOnlyDictionary<string, FlagKind> spec,
        string commandName)
        => throw new NotImplementedException();
}
