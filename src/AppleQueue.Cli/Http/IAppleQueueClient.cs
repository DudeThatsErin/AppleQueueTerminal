using System.Text.Json.Nodes;

namespace AppleQueue.Cli.Http;

/// <summary>
/// One HTTP client for every command: timeouts, JSON validation, the x-api-key
/// header, and backend errors mapped onto the CLI's exit codes.
/// Every path is relative to {baseUrl}/api.
/// </summary>
public interface IAppleQueueClient
{
    Task<JsonObject> GetAsync(string path, CancellationToken ct = default);
    Task<JsonObject> PostAsync(string path, JsonObject body, CancellationToken ct = default);
    Task<JsonObject> DeleteAsync(string path, JsonObject body, CancellationToken ct = default);
}

public sealed class AppleQueueClient : IAppleQueueClient, IDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public AppleQueueClient(string baseUrl, string apiKey, TimeSpan? timeout = null, HttpMessageHandler? handler = null)
        => throw new NotImplementedException();

    public Task<JsonObject> GetAsync(string path, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<JsonObject> PostAsync(string path, JsonObject body, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<JsonObject> DeleteAsync(string path, JsonObject body, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>
    /// Error mapping to preserve:
    ///   timeout / DNS / refused / bad TLS -> ExitCode.Network, message names the
    ///     URL and the transport reason but NEVER the API key
    ///   401 -> ExitCode.Auth
    ///   503 with "not enabled" -> ExitCode.Disabled
    ///   400 -> ExitCode.Usage
    ///   any other non-2xx, or a 2xx body that is not a JSON object -> ExitCode.Backend
    /// The backend's own {"error": "..."} string is used as the message when present.
    /// </summary>
    private Task<JsonObject> SendAsync(HttpMethod method, string path, JsonObject? body, CancellationToken ct)
        => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}

/// <summary>Shape of GET /api/config, fetched before any write.</summary>
public sealed record BackendConfig(
    IReadOnlyDictionary<string, bool> Modules,
    IReadOnlyDictionary<string, string> Defaults,
    IReadOnlyDictionary<string, bool> Features)
{
    /// <summary>
    /// Throws ExitCode.Disabled when the deployment has the module turned off, so
    /// a disabled module fails before we send content anywhere.
    /// </summary>
    public void Require(string module, string label) => throw new NotImplementedException();

    public string EnabledList() => throw new NotImplementedException();
}
