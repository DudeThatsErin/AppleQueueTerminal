using System.Collections;
using AppleQueue.Cli;
using AppleQueue.Cli.Commands;
using AppleQueue.Cli.Configuration;
using AppleQueue.Cli.Console;
using AppleQueue.Cli.Http;

var env = Environment.GetEnvironmentVariables()
    .Cast<DictionaryEntry>()
    .ToDictionary(e => (string)e.Key, e => e.Value as string, StringComparer.OrdinalIgnoreCase);

IConsoleIo io = new SystemConsoleIo();
ISettingsStore settings = new SettingsStore(env);

var context = new CommandContext
{
    Io = io,
    Settings = settings,
    ClientFactory = () =>
    {
        var resolved = settings.Require();
        var timeout = int.TryParse(env.GetValueOrDefault(SettingsStore.TimeoutVar), out var ms)
            ? TimeSpan.FromMilliseconds(ms)
            : AppleQueueClient.DefaultTimeout;
        return new AppleQueueClient(resolved.Url, resolved.ApiKey, timeout);
    },
};

return await new Cli(context).RunAsync(args);
