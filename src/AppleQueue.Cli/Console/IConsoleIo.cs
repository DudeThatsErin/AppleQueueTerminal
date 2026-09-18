using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AppleQueue.Cli.Console;

/// <summary>
/// All output goes through here so tests can capture it, and so the split stays
/// honest: machine-readable output on stdout alone, progress and hints on stderr.
/// </summary>
public interface IConsoleIo
{
    void Out(string text);
    void Note(string text);
    void Json(object value);

    /// <param name="secret">Do not echo typed characters (API keys).</param>
    Task<string> AskAsync(string prompt, bool secret = false, CancellationToken ct = default);

    Task<string> ReadStdinAsync(CancellationToken ct = default);
}

public sealed class SystemConsoleIo : IConsoleIo
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void Out(string text) => System.Console.Out.Write($"{text}\n");

    public void Note(string text) => System.Console.Error.Write($"{text}\n");

    public void Json(object value)
    {
        var text = value is JsonNode node
            ? node.ToJsonString(JsonOptions)
            : JsonSerializer.Serialize(value, JsonOptions);
        System.Console.Out.Write($"{text}\n");
    }

    public Task<string> AskAsync(string prompt, bool secret = false, CancellationToken ct = default)
    {
        // A redirected stdin has no terminal to mask, and ReadKey would throw.
        if (System.Console.IsInputRedirected)
        {
            System.Console.Error.Write(prompt);
            return Task.FromResult((System.Console.ReadLine() ?? string.Empty).Trim());
        }

        System.Console.Error.Write(prompt);
        if (!secret) return Task.FromResult((System.Console.ReadLine() ?? string.Empty).Trim());

        var buffer = new StringBuilder();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var key = System.Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0) buffer.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
        }

        System.Console.Error.Write("\n");
        return Task.FromResult(buffer.ToString().Trim());
    }

    public async Task<string> ReadStdinAsync(CancellationToken ct = default)
    {
        using var reader = new StreamReader(System.Console.OpenStandardInput(), Encoding.UTF8);
        var text = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        return text.TrimEnd('\r', '\n');
    }
}
