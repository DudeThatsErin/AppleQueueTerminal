# AppleQueue Terminal

`applequeue` — queue Apple Journal, Notes, Reminders, and Calendar items from the
terminal. The CLI only writes to a backend queue; your existing Apple Shortcut
collects the items and creates them in Apple's apps.

## Install

```bash
dotnet tool install -g applequeue
```

Requires the .NET 8 runtime or later.

> **Moving from npm?** Version 2.0.0 is a C# rewrite distributed as a dotnet
> tool. The npm package `applequeue` is deprecated and receives no further
> updates. Run `npm uninstall -g applequeue` and install as above — the
> commands, flags, and exit codes are unchanged.

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

Install from source:

```bash
dotnet pack src/AppleQueue.Cli -c Release -o artifacts && dotnet tool install -g --add-source artifacts applequeue
```

## Releasing

Tag the commit and push the tag; [release.yml](.github/workflows/release.yml)
packs, tests, and publishes to NuGet.org via Trusted Publishing (no stored API
key). The package version comes from the tag.

```bash
git tag v2.0.0 && git push origin v2.0.0
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
