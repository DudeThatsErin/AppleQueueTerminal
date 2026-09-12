// Dates are validated locally so a typo costs a round trip to nobody. The
// backend takes anything `new Date()` accepts; this adds the shorthand a person
// actually types in a shell ("tomorrow 9am", "+2h").

import { CliError, EXIT } from './exit.js';

const RELATIVE = /^\+(\d+)\s*(m|min|mins|minutes?|h|hr|hrs|hours?|d|days?|w|weeks?)$/i;
const TIME = /^(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$/i;

const UNIT_MS = {
  m: 60_000,
  h: 3_600_000,
  d: 86_400_000,
  w: 604_800_000,
};

function unitKey(raw) {
  const u = raw.toLowerCase();
  if (u.startsWith('mi') || u === 'm') return 'm';
  if (u.startsWith('h')) return 'h';
  if (u.startsWith('d')) return 'd';
  return 'w';
}

function applyTime(date, timeText) {
  const match = TIME.exec(timeText.trim());
  if (!match) return null;
  let hours = Number(match[1]);
  const minutes = match[2] ? Number(match[2]) : 0;
  const meridiem = match[3] ? match[3].toLowerCase() : '';
  if (minutes > 59) return null;
  if (meridiem) {
    if (hours < 1 || hours > 12) return null;
    if (meridiem === 'pm' && hours !== 12) hours += 12;
    if (meridiem === 'am' && hours === 12) hours = 0;
  } else if (hours > 23) {
    return null;
  }
  const out = new Date(date);
  out.setHours(hours, minutes, 0, 0);
  return out;
}

function startOfDay(now, offsetDays) {
  const d = new Date(now);
  d.setDate(d.getDate() + offsetDays);
  d.setHours(0, 0, 0, 0);
  return d;
}

// Returns an ISO string, or throws a CliError naming the offending flag.
export function parseDate(input, flag, now = new Date()) {
  const value = String(input || '').trim();
  if (!value) throw new CliError(`${flag} is required`, EXIT.USAGE);

  const relative = RELATIVE.exec(value);
  if (relative) {
    return new Date(now.getTime() + Number(relative[1]) * UNIT_MS[unitKey(relative[2])]).toISOString();
  }

  const lower = value.toLowerCase();
  const words = lower.split(/\s+/);
  const dayOffsets = { today: 0, tonight: 0, tomorrow: 1, yesterday: -1 };
  if (words[0] in dayOffsets) {
    let base = startOfDay(now, dayOffsets[words[0]]);
    const rest = words.slice(1).join(' ');
    if (rest) {
      const timed = applyTime(base, rest);
      if (!timed) throw new CliError(`could not read a time from ${flag} "${value}"`, EXIT.USAGE);
      base = timed;
    } else if (words[0] === 'tonight') {
      base.setHours(20, 0, 0, 0);
    } else {
      base.setHours(9, 0, 0, 0);
    }
    return base.toISOString();
  }

  // A bare date with no zone is local time, not UTC -- Date parses
  // "2026-09-15" as UTC, which silently shifts the day for most users.
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [y, m, d] = value.split('-').map(Number);
    return new Date(y, m - 1, d, 9, 0, 0, 0).toISOString();
  }
  const localDateTime = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value);
  if (localDateTime) {
    const [, y, mo, d, h, mi, s] = localDateTime;
    return new Date(+y, +mo - 1, +d, +h, +mi, s ? +s : 0, 0).toISOString();
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    throw new CliError(
      `${flag} "${value}" is not a date. Try an ISO value (2026-09-15T09:00), "tomorrow 9am", or "+2h".`,
      EXIT.USAGE
    );
  }
  return parsed.toISOString();
}

export function addMinutes(iso, minutes) {
  return new Date(new Date(iso).getTime() + minutes * 60_000).toISOString();
}

export function formatLocal(iso) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return String(iso);
  return d.toLocaleString();
}
