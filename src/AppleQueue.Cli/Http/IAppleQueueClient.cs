using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;

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

    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly TimeSpan _timeout;
    private readonly HttpClient _http;

    public AppleQueueClient(string baseUrl, string apiKey, TimeSpan? timeout = null, HttpMessageHandler? handler = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _timeout = timeout is { TotalMilliseconds: > 0 } t ? t : DefaultTimeout;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        // Timeouts are enforced per request via the linked token, so the message
        // can name the URL and the elapsed budget instead of a bare TaskCanceled.
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public Task<JsonObject> GetAsync(string path, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, path, null, ct);

    public Task<JsonObject> PostAsync(string path, JsonObject body, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, path, body, ct);

    public Task<JsonObject> DeleteAsync(string path, JsonObject body, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, path, body, ct);

    private async Task<JsonObject> SendAsync(HttpMethod method, string path, JsonObject? body, CancellationToken ct)
    {
        var target = $"{_baseUrl}/api{path}";

        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutSource.Token);

        using var request = new HttpRequestMessage(method, target);
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        request.Headers.TryAddWithoutValidation("accept", "application/json");
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            throw new CliException(
                $"no response from {_baseUrl} after {(int)_timeout.TotalMilliseconds}ms",
                ExitCode.Network);
        }
        catch (HttpRequestException err)
        {
            // Never interpolate the key; only the URL and the transport reason.
            throw new CliException($"could not reach {_baseUrl}: {Reason(err)}", ExitCode.Network);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            JsonObject? parsed = null;
            try
            {
                parsed = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text) as JsonObject;
            }
            catch (System.Text.Json.JsonException)
            {
                parsed = null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var message = parsed.Str("error") ?? $"HTTP {(int)response.StatusCode}";

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new CliException(
                        $"unauthorized: the backend rejected this API key ({_baseUrl})",
                        ExitCode.Auth);
                }

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable
                    && message.Contains("not enabled", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CliException(message, ExitCode.Disabled);
                }

                throw new CliException(
                    message,
                    response.StatusCode == HttpStatusCode.BadRequest ? ExitCode.Usage : ExitCode.Backend);
            }

            if (parsed is null)
            {
                throw new CliException(
                    $"backend returned a non-JSON response (HTTP {(int)response.StatusCode})",
                    ExitCode.Backend);
            }

            return parsed;
        }
    }

    private static string Reason(Exception err)
    {
        for (var current = err; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case SocketException socket:
                    return socket.SocketErrorCode switch
                    {
                        SocketError.HostNotFound => "host not found",
                        SocketError.ConnectionRefused => "connection refused",
                        SocketError.TimedOut => "connection timed out",
                        _ => socket.SocketErrorCode.ToString(),
                    };
                case AuthenticationException:
                    return "the TLS handshake failed (bad or expired certificate)";
            }
        }

        return err.Message;
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Shape of GET /api/config, fetched before any write.</summary>
public sealed record BackendConfig(
    IReadOnlyDictionary<string, bool> Modules,
    IReadOnlyDictionary<string, string> Defaults,
    IReadOnlyDictionary<string, bool> Features)
{
    public static BackendConfig From(JsonObject response)
    {
        var modules = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (key, node) in response.Obj("modules") ?? [])
        {
            if (node is null) continue;
            var kind = node.GetValueKind();
            if (kind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
            {
                modules[key] = kind == System.Text.Json.JsonValueKind.True;
            }
        }

        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, node) in response.Obj("defaults") ?? [])
        {
            if (node is null) continue;
            if (node.GetValueKind() == System.Text.Json.JsonValueKind.String)
            {
                defaults[key] = node.GetValue<string>();
            }
        }

        var features = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (key, node) in response.Obj("features") ?? [])
        {
            if (node is null) continue;
            var kind = node.GetValueKind();
            if (kind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
            {
                features[key] = kind == System.Text.Json.JsonValueKind.True;
            }
        }

        return new BackendConfig(modules, defaults, features);
    }

    /// <summary>
    /// Throws ExitCode.Disabled when the deployment has the module turned off, so
    /// a disabled module fails before we send content anywhere.
    /// </summary>
    public void Require(string module, string label)
    {
        if (Modules.TryGetValue(module, out var enabled) && !enabled)
        {
            throw new CliException($"{label} is not enabled on this deployment", ExitCode.Disabled);
        }
    }

    public string EnabledList()
    {
        var on = Modules.Where(m => m.Value).Select(m => m.Key).ToArray();
        return on.Length > 0 ? string.Join(", ", on) : "none";
    }

    public string Default(string key) => Defaults.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : "-";
}
