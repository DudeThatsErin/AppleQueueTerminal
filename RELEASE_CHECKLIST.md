# AppleQueueTerminal release checklist

Use this as a GitHub issue, project note, or release checklist. The CLI sends items to a person's existing Apple Queue backend; their Apple Shortcut still creates the item in Apple Notes, Reminders, Calendar, or Journal.

## 1. Confirm the backend contract

- [ ] Read the current backend handlers for Notes, Reminders, Calendar, and Journal.
- [ ] Document the exact request/response payloads in `docs/api-contract.md`.
- [ ] Confirm the modules/defaults returned by `GET /api/config`.
- [ ] Test `GET /api/health` with no key, invalid key, and a valid key against a test backend.
- [ ] Confirm the exact Journal endpoint and payload before promising a `journal add` command.

## 2. Make release decisions

- [ ] Use Node.js + TypeScript and npm for v1, or explicitly select another runtime/distribution model.
- [ ] Verify and reserve the `applequeue` package name.
- [ ] Confirm the public command name is `applequeue`.
- [ ] Choose supported Node LTS versions and versioning policy.
- [ ] Decide whether v1 ships as npm only, GitHub binaries only, or both.
- [ ] Record the final public install command.

## 3. Scaffold the CLI

- [ ] Add `package.json`, TypeScript config, linting/formatting, and a test runner.
- [ ] Add executable CLI entry point and useful `applequeue --help` output.
- [ ] Create command groups: `configure`, `doctor`, `note add`, `reminder add`, `event add`.
- [ ] Reserve `journal add` until the backend contract is verified.
- [ ] Make a fresh clone pass install, help, lint, type-check, and test commands.

## 4. Configure securely

- [ ] Prompt for backend HTTPS URL and API key with `applequeue configure`.
- [ ] Normalize the backend URL without ever printing the API key.
- [ ] Store secrets in the OS keychain where available.
- [ ] Provide a documented restrictive-permission config-file fallback.
- [ ] Support `APPLE_QUEUE_URL` and `APPLE_QUEUE_API_KEY` environment overrides for automation.
- [ ] Implement `applequeue doctor` using health/config endpoints.
- [ ] Make doctor show the backend URL, enabled modules, and actionable connection errors.
- [ ] Redact keys from errors, debug logs, snapshots, fixtures, and tests.

## 5. Implement queue commands

- [ ] Build a shared HTTP client with timeouts, JSON validation, `x-api-key`, and mapped errors.
- [ ] Implement `note add` with `--title`, `--body`, `--folder`, `--stdin`, and `--json`.
- [ ] Implement `reminder add` with local validation for required fields/dates.
- [ ] Implement `event add` with local validation for start/end date-times.
- [ ] Implement `journal add` only after its route and payload are confirmed.
- [ ] Fetch module configuration before creates and reject disabled modules clearly.
- [ ] Use backend defaults when destination options are omitted.
- [ ] Verify each command queues an item visible in the backend dashboard.
- [ ] Verify an existing configured Shortcut delivers and acknowledges each supported item exactly once.

## 6. Test and harden

- [ ] Unit-test flags, date conversion, config precedence, redaction, and payload serialization.
- [ ] Mock success, 401, disabled-module, malformed-response, timeout, and 5xx cases.
- [ ] Define stable exit codes for shell automation.
- [ ] Ensure `--json` is machine-readable and does not mix with progress output.
- [ ] Run end-to-end tests against a disposable backend and real Shortcut setup.
- [ ] Verify platform behaviour on every OS the CLI claims to support.
- [ ] Confirm no API keys or user content appear in repository history or test output.

## 7. Package and publish

- [ ] Complete README: prerequisites, install, configure, examples, automation env vars, security, upgrade, and uninstall.
- [ ] Add CHANGELOG and release checklist/process.
- [ ] Ensure package contents omit tests, local configuration, secrets, and unrelated development files.
- [ ] Automate lint, type-check, tests, and a package dry-run.
- [ ] Publish a prerelease.
- [ ] Test installation from a clean account or machine.
- [ ] Publish v1.0.0, tag it, and create release notes with tested install/upgrade instructions.

## 8. Release on applequeue.erinskidds.com

- [ ] Add canonical CLI repo/package URLs to `/var/www/applequeue.erinskidds.com/src/config.js`.
- [ ] Add a concise Terminal/CLI section to the public site.
- [ ] Include the verified install command, `applequeue configure`, and one create example.
- [ ] Explain that the configured Apple Shortcut completes delivery to Apple apps.
- [ ] Do not include API keys, test keys, or unauthenticated backend examples.
- [ ] Run `npm run build` in `/var/www/applequeue.erinskidds.com`.
- [ ] Verify site links and routes locally, deploy `dist/`, and smoke-test the live domain.

## 9. Support after launch

- [ ] Add GitHub issue templates for install, connection, and command bugs.
- [ ] Warn issue reporters never to paste API keys.
- [ ] Monitor early onboarding issues and fix documentation gaps.
- [ ] Define backend/CLI compatibility and deprecation policy.
- [ ] Decide later whether to add AI input, completions, standalone binaries, batch input, or direct macOS automation.

