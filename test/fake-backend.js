// A stand-in for the Apple Queue backend: same routes, same auth header, same
// response shapes, so the CLI can be exercised end-to-end with no deployment.
//
//   node test/fake-backend.js            -> listens on 127.0.0.1:8787
//   APPLE_QUEUE_PORT=9000 node test/...  -> different port
//
// The API key is "test-key" unless APPLE_QUEUE_API_KEY is set.

import { createServer } from 'node:http';
import { randomUUID } from 'node:crypto';

const API_KEY = process.env.APPLE_QUEUE_API_KEY || 'test-key';
const MODULES = {
  journal: process.env.ENABLE_JOURNAL !== 'false',
  notes: process.env.ENABLE_NOTES !== 'false',
  reminders: process.env.ENABLE_REMINDERS !== 'false',
  calendar: process.env.ENABLE_CALENDAR !== 'false',
};
const DEFAULTS = { notesFolder: 'Notes', reminderList: 'Reminders', calendar: 'Calendar' };

const queues = { journal: [], notes: [], reminders: [], calendar: [] };

const KINDS = {
  '/api/apple-journal': { kind: 'journal', single: 'entry', plural: 'entries', label: 'Journal' },
  '/api/apple-notes': { kind: 'notes', single: 'note', plural: 'notes', label: 'Notes' },
  '/api/reminders': { kind: 'reminders', single: 'reminder', plural: 'reminders', label: 'Reminders' },
  '/api/calendar': { kind: 'calendar', single: 'event', plural: 'events', label: 'Calendar' },
};

function send(res, status, body) {
  const text = JSON.stringify(body);
  res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' });
  res.end(text);
}

async function body(req) {
  const chunks = [];
  for await (const chunk of req) chunks.push(chunk);
  if (!chunks.length) return null;
  try {
    const parsed = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

export function createFakeBackend() {
  return createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');

    if (req.headers['x-api-key'] !== API_KEY && url.searchParams.get('key') !== API_KEY) {
      return send(res, 401, { ok: false, error: 'unauthorized' });
    }

    if (url.pathname === '/api/health') {
      return send(res, 200, {
        ok: true,
        service: 'apple-queue',
        time: new Date().toISOString(),
        storage: { queues: 'memory', attachments: 'filesystem' },
      });
    }

    if (url.pathname === '/api/config') {
      return send(res, 200, {
        ok: true,
        version: 1,
        modules: MODULES,
        defaults: {
          notesFolder: MODULES.notes ? DEFAULTS.notesFolder : null,
          reminderList: MODULES.reminders ? DEFAULTS.reminderList : null,
          calendar: MODULES.calendar ? DEFAULTS.calendar : null,
        },
        features: { ai: false, places: false, attachments: MODULES.notes },
      });
    }

    const spec = KINDS[url.pathname];
    if (!spec) return send(res, 404, { ok: false, error: 'not found' });
    if (!MODULES[spec.kind]) {
      return send(res, 503, { ok: false, error: `${spec.label} is not enabled on this deployment` });
    }

    if (req.method === 'GET') {
      return send(res, 200, { ok: true, [spec.plural]: queues[spec.kind] });
    }

    if (req.method === 'POST') {
      const payload = await body(req);
      if (!payload) return send(res, 400, { ok: false, error: 'expected a JSON object body' });
      if (!payload.title) return send(res, 400, { ok: false, error: 'title is required' });
      if (spec.kind === 'calendar' && !payload.startDate) {
        return send(res, 400, { ok: false, error: 'startDate is required and must be a valid date' });
      }

      const item = {
        id: randomUUID(),
        createdAt: new Date().toISOString(),
        date: spec.kind === 'journal' ? new Date().toISOString().slice(0, 10) : undefined,
        folder: spec.kind === 'notes' ? DEFAULTS.notesFolder : undefined,
        list: spec.kind === 'reminders' ? DEFAULTS.reminderList : undefined,
        calendar: spec.kind === 'calendar' ? DEFAULTS.calendar : undefined,
        invitees: spec.kind === 'calendar' ? [] : undefined,
        ...payload,
      };
      queues[spec.kind].push(item);

      const response = { ok: true, [spec.single]: item };
      if (spec.kind === 'calendar' && item.invitees?.length && MODULES.reminders) {
        const nudge = {
          id: randomUUID(),
          title: `Invite ${item.invitees.join(', ')} to "${item.title}"`,
          list: 'Inbox',
          priority: 'medium',
          createdAt: new Date().toISOString(),
        };
        queues.reminders.push(nudge);
        response.inviteReminder = nudge;
      }
      return send(res, 200, response);
    }

    if (req.method === 'DELETE') {
      const payload = await body(req);
      const ids = Array.isArray(payload?.ids) ? payload.ids : null;
      if (!ids?.length) return send(res, 400, { ok: false, error: 'expected { "ids": ["..."] }' });
      const before = queues[spec.kind].length;
      queues[spec.kind] = queues[spec.kind].filter((item) => !ids.includes(item.id));
      return send(res, 200, { ok: true, removed: before - queues[spec.kind].length });
    }

    return send(res, 404, { ok: false, error: 'not found' });
  });
}

// Only listen when run directly, so tests can import and control the server.
if (process.argv[1] && process.argv[1].endsWith('fake-backend.js')) {
  const port = Number(process.env.APPLE_QUEUE_PORT || 8787);
  createFakeBackend().listen(port, '127.0.0.1', () => {
    process.stdout.write(`fake Apple Queue backend on http://127.0.0.1:${port} (api key: ${API_KEY})\n`);
  });
}
