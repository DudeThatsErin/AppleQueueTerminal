// Generates the three winget manifests for a release into
// dist/winget/<version>/. They are submitted to microsoft/winget-pkgs, which is
// what makes `winget install applequeue` work for everyone else.
//
//   node scripts/build.js win-x64 win-arm64
//   node scripts/winget.js
//
// The installer type is "portable": winget drops applequeue.exe in its links
// directory and registers the `applequeue` command, so there is no installer to
// sign and nothing to uninstall by hand. That is the same shape as most CLI
// tools in the community repo.

import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const pkg = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

const VERSION = process.env.APPLEQUEUE_VERSION || pkg.version;
const ID = 'DudeThatsErin.AppleQueue';
const REPO = 'https://github.com/DudeThatsErin/AppleQueueTerminal';
const RELEASE = `${REPO}/releases/download/v${VERSION}`;
const MANIFEST_VERSION = '1.6.0';
const RELEASE_DATE = new Date().toISOString().slice(0, 10);

function checksums() {
  const text = readFileSync(join(root, 'dist', 'SHA256SUMS'), 'utf8');
  const map = new Map();
  for (const line of text.trim().split('\n')) {
    const [hash, file] = line.trim().split(/\s+/);
    map.set(file, hash);
  }
  return map;
}

function installerEntry(architecture, file, sums) {
  const hash = sums.get(file);
  if (!hash) throw new Error(`dist/SHA256SUMS has no entry for ${file} -- run scripts/build.js first`);
  return [
    `- Architecture: ${architecture}`,
    `  InstallerUrl: ${RELEASE}/${file}`,
    `  InstallerSha256: ${hash}`,
    '  PortableCommandAlias: applequeue',
  ].join('\n');
}

function main() {
  const sums = checksums();
  const outDir = join(root, 'dist', 'winget', VERSION);
  mkdirSync(outDir, { recursive: true });

  const installers = [
    installerEntry('x64', 'applequeue-win-x64.exe', sums),
    // arm64 is optional: skip it rather than fail if it was not built.
    sums.has('applequeue-win-arm64.exe') ? installerEntry('arm64', 'applequeue-win-arm64.exe', sums) : null,
  ].filter(Boolean);

  const version = `# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.${MANIFEST_VERSION}.schema.json
PackageIdentifier: ${ID}
PackageVersion: ${VERSION}
DefaultLocale: en-US
ManifestType: version
ManifestVersion: ${MANIFEST_VERSION}
`;

  const installer = `# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.${MANIFEST_VERSION}.schema.json
PackageIdentifier: ${ID}
PackageVersion: ${VERSION}
InstallerType: portable
Commands:
- applequeue
ReleaseDate: ${RELEASE_DATE}
Installers:
${installers.join('\n')}
ManifestType: installer
ManifestVersion: ${MANIFEST_VERSION}
`;

  const locale = `# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.${MANIFEST_VERSION}.schema.json
PackageIdentifier: ${ID}
PackageVersion: ${VERSION}
PackageLocale: en-US
Publisher: Erin Skidds
PublisherUrl: https://applequeue.erinskidds.com
PublisherSupportUrl: ${REPO}/issues
PackageName: AppleQueue
PackageUrl: ${REPO}
License: MIT
LicenseUrl: ${REPO}/blob/main/LICENSE
ShortDescription: ${pkg.description}
Description: |-
  A terminal client for Apple Queue. Queue Apple Journal entries, Notes,
  Reminders, and Calendar events from a shell by sending them to your own
  Apple Queue backend. Your existing Apple Shortcut still delivers each item
  into Apple's apps. Requires an Apple Queue backend URL and API key.
Moniker: applequeue
Tags:
- apple
- calendar
- cli
- journal
- notes
- productivity
- reminders
- shortcuts
ReleaseNotesUrl: ${REPO}/releases/tag/v${VERSION}
ManifestType: defaultLocale
ManifestVersion: ${MANIFEST_VERSION}
`;

  writeFileSync(join(outDir, `${ID}.yaml`), version);
  writeFileSync(join(outDir, `${ID}.installer.yaml`), installer);
  writeFileSync(join(outDir, `${ID}.locale.en-US.yaml`), locale);
  process.stderr.write(`winget: wrote manifests to dist/winget/${VERSION}/\n`);
}

main();
