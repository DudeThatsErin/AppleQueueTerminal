// One HTTP client for every command: timeouts, JSON validation, x-api-key, and
// backend errors mapped onto the CLI's exit codes.

import { CliError, EXIT } from './exit.js';

const DEFAULT_TIMEOUT_MS = 15000;

export class Client {
  constructor({ url, apiKey, timeoutMs = DEFAULT_TIMEOUT_MS, fetchImpl = globalThis.fetch }) {
    this.url = url;
    this.apiKey = apiKey;
    this.timeoutMs = timeoutMs;
    this.fetchImpl = fetchImpl;
  }

  async request(method, path, body) {
    const target = `${this.url}/api${path}`;
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);

    let response;
    try {
      response = await this.fetchImpl(target, {
        method,
        headers: {
          'x-api-key': this.apiKey,
          accept: 'application/json',
          ...(body === undefined ? {} : { 'content-type': 'application/json' }),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: controller.signal,
      });
    } catch (err) {
      if (err && err.name === 'AbortError') {
        throw new CliError(`no response from ${this.url} after ${this.timeoutMs}ms`, EXIT.NETWORK);
      }
      // Never interpolate the key; only the URL and the transport reason.
      throw new CliError(`could not reach ${this.url}: ${reason(err)}`, EXIT.NETWORK);
    } finally {
      clearTimeout(timer);
    }

    const text = await response.text();
    let parsed = null;
    try {
      parsed = text ? JSON.parse(text) : null;
    } catch {
      parsed = null;
    }

    if (!response.ok) {
      const message = parsed && typeof parsed.error === 'string' ? parsed.error : `HTTP ${response.status}`;
      if (response.status === 401) {
        throw new CliError(`unauthorized: the backend rejected this API key (${this.url})`, EXIT.AUTH);
      }
      if (response.status === 503 && /not enabled/i.test(message)) {
        throw new CliError(message, EXIT.DISABLED);
      }
      throw new CliError(message, response.status === 400 ? EXIT.USAGE : EXIT.BACKEND);
    }

    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      throw new CliError(`backend returned a non-JSON response (HTTP ${response.status})`, EXIT.BACKEND);
    }
    return parsed;
  }

  get(path) {
    return this.request('GET', path);
  }

  post(path, body) {
    return this.request('POST', path, body);
  }

  delete(path, body) {
    return this.request('DELETE', path, body);
  }
}

function reason(err) {
  const code = err && (err.cause?.code || err.code);
  if (code === 'ENOTFOUND') return 'host not found';
  if (code === 'ECONNREFUSED') return 'connection refused';
  if (code === 'CERT_HAS_EXPIRED') return 'the TLS certificate has expired';
  if (code) return String(code);
  return err && err.message ? err.message : 'unknown error';
}
