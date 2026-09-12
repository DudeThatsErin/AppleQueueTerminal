# Releasing

The goal is that nobody installing `applequeue` ever sees this repository. Two
distribution channels carry that: npm for anyone who has Node, winget for
Windows users who do not.

## One-time setup

These are the only steps that need credentials, and they cannot be automated
away.

1. **Claim the npm name.** `applequeue` was unclaimed as of the last check.
   ```bash
   npm login
   npm publish --access public --dry-run   # sanity check the file list
   ```
2. **Add repository secrets** on GitHub:
   - `NPM_TOKEN` — an npm automation token with publish rights.
   - `WINGET_TOKEN` — a classic PAT with `public_repo` scope, on an account that
     has forked [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).
     The release workflow uses it to open the submission PR.
3. **Fork winget-pkgs** with that account. The first submission is reviewed by
   Microsoft; later versions are usually auto-merged.

## Cutting a release

```bash
npm version minor        # bumps package.json and tags
git push --follow-tags
```

`.github/workflows/release.yml` then, on the tag:

1. runs the test suite on Linux, Windows, and macOS against Node 18/20/22;
2. publishes to npm with provenance;
3. cross-builds six standalone binaries and attaches them, plus `SHA256SUMS`, to
   the GitHub release;
4. opens the winget-pkgs pull request for `DudeThatsErin.AppleQueue`.

Prereleases (a tag containing `-`) skip step 4.

## How the standalone binaries work

`scripts/build.js` bundles the CLI into one CommonJS file with esbuild, turns it
into a Node [Single Executable Application](https://nodejs.org/api/single-executable-applications.html)
blob, and injects that blob into an unmodified official `node` binary with
postject. The result is one file that needs no Node.js installed.

Every target cross-builds from Linux, because postject patches the executable
rather than running it:

```bash
node scripts/build.js win-x64 win-arm64 linux-x64 linux-arm64 darwin-x64 darwin-arm64
```

The embedded Node version is pinned in `scripts/build.js` (`NODE_VERSION`). Bump
it deliberately — it is what users actually run.

macOS binaries are unsigned, so a person who downloads one directly has to clear
Gatekeeper. That is why Homebrew, not a raw download, is the right next channel
for macOS; npm covers it in the meantime.

## Building the winget manifests by hand

The workflow does this, but to inspect or submit manually:

```bash
node scripts/build.js win-x64 win-arm64
node scripts/winget.js         # writes dist/winget/<version>/
winget validate --manifest dist/winget/<version>/
```

Then copy that folder to
`manifests/d/DudeThatsErin/AppleQueue/<version>/` in a winget-pkgs fork and open
a PR.

## After publishing

Point the website at the CLI: add the release URLs to
`/var/www/applequeue.erinskidds.com/src/config.js`, add a short Terminal section
with the two install commands and one `applequeue journal add` example, note
that the Shortcut completes delivery, then `npm run build` and deploy `dist/`.
Never put an API key or an unauthenticated backend example on the site.
