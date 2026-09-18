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

    public string? String(string name) => Strings.TryGetValue(name, out var value) ? value : null;

    public bool Bool(string name) => Booleans.Contains(name);

    public IReadOnlyList<string> List(string name) => Lists.TryGetValue(name, out var values) ? values : [];

    /// <summary>Positionals joined with a single space — the "title as bare words" pattern.</summary>
    public string JoinedPositionals() => string.Join(' ', Positionals).Trim();

    /// <summary>--title if given, otherwise the bare words. Throws when neither is present.</summary>
    public string RequireTitle(string what, string flag = "title")
    {
        var title = String(flag);
        if (string.IsNullOrWhiteSpace(title)) title = JoinedPositionals();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new CliException($"a {what} title is required (positional or --{flag})", ExitCode.Usage);
        }

        return title.Trim();
    }
}

public static class ArgParser
{
    /// <summary>
    /// Parses <paramref name="argv"/> against <paramref name="spec"/>.
    /// Supports --flag value, --flag=value, and boolean --flag. A bare "--" ends
    /// flag parsing so a title may start with a dash.
    /// </summary>
    public static ParsedArgs Parse(
        IReadOnlyList<string> argv,
        IReadOnlyDictionary<string, FlagKind> spec,
        string commandName)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        var booleans = new HashSet<string>(StringComparer.Ordinal);
        var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var positionals = new List<string>();
        var flagsDone = false;

        for (var i = 0; i < argv.Count; i++)
        {
            var token = argv[i];

            if (flagsDone || !token.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            if (token.Length == 2)
            {
                flagsDone = true;
                continue;
            }

            var body = token[2..];
            string? inlineValue = null;
            var equals = body.IndexOf('=');
            if (equals >= 0)
            {
                inlineValue = body[(equals + 1)..];
                body = body[..equals];
            }

            if (!spec.TryGetValue(body, out var kind))
            {
                throw new CliException(
                    $"unknown option --{body} for `applequeue {commandName}`",
                    ExitCode.Usage);
            }

            if (kind == FlagKind.Boolean)
            {
                if (inlineValue is not null && !string.Equals(inlineValue, "true", StringComparison.OrdinalIgnoreCase))
                {
                    booleans.Remove(body);
                    continue;
                }

                booleans.Add(body);
                continue;
            }

            var value = inlineValue;
            if (value is null)
            {
                if (i + 1 >= argv.Count)
                {
                    throw new CliException($"--{body} needs a value", ExitCode.Usage);
                }

                value = argv[++i];
            }

            if (kind == FlagKind.String)
            {
                strings[body] = value;
                continue;
            }

            // List: repeatable, and comma-separated values are split.
            if (!lists.TryGetValue(body, out var bucket))
            {
                bucket = [];
                lists[body] = bucket;
            }

            foreach (var part in value.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) bucket.Add(trimmed);
            }
        }

        return new ParsedArgs
        {
            Strings = strings,
            Booleans = booleans,
            Lists = lists.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.Ordinal),
            Positionals = positionals,
        };
    }
}
