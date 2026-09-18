namespace AppleQueue.Cli.Configuration;

public sealed record SettingsSource(string Url, string ApiKey);

/// <summary>Resolved backend settings and where each value came from.</summary>
public sealed record Settings(string Url, string ApiKey, SettingsSource Source, string File)
{
    public bool IsConfigured => !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(ApiKey);
}

/// <summary>
/// Where the backend URL and API key come from, in precedence order:
///   1. environment (APPLE_QUEUE_URL / APPLE_QUEUE_API_KEY) — for CI and scripts
///   2. the config file written by `applequeue configure`
/// The key is never printed, logged, or included in error output.
/// </summary>
public interface ISettingsStore
{
    Settings Load();

    /// <summary>Same as <see cref="Load"/> but throws ExitCode.Config when unset.</summary>
    Settings Require();

    /// <summary>Writes the config file with owner-only permissions; returns its path.</summary>
    string Save(string url, string apiKey);

    string ConfigPath { get; }
}

public sealed class SettingsStore : ISettingsStore
{
    public const string UrlVar = "APPLE_QUEUE_URL";
    public const string ApiKeyVar = "APPLE_QUEUE_API_KEY";
    public const string TimeoutVar = "APPLE_QUEUE_TIMEOUT_MS";
    public const string ConfigDirVar = "APPLE_QUEUE_CONFIG_DIR";

    private readonly IDictionary<string, string?> _env;

    public SettingsStore(IDictionary<string, string?> env) => _env = env;

    /// <summary>
    /// %APPDATA%\applequeue on Windows, $XDG_CONFIG_HOME/applequeue (default
    /// ~/.config/applequeue) elsewhere. APPLE_QUEUE_CONFIG_DIR overrides both.
    /// </summary>
    public string ConfigDir => throw new NotImplementedException();

    public string ConfigPath => throw new NotImplementedException();

    public Settings Load() => throw new NotImplementedException();

    public Settings Require() => throw new NotImplementedException();

    // TODO: create the directory, write {url, apiKey} as JSON, then restrict the
    // file: chmod 600 on Unix, icacls /inheritance:r /grant:r <user>:F on Windows
    // (best effort — it already lives in the per-user profile).
    public string Save(string url, string apiKey) => throw new NotImplementedException();

    /// <summary>
    /// Trailing slashes and an accidental /api suffix are the two things people
    /// paste; normalize both rather than failing later with a confusing 404.
    /// Adds https:// when no scheme is given, and rejects plain http for anything
    /// other than localhost / 127.0.0.1.
    /// </summary>
    public static string NormalizeUrl(string raw) => throw new NotImplementedException();
}
