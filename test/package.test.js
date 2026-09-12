// Guards the two things that break an install rather than a command: what ends
// up in the published tarball, and whether the version a user sees matches the
// package they installed.

import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { VERSION } from '../src/cli.js';

const root = fileURLToPath(new URL('..', import.meta.url));
const pkg = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8'));

test('`applequeue --version` reports the published package version', () => {
  assert.equal(VERSION, pkg.version);
});

test('the package has no runtime dependencies, so a global install is one download', () => {
  assert.equal(pkg.dependencies, undefined);
});

test('the published tarball carries the CLI and nothing private', () => {
  const raw = JSON.parse(
    execFileSync('npm', ['pack', '--dry-run', '--json'], { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] })
  );
  // npm <= 11 reports an array; npm 12 reports an object keyed by package name.
  const report = Array.isArray(raw) ? raw[0] : Object.values(raw)[0];
  const files = report.files.map((entry) => entry.path);

  assert.ok(files.includes('bin/applequeue.js'), 'the bin entry point must ship');
  assert.ok(files.includes('src/cli.js'));
  assert.ok(files.includes('README.md'));
  assert.ok(files.includes('LICENSE'));

  for (const path of files) {
    assert.ok(!path.startsWith('test/'), `tests should not ship: ${path}`);
    assert.ok(!path.startsWith('dist/'), `build output should not ship: ${path}`);
    assert.ok(!path.startsWith('scripts/'), `build scripts should not ship: ${path}`);
    assert.ok(!path.includes('config.json'), `local config must never ship: ${path}`);
  }
});
