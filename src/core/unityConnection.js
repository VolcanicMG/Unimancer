/**
 * WebSocket client to the Unity Editor.
 *
 * The Unity side (the `unity/` UPM package) hosts a WebSocket server inside the
 * Editor. This client connects to it, sends JSON requests correlated by a
 * monotonic id, and resolves the matching response. It auto-reconnects lazily:
 * any tool that needs Unity calls `request()`, which connects on demand.
 *
 * Wire protocol (JSON per message):
 *   request:  { id: number, method: string, params: object }
 *   response: { id: number, result?: any, error?: string }
 */
import { WebSocket } from "ws";

/** Default Editor WebSocket endpoint. Override with `UNITY_MCP_URL`. */
const DEFAULT_URL = process.env.UNITY_MCP_URL || "ws://127.0.0.1:8090";
/** How long (ms) to wait for a Unity response before rejecting. */
const REQUEST_TIMEOUT_MS = 120_000;

export class UnityConnection {
  /** @param {string} [url] - Editor WebSocket URL. */
  constructor(url = DEFAULT_URL) {
    /** @private */ this.url = url;
    /** @private @type {WebSocket|null} */ this.ws = null;
    /** @private */ this.nextId = 1;
    /** @private @type {Map<number, {resolve:Function, reject:Function, timer:NodeJS.Timeout}>} */
    this.pending = new Map();
    /** @private @type {Promise<void>|null} */ this.connecting = null;
  }

  /**
   * Ensure a live connection, connecting (or reconnecting) on demand.
   * @returns {Promise<void>}
   */
  ensureConnected() {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) return Promise.resolve();
    if (this.connecting) return this.connecting;

    this.connecting = new Promise((resolve, reject) => {
      const ws = new WebSocket(this.url);
      this.ws = ws;

      ws.on("open", () => {
        this.connecting = null;
        resolve();
      });
      ws.on("message", (data) => this.handleMessage(data.toString()));
      ws.on("error", (e) => {
        this.connecting = null;
        reject(new Error(`Unity connection failed at ${this.url}: ${e.message}. Is the Unity Editor open with the Unimancer package?`));
      });
      ws.on("close", () => {
        this.ws = null;
        // Fail any in-flight requests so callers don't hang.
        for (const [, p] of this.pending) {
          clearTimeout(p.timer);
          p.reject(new Error("Unity connection closed"));
        }
        this.pending.clear();
      });
    });
    return this.connecting;
  }

  /**
   * Route an incoming Editor message to its pending request.
   * @private
   * @param {string} raw
   */
  handleMessage(raw) {
    let msg;
    try {
      msg = JSON.parse(raw);
    } catch {
      return; // ignore malformed frames
    }
    const p = this.pending.get(msg.id);
    if (!p) return;
    this.pending.delete(msg.id);
    clearTimeout(p.timer);
    if (msg.error) p.reject(new Error(String(msg.error)));
    else p.resolve(msg.result);
  }

  /**
   * Send a request to the Unity Editor and await its result.
   * @param {string} method - Editor-side tool/method name.
   * @param {object} [params] - parameters object.
   * @returns {Promise<any>} the `result` field from the Editor response.
   */
  async request(method, params = {}) {
    await this.ensureConnected();
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Unity request "${method}" timed out after ${REQUEST_TIMEOUT_MS}ms`));
      }, REQUEST_TIMEOUT_MS);
      this.pending.set(id, { resolve, reject, timer });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }

  /** Close the connection (used on shutdown). */
  close() {
    if (this.ws) this.ws.close();
  }
}
