namespace AppleQueue.Cli;

/// <summary>Stable exit codes so shell automation can branch on failures.</summary>
public static class ExitCode
{
    public const int Ok = 0;
    public const int Usage = 1;      // bad flags, missing required input, local validation failure
    public const int Auth = 2;       // 401 from the backend
    public const int Disabled = 3;   // module turned off on the deployment (503)
    public const int Network = 4;    // DNS/TLS/timeout/connection refused
    public const int Backend = 5;    // 4xx/5xx the CLI cannot classify further
    public const int Config = 6;     // no backend URL or API key configured
}

/// <summary>An error that maps onto a CLI exit code and prints without a stack trace.</summary>
public sealed class CliException : Exception
{
    public CliException(string message, int code = ExitCode.Usage)
        : base(message) => Code = code;

    public int Code { get; }
}
