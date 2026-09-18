using System.Net;
using AppleQueue.Cli.Configuration;
using AppleQueue.Cli.Http;
using AppleQueue.Cli.Parsing;
using Xunit;

namespace AppleQueue.Cli.Tests;

public class UrlNormalizationTests
{
    [Theory]
    [InlineData("queue.example", "https://queue.example")]
    [InlineData("https://queue.example/", "https://queue.example")]
    [InlineData("https://queue.example/api", "https://queue.example")]
    [InlineData("https://queue.example/api/", "https://queue.example")]
    [InlineData("https://queue.example/base/api", "https://queue.example/base")]
    [InlineData("http://localhost:8787", "http://localhost:8787")]
    [InlineData("localhost:8787", "http://localhost:8787")]
    [InlineData("localhost:8787/api", "http://localhost:8787")]
    [InlineData("127.0.0.1:8787", "http://127.0.0.1:8787")]
    [InlineData("http://127.0.0.1:8787/api", "http://127.0.0.1:8787")]
    public void Normalizes(string input, string expected)
        => Assert.Equal(expected, SettingsStore.NormalizeUrl(input));

    [Fact]
    public void RejectsPlainHttpForRemoteHosts()
    {
        var err = Assert.Throws<CliException>(() => SettingsStore.NormalizeUrl("http://queue.example"));
        Assert.Equal(ExitCode.Usage, err.Code);
        Assert.Contains("https", err.Message);
    }

    [Fact]
    public void RejectsAnEmptyUrl()
        => Assert.Equal(ExitCode.Usage, Assert.Throws<CliException>(() => SettingsStore.NormalizeUrl("  ")).Code);
}

public class SettingsPrecedenceTests
{
    [Fact]
    public void EnvironmentWinsOverTheConfigFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "applequeue-tests", Guid.NewGuid().ToString("n"));
        try
        {
            TestContext.Store(configDir: dir).Save("https://file.example", "file-key");

            var withEnv = TestContext.Store("https://env.example", "env-key", dir).Load();
            Assert.Equal("https://env.example", withEnv.Url);
            Assert.Equal("env", withEnv.Source.Url);

            var withoutEnv = TestContext.Store(configDir: dir).Load();
            Assert.Equal("https://file.example", withoutEnv.Url);
            Assert.Equal("config file", withoutEnv.Source.ApiKey);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RequireThrowsConfigWhenUnset()
    {
        var err = Assert.Throws<CliException>(() => TestContext.Store().Require());
        Assert.Equal(ExitCode.Config, err.Code);
    }

    [Fact]
    public void MissingConfigFileIsNotAnError()
        => Assert.False(TestContext.Store().Load().IsConfigured);
}

public class ArgParserTests
{
    private static readonly Dictionary<string, FlagKind> Spec = new(StringComparer.Ordinal)
    {
        ["title"] = FlagKind.String,
        ["all-day"] = FlagKind.Boolean,
        ["invitee"] = FlagKind.List,
    };

    [Fact]
    public void ParsesSpaceAndEqualsForms()
    {
        var args = ArgParser.Parse(["--title", "a b", "--all-day", "--invitee=x,y"], Spec, "t");
        Assert.Equal("a b", args.String("title"));
        Assert.True(args.Bool("all-day"));
        Assert.Equal(["x", "y"], args.List("invitee"));
    }

    [Fact]
    public void RepeatedListFlagsAccumulate()
    {
        var args = ArgParser.Parse(["--invitee", "a", "--invitee", "b,c"], Spec, "t");
        Assert.Equal(["a", "b", "c"], args.List("invitee"));
    }

    [Fact]
    public void CollectsPositionals()
        => Assert.Equal("hello world", ArgParser.Parse(["hello", "world"], Spec, "t").JoinedPositionals());

    [Fact]
    public void DoubleDashEndsFlagParsing()
    {
        var args = ArgParser.Parse(["--", "--title"], Spec, "t");
        Assert.Equal("--title", args.JoinedPositionals());
        Assert.Null(args.String("title"));
    }

    [Fact]
    public void MissingValueIsAUsageError()
    {
        var err = Assert.Throws<CliException>(() => ArgParser.Parse(["--title"], Spec, "t"));
        Assert.Equal(ExitCode.Usage, err.Code);
    }

    [Fact]
    public void UnknownFlagNamesTheCommand()
    {
        var err = Assert.Throws<CliException>(() => ArgParser.Parse(["--nope"], Spec, "note add"));
        Assert.Contains("applequeue note add", err.Message);
    }
}

public class TimeInputTests : IDisposable
{
    private static readonly DateTimeOffset Fixed =
        new(2026, 9, 18, 13, 30, 0, TimeSpan.Zero);

    public TimeInputTests() => TimeInput.Now = () => Fixed.ToLocalTime();

    public void Dispose() => TimeInput.Now = () => DateTimeOffset.Now;

    [Fact]
    public void ParsesIso()
    {
        var parsed = TimeInput.Parse("2026-09-15T09:00", "--start");
        Assert.Equal(new DateTime(2026, 9, 15, 9, 0, 0), parsed.DateTime);
    }

    [Fact]
    public void ParsesAPlainDateAsMidnight()
        => Assert.Equal(new DateTime(2026, 9, 15, 0, 0, 0), TimeInput.Parse("2026-09-15", "--date").DateTime);

    [Fact]
    public void ParsesRelativeOffsets()
    {
        var now = TimeInput.Now();
        Assert.Equal(now.AddHours(2), TimeInput.Parse("+2h", "--due"));
        Assert.Equal(now.AddMinutes(30), TimeInput.Parse("+30", "--due"));
        Assert.Equal(now.AddDays(-1), TimeInput.Parse("-1d", "--due"));
    }

    [Fact]
    public void ParsesDayWordsWithAClock()
    {
        var tomorrow9 = TimeInput.Parse("tomorrow 9am", "--due");
        Assert.Equal(TimeInput.Now().Date.AddDays(1).AddHours(9), tomorrow9.DateTime);

        var yesterday = TimeInput.Parse("yesterday", "--date");
        Assert.Equal(TimeInput.Now().Date.AddDays(-1), yesterday.DateTime);

        Assert.Equal(TimeInput.Now().Date.AddHours(17).AddMinutes(30), TimeInput.Parse("today 5:30pm", "--due").DateTime);
    }

    [Fact]
    public void RejectsNonsense()
    {
        var err = Assert.Throws<CliException>(() => TimeInput.Parse("someday", "--due"));
        Assert.Equal(ExitCode.Usage, err.Code);
        Assert.Contains("--due", err.Message);
    }

    [Theory]
    [InlineData("30m", 30)]
    [InlineData("30", 30)]
    [InlineData("2h", 120)]
    [InlineData("1d", 1440)]
    [InlineData("90 minutes", 90)]
    public void ParsesDurations(string raw, int expected)
        => Assert.Equal(expected, TimeInput.DurationMinutes(raw));

    [Fact]
    public void RejectsABadDuration()
        => Assert.Contains("is not a duration", Assert.Throws<CliException>(() => TimeInput.DurationMinutes("soon")).Message);

    [Fact]
    public void LocalDateIsCalendarOnly()
        => Assert.Equal("2026-09-15", TimeInput.LocalDate(new DateTimeOffset(2026, 9, 15, 23, 0, 0, TimeSpan.Zero).ToLocalTime()));
}

public class ClientMappingTests
{
    private static AppleQueueClient Client(HttpMessageHandler handler, TimeSpan? timeout = null)
        => new("https://queue.example", "super-secret-key", timeout, handler);

    [Fact]
    public async Task SendsTheApiKeyHeaderAndApiPrefix()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"ok\":true}");
        using var client = Client(handler);

        await client.GetAsync("/config");

        Assert.Equal("https://queue.example/api/config", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("super-secret-key", handler.LastRequest.Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    public async Task UnauthorizedMapsToExitTwoAndHidesTheKey()
    {
        using var client = Client(new StubHandler(HttpStatusCode.Unauthorized, "{\"error\":\"nope\"}"));

        var err = await Assert.ThrowsAsync<CliException>(() => client.GetAsync("/config"));
        Assert.Equal(ExitCode.Auth, err.Code);
        Assert.DoesNotContain("super-secret-key", err.Message);
    }

    [Fact]
    public async Task DisabledModuleMapsToExitThree()
    {
        using var client = Client(new StubHandler(HttpStatusCode.ServiceUnavailable, "{\"error\":\"Notes is not enabled\"}"));

        var err = await Assert.ThrowsAsync<CliException>(() => client.GetAsync("/config"));
        Assert.Equal(ExitCode.Disabled, err.Code);
    }

    [Fact]
    public async Task BadRequestMapsToExitOne()
    {
        using var client = Client(new StubHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad title\"}"));

        var err = await Assert.ThrowsAsync<CliException>(() => client.GetAsync("/config"));
        Assert.Equal(ExitCode.Usage, err.Code);
        Assert.Equal("bad title", err.Message);
    }

    [Fact]
    public async Task ServerErrorMapsToExitFive()
    {
        using var client = Client(new StubHandler(HttpStatusCode.InternalServerError, ""));

        var err = await Assert.ThrowsAsync<CliException>(() => client.GetAsync("/config"));
        Assert.Equal(ExitCode.Backend, err.Code);
        Assert.Contains("HTTP 500", err.Message);
    }

    [Fact]
    public async Task NonJsonSuccessMapsToExitFive()
    {
        using var client = Client(new StubHandler(HttpStatusCode.OK, "<html>hi</html>"));

        var err = await Assert.ThrowsAsync<CliException>(() => client.GetAsync("/config"));
        Assert.Equal(ExitCode.Backend, err.Code);
        Assert.Contains("non-JSON", err.Message);
    }

    [Fact]
    public async Task TimeoutMapsToExitFour()
    {
        using var client = Client(
            new StubHandler(HttpStatusCode.OK, "{}", TimeSpan.FromSeconds(5)),
            TimeSpan.FromMilliseconds(50));

        var err = await Assert.ThrowsAsync<CliException>(() => client.GetAsync("/config"));
        Assert.Equal(ExitCode.Network, err.Code);
        Assert.Contains("no response from https://queue.example", err.Message);
    }

    [Fact]
    public async Task TransportFailureMapsToExitFour()
    {
        using var client = Client(new StubHandler(new HttpRequestException(
            "boom",
            new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionRefused))));

        var err = await Assert.ThrowsAsync<CliException>(() => client.GetAsync("/config"));
        Assert.Equal(ExitCode.Network, err.Code);
        Assert.Contains("connection refused", err.Message);
        Assert.DoesNotContain("super-secret-key", err.Message);
    }
}
