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
    public void Out(string text) => System.Console.Out.Write($"{text}\n");

    public void Note(string text) => System.Console.Error.Write($"{text}\n");

    // TODO: JsonSerializer with WriteIndented = true and camelCase naming, trailing newline.
    public void Json(object value) => throw new NotImplementedException();

    // TODO: when secret, read with Console.ReadKey(intercept: true); fall back to a
    // plain line read when stdin is redirected (Console.IsInputRedirected).
    public Task<string> AskAsync(string prompt, bool secret = false, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<string> ReadStdinAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
}
