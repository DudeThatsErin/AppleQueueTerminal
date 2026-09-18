using System.Text.Json.Nodes;
using AppleQueue.Cli.Commands;
using Xunit;

namespace AppleQueue.Cli.Tests;

public class CliTests
{
    private static (Cli Cli, FakeConsoleIo Io) Build(FakeAppleQueueClient? client = null)
    {
        var io = new FakeConsoleIo();
        var context = new CommandContext
        {
            Io = io,
            Settings = TestContext.Store("https://queue.example", "k"),
            ClientFactory = () => client ?? new FakeAppleQueueClient(),
        };
        return (new Cli(context), io);
    }

    [Fact]
    public async Task NoArgsPrintsHelp()
    {
        var (cli, io) = Build();
        Assert.Equal(ExitCode.Ok, await cli.RunAsync([]));
        Assert.Contains(io.StdOut, line => line.Contains("applequeue"));
    }

    [Fact]
    public async Task VersionFlagPrintsVersion()
    {
        var (cli, io) = Build();
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["--version"]));
        Assert.Equal([Cli.Version], io.StdOut);
    }

    [Fact]
    public async Task UnknownCommandIsAUsageError()
    {
        var (cli, io) = Build();
        Assert.Equal(ExitCode.Usage, await cli.RunAsync(["nope"]));
        Assert.Contains(io.StdErr, line => line.Contains("unknown command"));
    }

    [Fact]
    public async Task SubcommandGroupRequiresASubcommand()
    {
        var (cli, io) = Build();
        Assert.Equal(ExitCode.Usage, await cli.RunAsync(["note"]));
        Assert.Contains(io.StdErr, line => line.Contains("usage: applequeue note add"));
    }

    [Fact]
    public async Task NoteAddPostsTitleAndBody()
    {
        var client = new FakeAppleQueueClient()
            .WithConfig("notes")
            .With("POST /apple-notes", new JsonObject
            {
                ["note"] = new JsonObject { ["id"] = "n1", ["title"] = "Buy milk", ["folder"] = "Inbox" },
            });

        var (cli, io) = Build(client);
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["note", "add", "Buy", "milk", "--body", "2%"]));

        var body = client.BodyFor("POST /apple-notes");
        Assert.Equal("Buy milk", (string?)body!["title"]);
        Assert.Equal("2%", (string?)body["body"]);
        Assert.Contains(io.StdOut, l => l.Contains("Queued note \"Buy milk\" to folder \"Inbox\""));
        Assert.Contains(io.StdOut, l => l == "id: n1");
    }

    [Fact]
    public async Task DisabledModuleFailsBeforeSendingContent()
    {
        var client = new FakeAppleQueueClient().WithConfig("reminders"); // notes off

        var (cli, io) = Build(client);
        Assert.Equal(ExitCode.Disabled, await cli.RunAsync(["note", "add", "secret"]));

        Assert.DoesNotContain(client.Requests, r => r.Method == "POST");
        Assert.Contains(io.StdErr, l => l.Contains("Notes is not enabled"));
    }

    [Fact]
    public async Task MissingTitleIsAUsageError()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().WithConfig("notes"));
        Assert.Equal(ExitCode.Usage, await cli.RunAsync(["note", "add"]));
        Assert.Contains(io.StdErr, l => l.Contains("a note title is required"));
    }

    [Fact]
    public async Task UnknownOptionIsAUsageError()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().WithConfig("notes"));
        Assert.Equal(ExitCode.Usage, await cli.RunAsync(["note", "add", "x", "--nope", "1"]));
        Assert.Contains(io.StdErr, l => l.Contains("unknown option --nope"));
    }

    [Fact]
    public async Task ReminderRejectsAnUnknownPriority()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().WithConfig("reminders"));
        Assert.Equal(ExitCode.Usage, await cli.RunAsync(["reminder", "add", "x", "--priority", "urgent"]));
        Assert.Contains(io.StdErr, l => l.Contains("--priority must be one of"));
    }

    [Fact]
    public async Task ReminderSendsNormalizedPriorityAndDueDate()
    {
        var client = new FakeAppleQueueClient()
            .WithConfig("reminders")
            .With("POST /reminders", new JsonObject
            {
                ["reminder"] = new JsonObject { ["id"] = "r1", ["title"] = "Call", ["list"] = "Inbox" },
            });

        var (cli, _) = Build(client);
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["reminder", "add", "Call", "--priority", "HIGH", "--due", "2026-09-15T09:00"]));

        var body = client.BodyFor("POST /reminders")!;
        Assert.Equal("high", (string?)body["priority"]);
        Assert.StartsWith("2026-09-15T09:00:00", (string?)body["dueDate"]);
    }

    [Fact]
    public async Task EventRejectsEndWithDuration()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().WithConfig("calendar"));
        var code = await cli.RunAsync(["event", "add", "x", "--start", "2026-09-15T09:00", "--end", "2026-09-15T10:00", "--duration", "30m"]);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains(io.StdErr, l => l.Contains("not both"));
    }

    [Fact]
    public async Task EventRejectsEndBeforeStart()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().WithConfig("calendar"));
        var code = await cli.RunAsync(["event", "add", "x", "--start", "2026-09-15T10:00", "--end", "2026-09-15T09:00"]);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains(io.StdErr, l => l.Contains("--end must not be before --start"));
    }

    [Fact]
    public async Task EventRejectsAnAlertBeyondFourWeeks()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().WithConfig("calendar"));
        var code = await cli.RunAsync(["event", "add", "x", "--start", "2026-09-15T10:00", "--alert", "50000m"]);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains(io.StdErr, l => l.Contains("must be between 0 and 4 weeks"));
    }

    [Fact]
    public async Task EventRejectsAnUnparseableAlert()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().WithConfig("calendar"));
        var code = await cli.RunAsync(["event", "add", "x", "--start", "2026-09-15T10:00", "--alert", "soon"]);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains(io.StdErr, l => l.Contains("is not a duration"));
    }

    [Fact]
    public async Task EventDefaultsToAnHourAndSplitsInvitees()
    {
        var client = new FakeAppleQueueClient()
            .WithConfig("calendar")
            .With("POST /calendar", new JsonObject
            {
                ["event"] = new JsonObject
                {
                    ["id"] = "e1",
                    ["title"] = "Sync",
                    ["calendar"] = "Home",
                    ["startDate"] = "2026-09-15T09:00:00+00:00",
                    ["endDate"] = "2026-09-15T10:00:00+00:00",
                    ["invitees"] = new JsonArray("a@example.com", "b@example.com"),
                },
                ["inviteReminder"] = true,
            });

        var (cli, io) = Build(client);
        var code = await cli.RunAsync(["event", "add", "Sync", "--start", "2026-09-15T09:00", "--invitee", "a@example.com,b@example.com", "--alert", "30m"]);

        Assert.Equal(ExitCode.Ok, code);
        var body = client.BodyFor("POST /calendar")!;
        Assert.Equal(2, body["invitees"]!.AsArray().Count);
        Assert.Equal(30, (int)body["alerts"]!.AsArray()[0]!);
        Assert.StartsWith("2026-09-15T10:00:00", (string?)body["endDate"]);
        Assert.Contains(io.StdErr, l => l.Contains("Also queued a reminder to invite"));
    }

    [Fact]
    public async Task ListRejectsAnUnknownKind()
    {
        var (cli, io) = Build(new FakeAppleQueueClient());
        Assert.Equal(ExitCode.Usage, await cli.RunAsync(["list", "widgets"]));
        Assert.Contains(io.StdErr, l => l.Contains("usage: applequeue list"));
    }

    [Fact]
    public async Task ListPrintsEachQueuedItem()
    {
        var client = new FakeAppleQueueClient().With("GET /reminders", new JsonObject
        {
            ["reminders"] = new JsonArray(
                new JsonObject { ["id"] = "r1", ["title"] = "Call", ["dueDate"] = "2026-09-15T09:00:00+00:00" },
                new JsonObject { ["id"] = "r2", ["title"] = "Email" }),
        });

        var (cli, io) = Build(client);
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["list", "reminders"]));

        Assert.Contains(io.StdOut, l => l.StartsWith("r1  Call  ["));
        Assert.Contains(io.StdOut, l => l == "r2  Email");
    }

    [Fact]
    public async Task EmptyQueueSaysSo()
    {
        var (cli, io) = Build(new FakeAppleQueueClient().With("GET /apple-notes", new JsonObject { ["notes"] = new JsonArray() }));
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["list", "notes"]));
        Assert.Contains(io.StdOut, l => l.Contains("No notes waiting"));
    }

    [Fact]
    public async Task RemoveRequiresAtLeastOneId()
    {
        var (cli, io) = Build(new FakeAppleQueueClient());
        Assert.Equal(ExitCode.Usage, await cli.RunAsync(["remove", "notes"]));
        Assert.Contains(io.StdErr, l => l.Contains("at least one item id is required"));
    }

    [Fact]
    public async Task RemoveSendsTheIds()
    {
        var client = new FakeAppleQueueClient().With("DELETE /notes", new JsonObject());
        client.Responses["DELETE /apple-notes"] = new JsonObject { ["removed"] = 2 };

        var (cli, io) = Build(client);
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["remove", "notes", "a", "b"]));

        var body = client.BodyFor("DELETE /apple-notes")!;
        Assert.Equal(2, body["ids"]!.AsArray().Count);
        Assert.Contains(io.StdOut, l => l.Contains("Removed 2 item(s)"));
    }

    [Fact]
    public async Task JsonGoesToStdoutAndNothingElseDoes()
    {
        var client = new FakeAppleQueueClient()
            .WithConfig("notes")
            .With("POST /apple-notes", new JsonObject { ["note"] = new JsonObject { ["id"] = "n1" } });

        var (cli, io) = Build(client);
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["note", "add", "x", "--json"]));

        Assert.Single(io.JsonOut);
        Assert.Single(io.StdOut);
        Assert.Empty(io.StdErr);
    }

    [Fact]
    public async Task DoctorReportsUnconfiguredWithoutThrowing()
    {
        var io = new FakeConsoleIo();
        var context = new CommandContext
        {
            Io = io,
            Settings = TestContext.Store(),
            ClientFactory = () => throw new CliException("should not be called", ExitCode.Config),
        };

        Assert.Equal(ExitCode.Config, await new Cli(context).RunAsync(["doctor"]));
        Assert.Contains(io.StdOut, l => l.Contains("(unset)"));
    }

    [Fact]
    public async Task DoctorSummarizesTheDeployment()
    {
        var client = new FakeAppleQueueClient()
            .WithConfig("notes", "reminders")
            .With("GET /health", new JsonObject { ["service"] = "applequeue", ["time"] = "2026-09-18T00:00:00Z" });

        var (cli, io) = Build(client);
        Assert.Equal(ExitCode.Ok, await cli.RunAsync(["doctor"]));

        Assert.Contains(io.StdOut, l => l.Contains("notes, reminders"));
        Assert.Contains(io.StdOut, l => l.Contains("applequeue @ 2026-09-18T00:00:00Z"));
    }

    [Fact]
    public async Task ConfigurePromptsHidesTheKeyAndSaves()
    {
        var dir = Path.Combine(Path.GetTempPath(), "applequeue-tests", Guid.NewGuid().ToString("n"));
        var io = new FakeConsoleIo();
        io.Answers.Enqueue("queue.example/api/");
        io.Answers.Enqueue("secret-key");

        var context = new CommandContext
        {
            Io = io,
            Settings = TestContext.Store(configDir: dir),
            ClientFactory = () => new FakeAppleQueueClient().WithConfig("notes"),
        };

        Assert.Equal(ExitCode.Ok, await new Cli(context).RunAsync(["configure", "--no-verify"]));

        var saved = File.ReadAllText(Path.Combine(dir, "config.json"));
        Assert.Contains("https://queue.example", saved);
        Assert.DoesNotContain("/api", saved);
        Assert.Contains(io.Prompts, p => p.Secret); // the key prompt is masked
        Assert.DoesNotContain("secret-key", io.AllText()); // and never echoed

        Directory.Delete(dir, recursive: true);
    }
}
