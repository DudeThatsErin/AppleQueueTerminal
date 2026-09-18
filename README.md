# AppleQueue Terminal

`applequeue` — queue Apple Journal, Notes, Reminders, and Calendar items from the
terminal. The CLI only writes to a backend queue; your existing Apple Shortcut
collects the items and creates them in Apple's apps.

This is a C# rewrite. The tree is currently scaffolding: the command surface,
exit codes, and help text are in place, and the implementation bodies throw
`NotImplementedException` with the behaviour to restore described in XML docs.

## Layout

```
src/AppleQueue.Cli/
  Program.cs                  composition root (env, console, settings, client)
  Cli.cs                      dispatch + help text
  ExitCode.cs                 exit codes and CliException
  Commands/                   one file per user-facing command
  Configuration/Settings.cs   env -> config file precedence, URL normalization
  Http/                       HTTP client, error-to-exit-code mapping
  Parsing/                    flag parser, date/duration input
  Console/                    stdout vs stderr, prompts, stdin
tests/AppleQueue.Cli.Tests/
```

## Build

```bash
dotnet test
```

Run the CLI locally:

```bash
dotnet run --project src/AppleQueue.Cli -- --help
```

Install as a global tool from source:

```bash
dotnet pack -c Release && dotnet tool install -g --add-source src/AppleQueue.Cli/nupkg applequeue
```

## Configuration

Read from the environment first, then the file written by `applequeue configure`:

- `APPLE_QUEUE_URL`
- `APPLE_QUEUE_API_KEY`
- `APPLE_QUEUE_TIMEOUT_MS`
- `APPLE_QUEUE_CONFIG_DIR` (overrides `%APPDATA%\applequeue` / `~/.config/applequeue`)

The API key is never printed, logged, or included in error output.

## Exit codes

| Code | Meaning |
| ---- | ------- |
| 0 | ok |
| 1 | usage / local validation |
| 2 | unauthorized |
| 3 | module disabled on the deployment |
| 4 | network |
| 5 | backend |
| 6 | not configured |
