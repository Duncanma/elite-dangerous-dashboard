import * as signalR from "@microsoft/signalr";
import { apiBaseUrl } from "./api";

let connection: signalR.HubConnection | null = null;
let startPromise: Promise<void> | null = null;

// Initial-start retries (backend not up yet) and restarts after a closed
// connection (automatic reconnect exhausted) share one escalating backoff.
const RETRY_DELAYS_MS = [2000, 5000, 10000, 30000];

function getConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      // withCredentials must be false: the API allows any origin (no auth/cookies
      // on this LAN-only app), and a wildcard CORS origin is rejected by browsers
      // when credentials are included.
      .withUrl(`${apiBaseUrl}/hubs/dashboard`, { withCredentials: false })
      .withAutomaticReconnect()
      .build();
    // onclose fires when automatic reconnect gives up after a drop. Without
    // this the connection would stay dead until a page reload.
    connection.onclose(() => {
      startPromise = null;
      void ensureStarted();
    });
  }
  return connection;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// Never rejects: the connection is a singleton for the app's lifetime, and the
// backend legitimately starts after the frontend (tray restart, phone opening
// the PWA before the host is up). Callers learn about success via the
// resolution and about the interim via connected=false.
async function startWithRetry(conn: signalR.HubConnection): Promise<void> {
  for (let attempt = 0; ; attempt++) {
    try {
      await conn.start();
      return;
    } catch {
      await sleep(RETRY_DELAYS_MS[Math.min(attempt, RETRY_DELAYS_MS.length - 1)]);
    }
  }
}

/** Multiple hooks share one HubConnection - only the first caller actually starts it. */
export function ensureStarted(): Promise<void> {
  const conn = getConnection();
  if (conn.state === signalR.HubConnectionState.Connected) {
    return Promise.resolve();
  }

  if (!startPromise) {
    startPromise = startWithRetry(conn);
  }

  return startPromise;
}

export function getHub(): signalR.HubConnection {
  return getConnection();
}
