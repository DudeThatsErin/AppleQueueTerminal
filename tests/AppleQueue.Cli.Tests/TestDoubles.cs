using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AppleQueue.Cli.Configuration;
using AppleQueue.Cli.Console;
using AppleQueue.Cli.Http;

namespace AppleQueue.Cli.Tests;

/// <summary>Captures the stdout/stderr split so tests can assert that --json stays clean.</summary>
public sealed class FakeConsoleIo : IConsoleIo
{
    public List<string> StdOut { get; } = [];
    public List<string> StdErr { get; } = [];
    public List<object> JsonOut { get; } = [];
    public Queue<string> Answers { get; } = new();
    public string Stdin { get; set; } = string.Empty;
    public List<(string Prompt, bool Secret)> Prompts { get; } = [];

    public void Out(string text) => StdOut.Add(text);

    public void Note(string text) => StdErr.Add(text);

    public void Json(object value)
    {
        JsonOut.Add(value);
        StdOut.Add(value is JsonNode node ? node.ToJsonString() : value.ToString() ?? string.Empty);
    }

    public Task<string> AskAsync(string prompt, bool secret = false, CancellationToken ct = default)
    {
        Prompts.Add((prompt, secret));
        return Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : string.Empty);
    }

    public Task<string> ReadStdinAsync(CancellationToken ct = default) => Task.FromResult(Stdin);

    public string AllText() => string.Join("\n", StdOut.Concat(StdErr));
}

/// <summary>
/// Stand-in for the deployed backend. Records the requests, hands back canned
/// responses, and can throw CliException to exercise the exit-code mapping.
/// </summary>
public sealed class FakeAppleQueueClient : IAppleQueueClient
{
    public List<(string Method, string Path, JsonObject? Body)> Requests { get; } = [];
    public Dictionary<string, JsonObject> Responses { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Exception> Failures { get; } = new(StringComparer.Ordinal);

    public FakeAppleQueueClient WithConfig(params string[] enabledModules)
    {
        var modules = new JsonObject();
        foreach (var module in new[] { "journal", "notes", "reminders", "calendar" })
        {
            modules[module] = enabledModules.Contains(module);
        }

        Responses["GET /config"] = new JsonObject
        {
            ["modules"] = modules,
            ["defaults"] = new JsonObject { ["notesFolder"] = "Inbox", ["reminderList"] = "Inbox", ["calendar"] = "Home" },
        };
        return this;
    }

    public FakeAppleQueueClient With(string key, JsonObject response)
    {
        Responses[key] = response;
        return this;
    }

    public JsonObject? BodyFor(string key) =>
        Requests.FirstOrDefault(r => $"{r.Method} {r.Path}" == key).Body;

    public Task<JsonObject> GetAsync(string path, CancellationToken ct = default) => Handle("GET", path, null);

    public Task<JsonObject> PostAsync(string path, JsonObject body, CancellationToken ct = default) => Handle("POST", path, body);

    public Task<JsonObject> UploadAsync(string path, string filename, byte[] bytes, string mimeType, CancellationToken ct = default)
        => Handle("POST", path, new JsonObject { ["name"] = filename });

    public Task<JsonObject> DeleteAsync(string path, JsonObject body, CancellationToken ct = default) => Handle("DELETE", path, body);

    private Task<JsonObject> Handle(string method, string path, JsonObject? body)
    {
        var key = $"{method} {path}";
        Requests.Add((method, path, body));
        if (Failures.TryGetValue(key, out var failure)) throw failure;
        return Task.FromResult(Responses.TryGetValue(key, out var response) ? response : []);
    }
}

/// <summary>Serves canned HTTP responses so the real client's mapping can be tested.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly Exception? _throw;
    private readonly TimeSpan _delay;

    public StubHandler(HttpStatusCode status, string body, TimeSpan? delay = null)
    {
        _status = status;
        _body = body;
        _delay = delay ?? TimeSpan.Zero;
    }

    public StubHandler(Exception toThrow)
    {
        _throw = toThrow;
        _status = HttpStatusCode.OK;
        _body = string.Empty;
    }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        if (_throw is not null) throw _throw;
        if (_delay > TimeSpan.Zero) await Task.Delay(_delay, ct).ConfigureAwait(false);

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json"),
        };
    }
}

internal static class TestContext
{
    /// <summary>A settings store backed by an in-memory environment and a temp dir.</summary>
    public static SettingsStore Store(string? url = null, string? apiKey = null, string? configDir = null)
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (url is not null) env["APPLE_QUEUE_URL"] = url;
        if (apiKey is not null) env["APPLE_QUEUE_API_KEY"] = apiKey;
        env["APPLE_QUEUE_CONFIG_DIR"] = configDir ?? Path.Combine(Path.GetTempPath(), "applequeue-tests", Guid.NewGuid().ToString("n"));
        return new SettingsStore(env);
    }
}
