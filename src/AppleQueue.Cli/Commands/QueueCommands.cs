using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;
using AppleQueue.Cli.Parsing;

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
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["json"] = FlagKind.Boolean,
    };

    public string Name => "list";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var kind = args.Positionals.Count > 0 ? args.Positionals[0] : null;
        if (kind is null || !Queues.All.TryGetValue(kind, out var spec))
        {
            throw new CliException($"usage: applequeue list <{Queues.KindList()}>", ExitCode.Usage);
        }

        var client = context.ClientFactory();
        try
        {
            var response = await client.GetAsync(spec.Path, ct).ConfigureAwait(false);
            var items = response.Arr(spec.CollectionKey) ?? [];

            if (args.Bool("json"))
            {
                context.Io.Json(new JsonObject
                {
                    ["ok"] = true,
                    [spec.CollectionKey] = items.DeepClone(),
                });
                return ExitCode.Ok;
            }

            if (items.Count == 0)
            {
                context.Io.Out($"No {kind} waiting. (Items disappear once your Shortcut collects them.)");
                return ExitCode.Ok;
            }

            foreach (var node in items)
            {
                if (node is not JsonObject item) continue;
                var when = item.Str("startDate") ?? item.Str("dueDate");
                var label = when is not null ? TimeInput.FormatLocal(when) : item.Str("date") ?? string.Empty;
                var suffix = string.IsNullOrEmpty(label) ? string.Empty : $"  [{label}]";
                context.Io.Out($"{item.Str("id")}  {item.Str("title")}{suffix}");
            }

            return ExitCode.Ok;
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }
}

/// <summary>
/// applequeue remove &lt;kind&gt; &lt;id...&gt; [--json]
/// DELETE with {"ids": [...]} — discards queued items without creating them.
/// </summary>
public sealed class QueueRemoveCommand : ICommand
{
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["json"] = FlagKind.Boolean,
    };

    public string Name => "remove";

    public async Task<int> RunAsync(CommandContext context, IReadOnlyList<string> argv, CancellationToken ct = default)
    {
        var args = ArgParser.Parse(argv, Spec, Name);
        var kind = args.Positionals.Count > 0 ? args.Positionals[0] : null;
        if (kind is null || !Queues.All.TryGetValue(kind, out var spec))
        {
            throw new CliException($"usage: applequeue remove <{Queues.KindList()}> <id...>", ExitCode.Usage);
        }

        var ids = args.Positionals.Skip(1).ToArray();
        if (ids.Length == 0) throw new CliException("at least one item id is required", ExitCode.Usage);

        var client = context.ClientFactory();
        try
        {
            var payload = new JsonObject { ["ids"] = ids.ToJsonArray() };
            var response = await client.DeleteAsync(spec.Path, payload, ct).ConfigureAwait(false);

            return context.Report(args.Bool("json"), response, () =>
            {
                var removed = response.Int("removed") ?? ids.Length;
                context.Io.Out($"Removed {removed} item(s) from the {kind} queue.");
            });
        }
        finally
        {
            (client as IDisposable)?.Dispose();
        }
    }
}
