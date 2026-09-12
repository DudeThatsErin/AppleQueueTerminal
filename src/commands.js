// Every user-facing command. Each returns an exit code; --json output goes to
// stdout alone, progress and hints go to stderr, so piping stays clean.

import { parseArgs, readStdin } from './args.js';
import { Client } from './client.js';
import { loadSettings, normalizeUrl, requireSettings, writeConfigFile } from './config.js';
import { addMinutes, formatLocal, parseDate } from './dates.js';
import { CliError, EXIT } from './exit.js';
import { ask } from './prompt.js';

const out = (text) => process.stdout.write(`${text}\n`);
const note = (text) => process.stderr.write(`${text}\n`);
const emitJson = (value) => process.stdout.write(`${JSON.stringify(value, null, 2)}\n`);

function clientFor(env = process.env) {
  const settings = requireSettings(env);
  const timeout = Number(env.APPLE_QUEUE_TIMEOUT_MS || 15000);
  return { client: new Client({ ...settings, timeoutMs: timeout }), settings };
}

// Fetch modules/defaults up front so a disabled module fails before we send
// content anywhere, and so omitted destinations report the backend's default.
async function fetchConfig(client) {
  const config = await client.get('/config');
  return {
    modules: config.modules || {},
    defaults: config.defaults || {},
    features: config.features || {},
  };
}

function requireModule(config, module, label) {
  if (config.modules[module] === false) {
    throw new CliError(`${label} is not enabled on this deployment`, EXIT.DISABLED);
  }
}

// ---------------------------------------------------------------- configure

export async function configure(argv) {
  const { flags } = parseArgs(argv, { url: 'string', 'api-key': 'string', 'no-verify': 'boolean' }, 'configure');

  const url = normalizeUrl(flags.url || (await ask('Apple Queue backend URL: ')));
  const apiKey = flags['api-key'] || (await ask('API key (input hidden): ', { secret: true }));
  if (!apiKey) throw new CliError('an API key is required', EXIT.USAGE);

  if (!flags['no-verify']) {
    note('Checking the backend...');
    const client = new Client({ url, apiKey });
    const config = await fetchConfig(client); // throws with a mapped exit code
    note(`Connected. Enabled modules: ${enabledList(config.modules)}`);
  }

  const file = writeConfigFile({ url, apiKey });
  out(`Saved configuration to ${file}`);
  note('The API key is stored in that file with owner-only permissions and is never printed.');
  return EXIT.OK;
}

function enabledList(modules) {
  const on = Object.keys(modules).filter((k) => modules[k]);
  return on.length ? on.join(', ') : 'none';
}

// ------------------------------------------------------------------- doctor

export async function doctor(argv) {
  const { flags } = parseArgs(argv, { json: 'boolean' }, 'doctor');
  const settings = loadSettings();

  if (!settings.url || !settings.apiKey) {
    const problem = {
      ok: false,
      configured: false,
      error: 'no backend URL or API key configured',
      configFile: settings.file,
    };
    if (flags.json) emitJson(problem);
    else {
      out(`Backend URL: ${settings.url || '(unset)'}`);
      out(`API key:     ${settings.apiKey ? 'set' : '(unset)'}`);
      note('Run `applequeue configure` to set them.');
    }
    return EXIT.CONFIG;
  }

  const { client } = clientFor();
  const health = await client.get('/health');
  const config = await fetchConfig(client);

  if (flags.json) {
    emitJson({ ok: true, url: settings.url, sources: settings.source, health, config });
    return EXIT.OK;
  }

  out(`Backend URL:  ${settings.url}  (from ${settings.source.url})`);
  out(`API key:      set (from ${settings.source.apiKey})`);
  out(`Service:      ${health.service || 'unknown'} @ ${health.time || 'unknown'}`);
  if (health.storage) out(`Storage:      queues=${health.storage.queues}, attachments=${health.storage.attachments}`);
  out(`Modules:      ${enabledList(config.modules)}`);
  out(`Defaults:     notes folder=${config.defaults.notesFolder || '-'}, reminder list=${config.defaults.reminderList || '-'}, calendar=${config.defaults.calendar || '-'}`);
  note('Queued items are delivered to Apple apps by your configured Apple Shortcut.');
  return EXIT.OK;
}

// -------------------------------------------------------------------- notes

export async function noteAdd(argv) {
  const { flags, positionals } = parseArgs(
    argv,
    { title: 'string', body: 'string', folder: 'string', stdin: 'boolean', json: 'boolean' },
    'note add'
  );

  const title = flags.title || positionals.join(' ').trim();
  if (!title) throw new CliError('a note title is required (positional or --title)', EXIT.USAGE);

  let body = flags.body || '';
  if (flags.stdin) body = await readStdin();

  const { client } = clientFor();
  const config = await fetchConfig(client);
  requireModule(config, 'notes', 'Notes');

  const payload = { title, body };
  if (flags.folder) payload.folder = flags.folder;

  const response = await client.post('/apple-notes', payload);
  return report(flags.json, response, () => {
    const item = response.note || {};
    out(`Queued note "${item.title}" to folder "${item.folder}"`);
    out(`id: ${item.id}`);
  });
}

// ------------------------------------------------------------------ journal

// Journal takes a plain calendar date (YYYY-MM-DD) rather than a timestamp:
// the Shortcut files the entry under that day.
export async function journalAdd(argv) {
  const { flags, positionals } = parseArgs(
    argv,
    { title: 'string', body: 'string', date: 'string', stdin: 'boolean', json: 'boolean' },
    'journal add'
  );

  const title = flags.title || positionals.join(' ').trim();
  if (!title) throw new CliError('a journal title is required (positional or --title)', EXIT.USAGE);

  let body = flags.body || '';
  if (flags.stdin) body = await readStdin();

  const date = flags.date ? localDate(parseDate(flags.date, '--date')) : localDate(new Date());

  const { client } = clientFor();
  const config = await fetchConfig(client);
  requireModule(config, 'journal', 'Journal');

  const response = await client.post('/apple-journal', { title, body, date });
  return report(flags.json, response, () => {
    // The deployed backend echoes the item; be tolerant of its key name.
    const item = response.entry || response.journal || {};
    out(`Queued journal entry "${item.title || title}" for ${item.date || date}`);
    if (item.id) out(`id: ${item.id}`);
  });
}

function localDate(value) {
  const d = value instanceof Date ? value : new Date(value);
  const pad = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

// ---------------------------------------------------------------- reminders

const PRIORITIES = new Set(['none', 'low', 'medium', 'high']);

export async function reminderAdd(argv) {
  const { flags, positionals } = parseArgs(
    argv,
    {
      title: 'string',
      notes: 'string',
      list: 'string',
      url: 'string',
      due: 'string',
      priority: 'string',
      stdin: 'boolean',
      json: 'boolean',
    },
    'reminder add'
  );

  const title = flags.title || positionals.join(' ').trim();
  if (!title) throw new CliError('a reminder title is required (positional or --title)', EXIT.USAGE);

  let notes = flags.notes || '';
  if (flags.stdin) notes = await readStdin();

  const payload = { title, notes };
  if (flags.list) payload.list = flags.list;
  if (flags.url) payload.url = flags.url;
  if (flags.due) payload.dueDate = parseDate(flags.due, '--due');
  if (flags.priority) {
    const priority = String(flags.priority).toLowerCase();
    if (!PRIORITIES.has(priority)) {
      throw new CliError(`--priority must be one of ${[...PRIORITIES].join(', ')}`, EXIT.USAGE);
    }
    payload.priority = priority;
  }

  const { client } = clientFor();
  const config = await fetchConfig(client);
  requireModule(config, 'reminders', 'Reminders');

  const response = await client.post('/reminders', payload);
  return report(flags.json, response, () => {
    const item = response.reminder || {};
    out(`Queued reminder "${item.title}" to list "${item.list}"`);
    if (item.dueDate) out(`due: ${formatLocal(item.dueDate)}`);
    out(`id: ${item.id}`);
  });
}

// ----------------------------------------------------------------- calendar

export async function eventAdd(argv) {
  const { flags, positionals } = parseArgs(
    argv,
    {
      title: 'string',
      start: 'string',
      end: 'string',
      duration: 'string',
      calendar: 'string',
      notes: 'string',
      location: 'string',
      url: 'string',
      invitee: 'list',
      alert: 'list',
      'all-day': 'boolean',
      json: 'boolean',
    },
    'event add'
  );

  const title = flags.title || positionals.join(' ').trim();
  if (!title) throw new CliError('an event title is required (positional or --title)', EXIT.USAGE);
  if (!flags.start) throw new CliError('--start is required', EXIT.USAGE);
  if (flags.end && flags.duration) throw new CliError('pass either --end or --duration, not both', EXIT.USAGE);

  const startDate = parseDate(flags.start, '--start');
  let endDate;
  if (flags.end) {
    endDate = parseDate(flags.end, '--end');
  } else {
    const minutes = flags.duration ? durationMinutes(flags.duration) : 60;
    endDate = addMinutes(startDate, minutes);
  }
  if (new Date(endDate) < new Date(startDate)) {
    throw new CliError('--end must not be before --start', EXIT.USAGE);
  }

  const alerts = (flags.alert || []).map((raw) => {
    const minutes = durationMinutes(raw);
    if (minutes < 0 || minutes > 40320) throw new CliError(`--alert ${raw} must be between 0 and 4 weeks`, EXIT.USAGE);
    return minutes;
  });

  const payload = { title, startDate, endDate, allDay: flags['all-day'] === true };
  if (flags.calendar) payload.calendar = flags.calendar;
  if (flags.notes) payload.notes = flags.notes;
  if (flags.location) payload.location = flags.location;
  if (flags.url) payload.url = flags.url;
  if (flags.invitee) payload.invitees = flags.invitee;
  if (alerts.length) payload.alerts = alerts;

  const { client } = clientFor();
  const config = await fetchConfig(client);
  requireModule(config, 'calendar', 'Calendar');

  const response = await client.post('/calendar', payload);
  return report(flags.json, response, () => {
    const item = response.event || {};
    out(`Queued event "${item.title}" to calendar "${item.calendar}"`);
    out(`${formatLocal(item.startDate)} -> ${formatLocal(item.endDate)}`);
    out(`id: ${item.id}`);
    // The backend turns invitees into a Reminders nudge because Shortcuts
    // cannot send invitations; surface that instead of letting it be a surprise.
    if (response.inviteReminder) note(`Also queued a reminder to invite: ${item.invitees.join(', ')}`);
    if (response.warning) note(`Warning: ${response.warning}`);
  });
}

function durationMinutes(raw) {
  const match = /^(\d+)\s*(m|min|mins|minutes?|h|hr|hrs|hours?|d|days?)?$/i.exec(String(raw).trim());
  if (!match) throw new CliError(`"${raw}" is not a duration. Use 30m, 2h, or 1d.`, EXIT.USAGE);
  const n = Number(match[1]);
  const unit = (match[2] || 'm').toLowerCase();
  if (unit.startsWith('h')) return n * 60;
  if (unit.startsWith('d')) return n * 1440;
  return n;
}

// -------------------------------------------------------------- queue views

const QUEUES = {
  journal: { path: '/apple-journal', plural: 'entries', module: 'journal', label: 'Journal' },
  notes: { path: '/apple-notes', plural: 'notes', module: 'notes', label: 'Notes' },
  reminders: { path: '/reminders', plural: 'reminders', module: 'reminders', label: 'Reminders' },
  events: { path: '/calendar', plural: 'events', module: 'calendar', label: 'Calendar' },
};

export async function queueList(argv) {
  const { flags, positionals } = parseArgs(argv, { json: 'boolean' }, 'list');
  const kind = positionals[0];
  const spec = QUEUES[kind];
  if (!spec) throw new CliError(`usage: applequeue list <${Object.keys(QUEUES).join('|')}>`, EXIT.USAGE);

  const { client } = clientFor();
  const response = await client.get(spec.path);
  const items = response[spec.plural] || [];

  if (flags.json) {
    emitJson({ ok: true, [spec.plural]: items });
    return EXIT.OK;
  }
  if (!items.length) {
    out(`No ${kind} waiting. (Items disappear once your Shortcut collects them.)`);
    return EXIT.OK;
  }
  for (const item of items) {
    const when = item.startDate || item.dueDate;
    const label = when ? formatLocal(when) : item.date || '';
    out(`${item.id}  ${item.title}${label ? `  [${label}]` : ''}`);
  }
  return EXIT.OK;
}

export async function queueRemove(argv) {
  const { flags, positionals } = parseArgs(argv, { json: 'boolean' }, 'remove');
  const [kind, ...ids] = positionals;
  const spec = QUEUES[kind];
  if (!spec) throw new CliError(`usage: applequeue remove <${Object.keys(QUEUES).join('|')}> <id...>`, EXIT.USAGE);
  if (!ids.length) throw new CliError('at least one item id is required', EXIT.USAGE);

  const { client } = clientFor();
  const response = await client.delete(spec.path, { ids });
  return report(flags.json, response, () => out(`Removed ${response.removed} item(s) from the ${kind} queue.`));
}

// ------------------------------------------------------------------ helpers

function report(asJson, response, print) {
  if (asJson) emitJson(response);
  else print();
  return EXIT.OK;
}
