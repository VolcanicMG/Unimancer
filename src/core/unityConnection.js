/**
 * TCP client to the Unity Editor bridge.
 *
 * The Unity side (the `unity/` package) hosts a TCP server inside the Editor.
 * This client connects, sends newline-delimited JSON requests correlated by a
 * monotonic id, and resolves the matching response. It connects lazily: any tool
 * that needs Unity calls `request()`, which connects on demand.
 *
 * We use a raw TCP line protocol (not WebSocket) because Unity's Mono runtime
 * can't perform server-side WebSocket upgrades. Both ends are ours, so framing
 * is just one JSON object per line.
 *
 *   request:  { id, method, params }\n
 *   response: { id, result } | { id, error }\n
 */
import net from "node:net";

/** How long (ms) to wait for a Unity response before rejecting. */
const REQUEST_TIMEOUT_MS = 120_000;

/**
 * Resolve the bridge endpoint from `UNITY_MCP_URL` (tcp://host:port or host:port)
 * or fall back to localhost:8090.
 * @returns {{host: string, port: number}}
 */
function parseEndpoint() {
  const raw = process.env.UNITY_MCP_URL;
  if (raw) {
    const m = raw.match(/^(?:tcp:\/\/|ws:\/\/)?([^:/]+):(\d+)/);
    if (m) return { host: m[1], port: Number(m[2]) };
  }
  return { host: "127.0.0.1", port: 8090 };
}

export class UnityConnection {
  /** @param {{host: string, port: number}} [endpoint] */
  constructor(endpoint = parseEndpoint()) {
    /** @private */ this.host = endpoint.host;
    /** @private */ this.port = endpoint.port;
    /** @private @type {net.Socket|null} */ this.socket = null;
    /** @private */ this.nextId = 1;
    /** @private @type {Map<number, {resolve:Function, reject:Function, timer:NodeJS.Timeout}>} */
    this.pending = new Map();
    /** @private @type {Promise<void>|null} */ this.connecting = null;
    /** @private — accumulates partial lines across data chunks. */ this.buffer = "";
    /** @private @type {Array<(evt:object)=>void>} */ this.eventHandlers = [];
  }

  /**
   * Ensure a live connection, connecting on demand.
   * @returns {Promise<void>}
   */
  ensureConnected() {
    if (this.socket && !this.socket.destroyed) return Promise.resolve();
    if (this.connecting) return this.connecting;
    this.connecting = this._connectWithRetry(10, 500).finally(() => { this.connecting = null; });
    return this.connecting;
  }

  /**
   * Connect with bounded retry/backoff so a request that lands during the Editor
   * bridge's restart window (e.g. right after a script recompile, while the bridge
   * re-binds port 8090) waits for it to come back instead of hard-failing the turn.
   * @private
   * @param {number} attempts - max connection attempts.
   * @param {number} delayMs - delay between attempts.
   * @returns {Promise<void>}
   */
  async _connectWithRetry(attempts, delayMs) {
    let lastErr;
    for (let i = 1; i <= attempts; i++) {
      try { return await this._connectOnce(); }
      catch (e) {
        lastErr = e;
        if (i < attempts) await new Promise((r) => setTimeout(r, delayMs));
      }
    }
    throw new Error(
      `Unity connection failed at ${this.host}:${this.port} after ${attempts} tries: ${lastErr && lastErr.message}. ` +
      `Is the Unity Editor open with the Unimancer package, and the bridge listening (Window -> Unimancer -> Setup)?`,
    );
  }

  /**
   * One connection attempt. Resolves when connected; rejects on connect error only
   * (post-connect errors are handled by the "close" cleanup, not this promise).
   * @private
   * @returns {Promise<void>}
   */
  _connectOnce() {
    return new Promise((resolve, reject) => {
      let settled = false;
      const s = net.createConnection({ host: this.host, port: this.port }, () => {
        settled = true;
        try { s.setNoDelay(true); } catch { /* ignore */ }
        resolve();
      });
      s.setEncoding("utf8");
      s.on("data", (chunk) => this.onData(chunk));
      s.on("error", (e) => { if (!settled) { settled = true; reject(e); } });
      s.on("close", () => {
        this.socket = null;
        for (const [, p] of this.pending) {
          clearTimeout(p.timer);
          p.reject(new Error("Unity connection closed"));
        }
        this.pending.clear();
      });
      this.socket = s;
    });
  }

  /**
   * Buffer incoming data and dispatch each complete newline-delimited message.
   * @private
   * @param {string} chunk
   */
  onData(chunk) {
    this.buffer += chunk;
    let idx;
    while ((idx = this.buffer.indexOf("\n")) >= 0) {
      const line = this.buffer.slice(0, idx);
      this.buffer = this.buffer.slice(idx + 1);
      if (line.length > 0) this.handleMessage(line);
    }
  }

  /**
   * Route a response line to its pending request.
   * @private
   * @param {string} raw
   */
  handleMessage(raw) {
    let msg;
    try { msg = JSON.parse(raw); } catch { return; }
    // Unsolicited editor event (no request id): fan out to listeners.
    if (msg && msg.event) {
      for (const fn of this.eventHandlers) { try { fn(msg); } catch { /* ignore */ } }
      return;
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
   * @param {string} method - Editor-side tool name.
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
      this.socket.write(JSON.stringify({ id, method, params }) + "\n");
    });
  }

  /**
   * Register a listener for unsolicited editor events ({ event, data }).
   * @param {(evt: object) => void} fn
   */
  onEvent(fn) {
    this.eventHandlers.push(fn);
  }

  /** Close the connection (used on shutdown). */
  close() {
    if (this.socket) this.socket.destroy();
  }
}
