using AppleQueue.Cli.Commands;
using AppleQueue.Cli.Configuration;
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
            Settings = new SettingsStore(new Dictionary<string, string?>()),
            ClientFactory = () => client ?? new FakeAppleQueueClient(),
        };
        return (new Cli(context), io);
    }

    [Fact]
    public async Task NoArgsPrintsHelp()
    {
        var (cli, io) = Build();
        var code = await cli.RunAsync([]);

        Assert.Equal(ExitCode.Ok, code);
        Assert.Contains(io.StdOut, line => line.Contains("applequeue"));
    }

    [Fact]
    public async Task VersionFlagPrintsVersion()
    {
        var (cli, io) = Build();
        var code = await cli.RunAsync(["--version"]);

        Assert.Equal(ExitCode.Ok, code);
        Assert.Equal([Cli.Version], io.StdOut);
    }

    [Fact]
    public async Task UnknownCommandIsAUsageError()
    {
        var (cli, io) = Build();
        var code = await cli.RunAsync(["nope"]);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains(io.StdErr, line => line.Contains("unknown command"));
    }

    [Fact]
    public async Task SubcommandGroupRequiresASubcommand()
    {
        var (cli, io) = Build();
        var code = await cli.RunAsync(["note"]);

        Assert.Equal(ExitCode.Usage, code);
        Assert.Contains(io.StdErr, line => line.Contains("usage: applequeue note add"));
    }

    // TODO as each piece lands, port the behaviour the JS suite covered:
    //   * URL normalization: bare host -> https, trailing slash and /api stripped,
    //     plain http rejected except for localhost / 127.0.0.1
    //   * time parsing: ISO, "+2h", "tomorrow 9am", "yesterday"; duration 30m/2h/1d
    //   * event validation: --end with --duration, --end before --start, alert bounds
    //   * client error mapping: 401 -> 2, 503 "not enabled" -> 3, timeout -> 4,
    //     400 -> 1, other non-2xx and non-JSON bodies -> 5
    //   * the API key never appears in any error message or log line
    //   * --json writes only to stdout, nothing else does
    //   * an end-to-end pass against a fake HTTP backend for each queue command
}
