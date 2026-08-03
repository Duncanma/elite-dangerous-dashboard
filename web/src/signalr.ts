import * as signalR from "@microsoft/signalr";
import { apiBaseUrl } from "./api";

let connection: signalR.HubConnection | null = null;
let startPromise: Promise<void> | null = null;

function getConnection(): signalR.HubConnection {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      // withCredentials must be false: the API allows any origin (no auth/cookies
      // on this LAN-only app), and a wildcard CORS origin is rejected by browsers
      // when credentials are included.
      .withUrl(`${apiBaseUrl}/hubs/dashboard`, { withCredentials: false })
      .withAutomaticReconnect()
      .build();
  }
  return connection;
}

/** Multiple hooks share one HubConnection - only the first caller actually starts it. */
export function ensureStarted(): Promise<void> {
  const conn = getConnection();
  if (conn.state === signalR.HubConnectionState.Connected) {
    return Promise.resolve();
  }

  if (!startPromise) {
    startPromise = conn.start().catch((err) => {
      startPromise = null;
      throw err;
    });
  }

  return startPromise;
}

export function getHub(): signalR.HubConnection {
  return getConnection();
}
