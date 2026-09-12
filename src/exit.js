// Stable exit codes so shell automation can branch on failures.
export const EXIT = {
  OK: 0,
  USAGE: 1, // bad flags, missing required input, local validation failure
  AUTH: 2, // 401 from the backend
  DISABLED: 3, // module turned off on the deployment (503)
  NETWORK: 4, // DNS/TLS/timeout/connection refused
  BACKEND: 5, // 4xx/5xx the CLI cannot classify further
  CONFIG: 6, // no backend URL or API key configured
};

export class CliError extends Error {
  constructor(message, code = EXIT.USAGE, details = undefined) {
    super(message);
    this.code = code;
    this.details = details;
  }
}
