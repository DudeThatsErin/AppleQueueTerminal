import test from 'node:test';
import assert from 'node:assert/strict';

import { parseArgs } from '../src/args.js';
import { normalizeUrl } from '../src/config.js';
import { parseDate, addMinutes } from '../src/dates.js';
import { Client } from '../src/client.js';
import { EXIT } from '../src/exit.js';

test('normalizeUrl trims slashes, adds https, and drops a pasted /api suffix', () => {
  assert.equal(normalizeUrl('applequeue.example.com'), 'https://applequeue.example.com');
  assert.equal(normalizeUrl('https://applequeue.example.com/'), 'https://applequeue.example.com');
  assert.equal(normalizeUrl('https://applequeue.example.com/api'), 'https://applequeue.example.com');
  assert.equal(normalizeUrl('http://127.0.0.1:8787'), 'http://127.0.0.1:8787');
});

test('normalizeUrl rejects plaintext http to a remote host', () => {
  assert.throws(() => normalizeUrl('http://applequeue.example.com'), /https/);
});

test('parseArgs handles values, equals form, repeated lists, and booleans', () => {
  const { flags, positionals } = parseArgs(
    ['Dentist', '--start=tomorrow 9am', '--invitee', 'a@x.com,b@x.com', '--invitee', 'c@x.com', '--all-day'],
    { start: 'string', invitee: 'list', 'all-day': 'boolean' },
    'event add'
  );
  assert.deepEqual(positionals, ['Dentist']);
  assert.equal(flags.start, 'tomorrow 9am');
  assert.deepEqual(flags.invitee, ['a@x.com', 'b@x.com', 'c@x.com']);
  assert.equal(flags['all-day'], true);
});

test('parseArgs names the command in unknown-option errors', () => {
  assert.throws(() => parseArgs(['--nope'], { title: 'string' }, 'note add'), /unknown option --nope/);
});

test('parseDate reads relative, named-day, and ISO inputs', () => {
  const now = new Date('2026-09-12T12:00:00');
  assert.equal(parseDate('+2h', '--due', now), new Date('2026-09-12T14:00:00').toISOString());

  const tomorrow = new Date(parseDate('tomorrow 9am', '--due', now));
  assert.equal(tomorrow.getDate(), 13);
  assert.equal(tomorrow.getHours(), 9);

  // A bare local date-time must stay local, not be read as UTC.
  const local = new Date(parseDate('2026-09-15T09:00', '--start', now));
  assert.equal(local.getHours(), 9);
  assert.equal(local.getDate(), 15);
});

test('parseDate rejects nonsense with an actionable message', () => {
  assert.throws(() => parseDate('next thursdayish', '--due'), /is not a date/);
});

test('addMinutes advances an ISO timestamp', () => {
  assert.equal(addMinutes('2026-09-15T09:00:00.000Z', 90), '2026-09-15T10:30:00.000Z');
});

function fakeFetch(status, payload) {
  return async () => ({
    ok: status >= 200 && status < 300,
    status,
    text: async () => (typeof payload === 'string' ? payload : JSON.stringify(payload)),
  });
}

test('401 maps to the auth exit code and never echoes the key', async () => {
  const client = new Client({
    url: 'https://q.example.com',
    apiKey: 'super-secret',
    fetchImpl: fakeFetch(401, { ok: false, error: 'unauthorized' }),
  });
  await assert.rejects(client.get('/config'), (err) => {
    assert.equal(err.code, EXIT.AUTH);
    assert.ok(!err.message.includes('super-secret'));
    return true;
  });
});

test('a disabled module maps to its own exit code', async () => {
  const client = new Client({
    url: 'https://q.example.com',
    apiKey: 'k',
    fetchImpl: fakeFetch(503, { ok: false, error: 'Notes is not enabled on this deployment' }),
  });
  await assert.rejects(client.post('/apple-notes', {}), (err) => err.code === EXIT.DISABLED);
});

test('a malformed body is reported rather than parsed as success', async () => {
  const client = new Client({ url: 'https://q.example.com', apiKey: 'k', fetchImpl: fakeFetch(200, '<html>') });
  await assert.rejects(client.get('/health'), /non-JSON/);
});

test('a hung backend aborts on the timeout', async () => {
  const client = new Client({
    url: 'https://q.example.com',
    apiKey: 'k',
    timeoutMs: 20,
    fetchImpl: (_url, opts) =>
      new Promise((_resolve, reject) => {
        opts.signal.addEventListener('abort', () => {
          const err = new Error('aborted');
          err.name = 'AbortError';
          reject(err);
        });
      }),
  });
  await assert.rejects(client.get('/health'), (err) => err.code === EXIT.NETWORK);
});

test('the api key travels in x-api-key, not the URL', async () => {
  let seen;
  const client = new Client({
    url: 'https://q.example.com',
    apiKey: 'k',
    fetchImpl: async (url, opts) => {
      seen = { url, opts };
      return { ok: true, status: 200, text: async () => '{"ok":true}' };
    },
  });
  await client.get('/config');
  assert.equal(seen.url, 'https://q.example.com/api/config');
  assert.equal(seen.opts.headers['x-api-key'], 'k');
});
