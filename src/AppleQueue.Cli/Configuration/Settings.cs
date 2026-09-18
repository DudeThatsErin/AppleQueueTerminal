using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AppleQueue.Cli.Json;

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
    public string ConfigDir
    {
        get
        {
            var overridden = Get(ConfigDirVar);
            if (!string.IsNullOrEmpty(overridden)) return overridden;

            if (OperatingSystem.IsWindows())
            {
                var appData = Get("APPDATA");
                if (string.IsNullOrEmpty(appData))
                {
                    appData = Path.Combine(HomeDir(), "AppData", "Roaming");
                }

                return Path.Combine(appData, "applequeue");
            }

            var xdg = Get("XDG_CONFIG_HOME");
            var baseDir = string.IsNullOrEmpty(xdg) ? Path.Combine(HomeDir(), ".config") : xdg;
            return Path.Combine(baseDir, "applequeue");
        }
    }

    public string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public Settings Load()
    {
        var file = ReadConfigFile();
        var envUrl = Get(UrlVar);
        var envKey = Get(ApiKeyVar);
        var fileUrl = file.Str("url");
        var fileKey = file.Str("apiKey");

        var url = !string.IsNullOrEmpty(envUrl) ? envUrl : fileUrl ?? string.Empty;
        var apiKey = !string.IsNullOrEmpty(envKey) ? envKey : fileKey ?? string.Empty;

        return new Settings(
            string.IsNullOrEmpty(url) ? string.Empty : NormalizeUrl(url),
            apiKey,
            new SettingsSource(
                !string.IsNullOrEmpty(envUrl) ? "env" : !string.IsNullOrEmpty(fileUrl) ? "config file" : "unset",
                !string.IsNullOrEmpty(envKey) ? "env" : !string.IsNullOrEmpty(fileKey) ? "config file" : "unset"),
            ConfigPath);
    }

    public Settings Require()
    {
        var settings = Load();
        if (!settings.IsConfigured)
        {
            throw new CliException(
                $"not configured. Run `applequeue configure`, or set {UrlVar} and {ApiKeyVar}.",
                ExitCode.Config);
        }

        return settings;
    }

    public string Save(string url, string apiKey)
    {
        Directory.CreateDirectory(ConfigDir);
        var file = ConfigPath;
        var payload = new JsonObject { ["url"] = url, ["apiKey"] = apiKey };
        System.IO.File.WriteAllText(file, payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        RestrictPermissions(file);
        return file;
    }

    /// <summary>
    /// The keychain is the goal; until that ships, at least make the fallback file
    /// unreadable by other accounts on the machine.
    /// </summary>
    private static void RestrictPermissions(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            var user = Environment.UserName;
            if (string.IsNullOrEmpty(user)) return;
            try
            {
                using var process = Process.Start(new ProcessStartInfo("icacls")
                {
                    ArgumentList = { file, "/inheritance:r", "/grant:r", $"{user}:F" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                });
                process?.WaitForExit(5000);
            }
            catch (Exception)
            {
                // icacls is best-effort; the file still lives in the per-user profile.
            }

            return;
        }

        try
        {
            System.IO.File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // Best effort on exotic filesystems.
        }
    }

    /// <summary>
    /// Trailing slashes and an accidental /api suffix are the two things people
    /// paste; normalize both rather than failing later with a confusing 404.
    /// Adds https:// when no scheme is given, and rejects plain http for anything
    /// other than localhost / 127.0.0.1.
    /// </summary>
    public static string NormalizeUrl(string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0) throw new CliException("backend URL is required", ExitCode.Usage);

        var hasScheme = value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        // https is the right default for a pasted hostname, but a scheme-less
        // localhost is a dev backend that almost never speaks TLS.
        var withScheme = hasScheme
            ? value
            : $"{(IsLocalHost(value) ? "http" : "https")}://{value}";

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var url)
            || string.IsNullOrEmpty(url.Host))
        {
            throw new CliException($"not a valid URL: {value}", ExitCode.Usage);
        }

        var isLocal = url.Host is "localhost" or "127.0.0.1";
        if (url.Scheme != Uri.UriSchemeHttps && !isLocal)
        {
            throw new CliException(
                "backend URL must use https (http is allowed only for localhost)",
                ExitCode.Usage);
        }

        var path = url.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/api", StringComparison.Ordinal)) path = path[..^4];

        return $"{url.GetLeftPart(UriPartial.Authority)}{path}";
    }

    private static bool IsLocalHost(string schemeless)
    {
        var host = schemeless.Split('/')[0].Split('?')[0];
        var colon = host.LastIndexOf(':');
        if (colon > 0) host = host[..colon];
        return host is "localhost" or "127.0.0.1";
    }

    private JsonObject ReadConfigFile()
    {
        try
        {
            var text = System.IO.File.ReadAllText(ConfigPath);
            return JsonNode.Parse(text) as JsonObject ?? [];
        }
        catch (Exception)
        {
            // A missing or corrupt config file simply means "nothing saved yet".
            return [];
        }
    }

    private string? Get(string name) => _env.TryGetValue(name, out var value) ? value : null;

    private string HomeDir()
    {
        var home = Get("HOME") ?? Get("USERPROFILE");
        return string.IsNullOrEmpty(home)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : home;
    }
}
