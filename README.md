# AppleQueueTerminal

A terminal client for [Apple Queue](https://applequeue.erinskidds.com). It will let a user create Apple Notes, Reminders, Calendar events, and Journal entries from a shell by sending them to their own Apple Queue backend; their existing Apple Shortcut remains responsible for writing into Apple's apps.

## Product boundary

This project is a client, not a replacement backend or a macOS automation layer.

- Authenticate with the existing `APPLE_QUEUE_API_KEY` using `x-api-key`.
- Read enabled modules and defaults from `GET /api/config`.
- Create items through the existing queue endpoints.
- Never store a user's API key or content in the public site repository.
- Do not require direct AppleScript, iCloud, or Apple app access.

## Planned command shape

```text
applequeue configure
applequeue doctor
applequeue note add --title "Trip ideas" --body "..." --folder Travel
applequeue reminder add "Pick up milk" --list Errands --due tomorrow
applequeue event add "Dentist" --start 2026-09-15T09:00 --end 2026-09-15T10:00
applequeue journal add --title "Today" --body "..."
```

See the Obsidian project plan at `/home/ubuntu/Obsidian/my-vault/Projects/AppleQueueTerminal/` for the delivery checklist.

## Release target

The public download and documentation live on `applequeue.erinskidds.com`; this repository should be published independently and linked from that site after the CLI package/release process has been proven.

