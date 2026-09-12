// Where the backend URL and API key come from, in precedence order:
//   1. environment (APPLE_QUEUE_URL / APPLE_QUEUE_API_KEY) -- for CI and scripts
//   2. the config file written by `applequeue configure`
// The key is never printed, logged, or included in error output.

import { chmodSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { homedir, platform } from 'node:os';
import { join } from 'node:path';
import { execFileSync } from 'node:child_process';
import { CliError, EXIT } from './exit.js';

export function configDir(env = process.env) {
  if (env.APPLE_QUEUE_CONFIG_DIR) return env.APPLE_QUEUE_CONFIG_DIR;
  if (platform() === 'win32') {
    return join(env.APPDATA || join(homedir(), 'AppData', 'Roaming'), 'applequeue');
  }
  return join(env.XDG_CONFIG_HOME || join(homedir(), '.config'), 'applequeue');
}

export function configPath(env = process.env) {
  return join(configDir(env), 'config.json');
}

export function readConfigFile(env = process.env) {
  try {
    const parsed = JSON.parse(readFileSync(configPath(env), 'utf8'));
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

// Trailing slashes and an accidental /api suffix are the two things people
// paste; normalise both rather than failing later with a confusing 404.
export function normalizeUrl(raw) {
  const value = String(raw || '').trim();
  if (!value) throw new CliError('backend URL is required', EXIT.USAGE);
  const withScheme = /^https?:\/\//i.test(value) ? value : `https://${value}`;
  let url;
  try {
    url = new URL(withScheme);
  } catch {
    throw new CliError(`not a valid URL: ${value}`, EXIT.USAGE);
  }
  if (url.protocol !== 'https:' && url.hostname !== 'localhost' && url.hostname !== '127.0.0.1') {
    throw new CliError('backend URL must use https (http is allowed only for localhost)', EXIT.USAGE);
  }
  let path = url.pathname.replace(/\/+$/, '');
  if (path.endsWith('/api')) path = path.slice(0, -4);
  return `${url.origin}${path}`;
}

export function writeConfigFile({ url, apiKey }, env = process.env) {
  const dir = configDir(env);
  mkdirSync(dir, { recursive: true });
  const file = configPath(env);
  writeFileSync(file, `${JSON.stringify({ url, apiKey }, null, 2)}\n`, { mode: 0o600 });
  restrictPermissions(file);
  return file;
}

// The keychain is the goal; until that ships, at least make the fallback file
// unreadable by other accounts on the machine.
function restrictPermissions(file) {
  if (platform() === 'win32') {
    try {
      execFileSync('icacls', [file, '/inheritance:r', '/grant:r', `${process.env.USERNAME}:F`], {
        stdio: 'ignore',
      });
    } catch {
      // icacls is best-effort; the file still lives in the per-user AppData dir.
    }
    return;
  }
  try {
    chmodSync(file, 0o600);
  } catch {
    /* ignore */
  }
}

export function loadSettings(env = process.env) {
  const file = readConfigFile(env);
  const url = env.APPLE_QUEUE_URL || file.url || '';
  const apiKey = env.APPLE_QUEUE_API_KEY || file.apiKey || '';
  return {
    url: url ? normalizeUrl(url) : '',
    apiKey,
    source: {
      url: env.APPLE_QUEUE_URL ? 'env' : file.url ? 'config file' : 'unset',
      apiKey: env.APPLE_QUEUE_API_KEY ? 'env' : file.apiKey ? 'config file' : 'unset',
    },
    file: configPath(env),
  };
}

export function requireSettings(env = process.env) {
  const settings = loadSettings(env);
  if (!settings.url || !settings.apiKey) {
    throw new CliError(
      'not configured. Run `applequeue configure`, or set APPLE_QUEUE_URL and APPLE_QUEUE_API_KEY.',
      EXIT.CONFIG
    );
  }
  return settings;
}
