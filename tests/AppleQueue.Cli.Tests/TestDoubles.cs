using System.Text.Json.Nodes;
using AppleQueue.Cli.Console;
using AppleQueue.Cli.Http;

namespace AppleQueue.Cli.Tests;

/// <summary>Captures the stdout/stderr split so tests can assert that --json stays clean.</summary>
public sealed class FakeConsoleIo : IConsoleIo
{
    public List<string> StdOut { get; } = [];
    public List<string> StdErr { get; } = [];
    public Queue<string> Answers { get; } = new();
    public string Stdin { get; set; } = string.Empty;

    public void Out(string text) => StdOut.Add(text);

    public void Note(string text) => StdErr.Add(text);

    public void Json(object value) => throw new NotImplementedException();

    public Task<string> AskAsync(string prompt, bool secret = false, CancellationToken ct = default)
        => Task.FromResult(Answers.Count > 0 ? Answers.Dequeue() : string.Empty);

    public Task<string> ReadStdinAsync(CancellationToken ct = default) => Task.FromResult(Stdin);
}

/// <summary>
/// Stand-in for the deployed backend. Record the requests, hand back canned
/// responses, and throw CliException to exercise the error-to-exit-code mapping.
/// </summary>
public sealed class FakeAppleQueueClient : IAppleQueueClient
{
    public List<(string Method, string Path, JsonObject? Body)> Requests { get; } = [];
    public Dictionary<string, JsonObject> Responses { get; } = [];

    public Task<JsonObject> GetAsync(string path, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<JsonObject> PostAsync(string path, JsonObject body, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<JsonObject> DeleteAsync(string path, JsonObject body, CancellationToken ct = default)
        => throw new NotImplementedException();
}
