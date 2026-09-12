// Runs the real binary against the fake backend, checking stdout, stderr, and
// exit codes the way a shell script would.

import test from 'node:test';
import assert from 'node:assert/strict';
import { execFile, spawn } from 'node:child_process';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { createFakeBackend } from './fake-backend.js';

const BIN = fileURLToPath(new URL('../bin/applequeue.js', import.meta.url));

function startBackend() {
  const server = createFakeBackend();
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => resolve({ server, port: server.address().port }));
  });
}

function runCli(args, env) {
  return new Promise((resolve) => {
    execFile(process.execPath, [BIN, ...args], { env: { ...process.env, ...env } }, (err, stdout, stderr) => {
      resolve({ code: err ? err.code : 0, stdout, stderr });
    });
  });
}

test('the CLI queues items and reports them through the real binary', async (t) => {
  const { server, port } = await startBackend();
  t.after(() => server.close());

  const env = {
    APPLE_QUEUE_URL: `http://127.0.0.1:${port}`,
    APPLE_QUEUE_API_KEY: 'test-key',
    APPLE_QUEUE_CONFIG_DIR: mkdtempSync(join(tmpdir(), 'aq-')),
  };

  const health = await runCli(['doctor'], env);
  assert.equal(health.code, 0);
  assert.match(health.stdout, /Modules: *journal, notes, reminders, calendar/);

  const note = await runCli(['note', 'add', 'Trip ideas', '--body', 'Kyoto', '--json'], env);
  assert.equal(note.code, 0);
  const notePayload = JSON.parse(note.stdout);
  assert.equal(notePayload.note.title, 'Trip ideas');
  assert.equal(notePayload.note.folder, 'Notes');

  const reminder = await runCli(['reminder', 'add', 'Pick up milk', '--due', 'tomorrow 9am', '--json'], env);
  assert.equal(reminder.code, 0);
  assert.ok(JSON.parse(reminder.stdout).reminder.dueDate);

  const event = await runCli(
    ['event', 'add', 'Dentist', '--start', '2026-09-15T09:00', '--duration', '90m', '--json'],
    env
  );
  assert.equal(event.code, 0);
  const created = JSON.parse(event.stdout).event;
  assert.equal(new Date(created.endDate) - new Date(created.startDate), 90 * 60 * 1000);

  const journal = await runCli(['journal', 'add', 'Today', '--body', 'Long walk', '--date', 'yesterday', '--json'], env);
  assert.equal(journal.code, 0);
  const entry = JSON.parse(journal.stdout).entry;
  assert.equal(entry.title, 'Today');
  assert.match(entry.date, /^\d{4}-\d{2}-\d{2}$/);
  const yesterday = new Date();
  yesterday.setDate(yesterday.getDate() - 1);
  assert.equal(entry.date.slice(8), String(yesterday.getDate()).padStart(2, '0'));

  const entries = await runCli(['list', 'journal'], env);
  assert.match(entries.stdout, /Today/);

  const list = await runCli(['list', 'notes'], env);
  assert.match(list.stdout, /Trip ideas/);

  const removed = await runCli(['remove', 'notes', notePayload.note.id], env);
  assert.equal(removed.code, 0);
  assert.match(removed.stdout, /Removed 1 item/);

  const empty = await runCli(['list', 'notes'], env);
  assert.match(empty.stdout, /No notes waiting/);
});

test('a bad key exits 2 and a missing config exits 6', async (t) => {
  const { server, port } = await startBackend();
  t.after(() => server.close());
  const configDir = mkdtempSync(join(tmpdir(), 'aq-'));

  const badKey = await runCli(['doctor'], {
    APPLE_QUEUE_URL: `http://127.0.0.1:${port}`,
    APPLE_QUEUE_API_KEY: 'wrong-key',
    APPLE_QUEUE_CONFIG_DIR: configDir,
  });
  assert.equal(badKey.code, 2);
  assert.match(badKey.stderr, /unauthorized/);
  assert.ok(!badKey.stderr.includes('wrong-key'));

  const unconfigured = await runCli(['doctor'], {
    APPLE_QUEUE_URL: '',
    APPLE_QUEUE_API_KEY: '',
    APPLE_QUEUE_CONFIG_DIR: configDir,
  });
  assert.equal(unconfigured.code, 6);
});

test('a module switched off on the deployment exits 3', async (t) => {
  // ENABLE_* is read when the fake backend module loads, so this variant runs
  // in its own process.
  const port = 18787;
  const child = spawn(process.execPath, [fileURLToPath(new URL('./fake-backend.js', import.meta.url))], {
    env: { ...process.env, ENABLE_JOURNAL: 'false', APPLE_QUEUE_PORT: String(port) },
    stdio: 'ignore',
  });
  t.after(() => child.kill());
  await new Promise((resolve) => setTimeout(resolve, 400));

  const result = await runCli(['journal', 'add', 'Today'], {
    APPLE_QUEUE_URL: `http://127.0.0.1:${port}`,
    APPLE_QUEUE_API_KEY: 'test-key',
    APPLE_QUEUE_CONFIG_DIR: mkdtempSync(join(tmpdir(), 'aq-')),
  });
  assert.equal(result.code, 3);
  assert.match(result.stderr, /Journal is not enabled/);
});

test('local validation fails before any network call', async () => {
  const missingStart = await runCli(['event', 'add', 'Dentist'], {
    APPLE_QUEUE_URL: 'https://unreachable.invalid',
    APPLE_QUEUE_API_KEY: 'k',
    APPLE_QUEUE_CONFIG_DIR: mkdtempSync(join(tmpdir(), 'aq-')),
  });
  assert.equal(missingStart.code, 1);
  assert.match(missingStart.stderr, /--start is required/);
});
