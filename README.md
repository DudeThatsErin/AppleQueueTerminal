# applequeue

A terminal client for [Apple Queue](https://applequeue.erinskidds.com). Queue Apple **Journal** entries, **Notes**, **Reminders**, and **Calendar** events from a shell — the same endpoints the browser extension uses, no extension and no browser. Your existing Apple Shortcut still delivers each item into Apple's apps.

## Install

```powershell
winget install applequeue
```

```bash
npm install -g applequeue
```

The winget package is a standalone binary with Node.js built in — nothing else to install. The npm package needs Node.js 18.17 or newer and has zero runtime dependencies.

Prefer a direct download? Grab a single binary for your platform from the [latest release](https://github.com/DudeThatsErin/AppleQueueTerminal/releases/latest), rename it to `applequeue`, and put it on your `PATH`. `SHA256SUMS` is published alongside.

Upgrade with `winget upgrade applequeue` or `npm update -g applequeue`; uninstall with `winget uninstall applequeue` or `npm uninstall -g applequeue`.

## Set up

```bash
applequeue configure
```

It asks for your Apple Queue backend URL and API key, verifies the connection, and saves them.

- Windows: `%APPDATA%\applequeue\config.json`, locked to your user account with `icacls`.
- macOS/Linux: `~/.config/applequeue/config.json`, mode `0600`.

The key is never echoed, printed, or included in an error message. For scripts and CI, the environment takes precedence over the saved file:

```
APPLE_QUEUE_URL=https://your-backend.example.com
APPLE_QUEUE_API_KEY=...
APPLE_QUEUE_TIMEOUT_MS=15000
```

Then confirm everything is wired up:

```bash
applequeue doctor
```

## Use

```bash
applequeue journal add "Today" --body "Long walk, good coffee."
applequeue journal add "Catch-up" --date yesterday --stdin < notes.md

applequeue note add "Trip ideas" --body "Kyoto in spring" --folder Travel
git log --oneline -20 | applequeue note add "Release notes" --stdin

applequeue reminder add "Pick up milk" --list Errands --due "tomorrow 9am" --priority high
applequeue event add "Dentist" --start "2026-09-15T09:00" --duration 90m --location "Main St"
applequeue event add "Standup" --start "+1h" --duration 15m --invitee a@example.com,b@example.com

applequeue list journal          # journal | notes | reminders | events
applequeue remove notes <id>
```

`--json` prints the backend's raw response on stdout and nothing else, so it pipes straight into `jq`.

Dates accept ISO (`2026-09-15T09:00`, read as local time), relative offsets (`+2h`, `+30m`, `+3d`), and shorthand (`today`, `tonight`, `tomorrow 9am`, `yesterday`). `--duration` takes `90m`, `2h`, `1d`; events default to one hour.

Because Shortcuts can't send invitations, an event with `--invitee` also queues a reminder nudge — the CLI says so when that happens.

`applequeue --help` lists every flag.

## Exit codes

| Code | Meaning |
| ---- | ------- |
| 0 | success |
| 1 | usage or local validation error |
| 2 | unauthorized (backend rejected the key) |
| 3 | that module is disabled on your deployment |
| 4 | network failure or timeout |
| 5 | other backend error |
| 6 | not configured |

## Windows

Both install methods give you an `applequeue` command in PowerShell, cmd.exe, and Git Bash.

```powershell
winget install applequeue
```

Close and reopen the terminal afterwards so the new command is on your `PATH`, then:

```powershell
applequeue configure
```

```powershell
applequeue doctor
```

```powershell
applequeue journal add "Windows test" --body "Queued from PowerShell"
```

```powershell
applequeue list journal
```

Run your Apple Shortcut and confirm the item lands in the Apple app exactly once.

Windows quirks worth knowing:

- Quote anything containing spaces: `--due "tomorrow 9am"`, not `--due tomorrow 9am`.
- If PowerShell swallows a flag value, use the equals form: `--title="Trip ideas"`.
- Pipe with `Get-Content notes.md | applequeue note add "Notes" --stdin`.
- In cmd.exe, set overrides with `set APPLE_QUEUE_URL=...` rather than `$env:`.
- SmartScreen may warn the first time you run an unsigned downloaded binary. Installing through winget or npm avoids that.

## Security

- The API key is sent only as the `x-api-key` header to your configured backend — never in a URL, never logged.
- Tests assert that the key cannot appear in error output or `--json`.
- Plain `http://` is rejected for anything except `localhost`.
- The config file never ships in the package and is excluded from git.

## What this does not do

- It does not talk to Apple. Your Shortcut still creates the items.
- It does not upload attachments yet; use the extension for those.
- It does not use the backend's AI parsing endpoint, which needs client-supplied provider credentials.

## Developing

```bash
git clone https://github.com/DudeThatsErin/AppleQueueTerminal.git && cd AppleQueueTerminal && npm install
npm test              # 19 unit, packaging, and end-to-end tests
node test/fake-backend.js   # a stand-in backend on 127.0.0.1:8787, key "test-key"
npm run build:win     # standalone Windows binaries into dist/
```

Release steps, including how the winget package is published, are in [RELEASING.md](RELEASING.md).
