import { CliError, EXIT } from './exit.js';
import {
  configure,
  doctor,
  eventAdd,
  journalAdd,
  noteAdd,
  queueList,
  queueRemove,
  reminderAdd,
} from './commands.js';

export const VERSION = '1.0.0';

const HELP = `applequeue ${VERSION} - queue Apple Journal, Notes, Reminders, and Calendar items from the terminal.

Usage
  applequeue configure                      Save the backend URL and API key
  applequeue doctor [--json]                Check the connection and show enabled modules
  applequeue note add <title> [options]     Queue an Apple Note
  applequeue reminder add <title> [options] Queue a Reminder
  applequeue event add <title> --start ...  Queue a Calendar event
  applequeue journal add <title> [options]  Queue an Apple Journal entry
  applequeue list <journal|notes|reminders|events>
                                            Show items still waiting for your Shortcut
  applequeue remove <kind> <id...>          Discard queued items without creating them

note add
  --title <text>      Title (or pass it as positional words)
  --body <text>       Note body
  --stdin             Read the body from piped stdin
  --folder <name>     Notes folder (defaults to the backend's setting)

reminder add
  --title <text>      Title (or pass it as positional words)
  --notes <text>      Notes body       --stdin  Read notes from stdin
  --list <name>       Reminders list   --url <url>
  --due <when>        "tomorrow 9am", "+2h", or 2026-09-15T09:00
  --priority <p>      none | low | medium | high

journal add
  --title <text>      Title (or pass it as positional words)
  --body <text>       Entry body        --stdin  Read the body from stdin
  --date <when>       Day to file under (default today); "yesterday" works

event add
  --title <text>      Title (or pass it as positional words)
  --start <when>      Required. Same formats as --due
  --end <when>        End time, or use --duration 90m (default 1h)
  --calendar <name>   --location <text>   --notes <text>   --url <url>
  --all-day           --invitee <a,b>     --alert <30m>   (repeatable)

Global
  --json              Machine-readable output on stdout only
  --help, --version

Configuration
  Values are read from the environment first, then the saved config file:
    APPLE_QUEUE_URL, APPLE_QUEUE_API_KEY, APPLE_QUEUE_TIMEOUT_MS

Exit codes
  0 ok   1 usage   2 unauthorized   3 module disabled   4 network   5 backend   6 not configured

Queued items are written into Apple's apps by your existing Apple Shortcut.
`;

export async function run(argv) {
  try {
    return await dispatch(argv);
  } catch (err) {
    if (err instanceof CliError) {
      process.stderr.write(`applequeue: ${err.message}\n`);
      if (err.code === EXIT.CONFIG) process.stderr.write('Run `applequeue configure` to get started.\n');
      if (err.code === EXIT.AUTH) process.stderr.write('Re-run `applequeue configure` with a current API key.\n');
      return err.code;
    }
    throw err;
  }
}

async function dispatch(argv) {
  const [command, ...rest] = argv;

  if (!command || command === '--help' || command === '-h' || command === 'help') {
    process.stdout.write(HELP);
    return EXIT.OK;
  }
  if (command === '--version' || command === '-v' || command === 'version') {
    process.stdout.write(`${VERSION}\n`);
    return EXIT.OK;
  }

  switch (command) {
    case 'configure':
      return configure(rest);
    case 'doctor':
      return doctor(rest);
    case 'note':
      return group('note', rest, { add: noteAdd });
    case 'reminder':
      return group('reminder', rest, { add: reminderAdd });
    case 'event':
      return group('event', rest, { add: eventAdd });
    case 'journal':
      return group('journal', rest, { add: journalAdd });
    case 'list':
      return queueList(rest);
    case 'remove':
      return queueRemove(rest);
    default:
      throw new CliError(`unknown command "${command}". Run \`applequeue --help\`.`, EXIT.USAGE);
  }
}

function group(name, argv, subcommands) {
  const [sub, ...rest] = argv;
  const handler = subcommands[sub];
  if (!handler) {
    throw new CliError(
      `usage: applequeue ${name} ${Object.keys(subcommands).join('|')} ...`,
      EXIT.USAGE
    );
  }
  return handler(rest);
}
