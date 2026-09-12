// Produces the artifacts a user installs, so nobody ever clones this repo:
//
//   dist/applequeue.cjs              bundled CLI (input to every binary)
//   dist/applequeue-win-x64.exe      standalone Windows binary -> winget
//   dist/applequeue-<os>-<arch>      standalone binary -> GitHub release
//   dist/SHA256SUMS                  checksums for the winget manifest
//
// Standalone binaries are Node's official Single Executable Application format:
// the bundled script is injected into an unmodified node binary, so the person
// installing needs no Node.js at all. Windows binaries can be built from any
// OS -- postject patches the PE file, it does not execute it.

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { chmodSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const dist = join(root, 'dist');
const cache = join(root, '.node-cache');
const pkg = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

// The Node runtime embedded in the standalone binaries. Pinned deliberately:
// a binary is a promise about what the user runs, so it should not drift.
const NODE_VERSION = process.env.APPLEQUEUE_NODE_VERSION || 'v22.20.0';

const TARGETS = {
  'win-x64': { file: 'applequeue-win-x64.exe', url: (v) => `${dist_url(v)}/win-x64/node.exe`, raw: true },
  'win-arm64': { file: 'applequeue-win-arm64.exe', url: (v) => `${dist_url(v)}/win-arm64/node.exe`, raw: true },
  'linux-x64': { file: 'applequeue-linux-x64', url: (v) => `${dist_url(v)}/node-${v}-linux-x64.tar.gz`, raw: false },
  'linux-arm64': { file: 'applequeue-linux-arm64', url: (v) => `${dist_url(v)}/node-${v}-linux-arm64.tar.gz`, raw: false },
  'darwin-x64': { file: 'applequeue-macos-x64', url: (v) => `${dist_url(v)}/node-${v}-darwin-x64.tar.gz`, raw: false },
  'darwin-arm64': { file: 'applequeue-macos-arm64', url: (v) => `${dist_url(v)}/node-${v}-darwin-arm64.tar.gz`, raw: false },
};

const dist_url = (v) => `https://nodejs.org/dist/${v}`;

async function main() {
  const requested = process.argv.slice(2).filter((a) => !a.startsWith('-'));
  const bundleOnly = process.argv.includes('--bundle-only');
  const targets = requested.length ? requested : ['win-x64'];

  rmSync(dist, { recursive: true, force: true });
  mkdirSync(dist, { recursive: true });
  mkdirSync(cache, { recursive: true });

  const bundle = bundleCli();
  if (bundleOnly) return;

  const blob = seaBlob(bundle);
  const checksums = [];

  for (const name of targets) {
    const target = TARGETS[name];
    if (!target) throw new Error(`unknown target "${name}". Known: ${Object.keys(TARGETS).join(', ')}`);
    const out = join(dist, target.file);
    const runtime = await nodeRuntime(name, target);
    injectBlob(runtime, out, name.startsWith('win'));
    checksums.push(`${sha256(out)}  ${target.file}`);
    log(`built ${target.file}`);
  }

  writeFileSync(join(dist, 'SHA256SUMS'), `${checksums.join('\n')}\n`);
  log(`wrote dist/SHA256SUMS`);
}

// One CommonJS file: SEA cannot load ESM or read files next to itself.
function bundleCli() {
  const out = join(dist, 'applequeue.cjs');
  execFileSync(
    process.execPath,
    [
      join(root, 'node_modules', 'esbuild', 'bin', 'esbuild'),
      join(root, 'bin', 'applequeue.js'),
      '--bundle',
      '--platform=node',
      '--format=cjs',
      '--target=node18',
      `--outfile=${out}`,
    ],
    { stdio: 'inherit', cwd: root }
  );
  log(`bundled dist/applequeue.cjs`);
  return out;
}

function seaBlob(bundle) {
  const configPath = join(dist, 'sea-config.json');
  const blobPath = join(dist, 'applequeue.blob');
  writeFileSync(
    configPath,
    JSON.stringify({ main: bundle, output: blobPath, disableExperimentalSEAWarning: true }, null, 2)
  );
  execFileSync(process.execPath, ['--experimental-sea-config', configPath], { stdio: 'inherit', cwd: root });
  return blobPath;
}

// Fetch (and cache) the official Node binary for a target, returning a path to
// the executable itself.
async function nodeRuntime(name, target) {
  const url = target.url(NODE_VERSION);
  const cached = join(cache, `${NODE_VERSION}-${name}${target.raw ? '.exe' : '.tar.gz'}`);

  if (!exists(cached)) {
    log(`downloading ${url}`);
    const response = await fetch(url);
    if (!response.ok) throw new Error(`could not download Node ${NODE_VERSION} for ${name}: HTTP ${response.status}`);
    writeFileSync(cached, Buffer.from(await response.arrayBuffer()));
  }

  if (target.raw) return cached;

  const extracted = join(cache, `${NODE_VERSION}-${name}-node`);
  if (!exists(extracted)) {
    const dir = join(cache, `${NODE_VERSION}-${name}-unpack`);
    rmSync(dir, { recursive: true, force: true });
    mkdirSync(dir, { recursive: true });
    // GNU tar needs --wildcards for the glob; BSD tar (macOS) rejects the flag.
    const wildcards = process.platform === 'darwin' ? [] : ['--wildcards'];
    execFileSync('tar', ['-xzf', cached, '-C', dir, '--strip-components=2', ...wildcards, '*/bin/node'], {
      stdio: 'inherit',
    });
    writeFileSync(extracted, readFileSync(join(dir, 'node')));
    rmSync(dir, { recursive: true, force: true });
  }
  return extracted;
}

function injectBlob(runtime, out, isWindows) {
  writeFileSync(out, readFileSync(runtime));
  chmodSync(out, 0o755);

  const args = [
    join(root, 'node_modules', 'postject', 'dist', 'cli.js'),
    out,
    'NODE_SEA_BLOB',
    join(dist, 'applequeue.blob'),
    '--sentinel-fuse',
    'NODE_SEA_FUSE_fce680ab2cc467b6e072b8b5df1996b2',
  ];
  // Mach-O binaries keep the payload in a segment; PE and ELF do not.
  if (!isWindows && process.platform === 'darwin') args.push('--macho-segment-name', 'NODE_SEA');
  execFileSync(process.execPath, args, { stdio: 'inherit', cwd: root });
}

const sha256 = (file) => createHash('sha256').update(readFileSync(file)).digest('hex').toUpperCase();

function exists(path) {
  try {
    readFileSync(path);
    return true;
  } catch {
    return false;
  }
}

const log = (message) => process.stderr.write(`build: ${message}\n`);

await main();
