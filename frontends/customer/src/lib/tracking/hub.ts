"use client";

import {
  HttpTransportType,
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

import { tokenStore } from "@/lib/auth/token-store";
import { env } from "@/lib/env";
import type { LocationUpdatedPayload, StatusChangedPayload } from "@/lib/api/types";

/**
 * Builds a SignalR connection to /hubs/tracking with JWT in the query string.
 * The hub is mounted under the gateway so all traffic hits one origin.
 */
export function buildTrackingConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${env.apiUrl}/hubs/tracking`, {
      // JWT goes in the query string — backend extracts ?access_token= when
      // path starts with /hubs/tracking. WebSocket handshakes can't carry
      // custom headers.
      accessTokenFactory: () => tokenStore.getAccessToken() ?? "",
      // Let SignalR negotiate (WebSockets first, fallback to SSE/LongPolling).
      transport:
        HttpTransportType.WebSockets |
        HttpTransportType.ServerSentEvents |
        HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build();
}

export type TrackingCallbacks = {
  onLocation: (payload: LocationUpdatedPayload) => void;
  onStatus: (payload: StatusChangedPayload) => void;
  onConnected?: () => void;
  onDisconnected?: (err?: Error) => void;
  onError?: (err: Error) => void;
};

/**
 * Manages the lifecycle of a tracking connection for ONE orderId. Returns a
 * `dispose` function. Resilient to React strict-mode double-mount.
 */
export function startTracking(orderId: string, cb: TrackingCallbacks): () => void {
  const conn = buildTrackingConnection();
  let disposed = false;

  conn.on("LocationUpdated", (p: LocationUpdatedPayload) => cb.onLocation(p));
  conn.on("StatusChanged",   (p: StatusChangedPayload)   => cb.onStatus(p));
  conn.onreconnected(() => cb.onConnected?.());
  conn.onclose((err) => cb.onDisconnected?.(err));

  (async () => {
    try {
      await conn.start();
      cb.onConnected?.();
      if (disposed) {
        await conn.stop();
        return;
      }
      try {
        await conn.invoke("Subscribe", orderId);
      } catch (subErr) {
        cb.onError?.(subErr as Error);
      }
    } catch (err) {
      cb.onError?.(err as Error);
    }
  })();

  return () => {
    disposed = true;
    void (async () => {
      if (conn.state === HubConnectionState.Connected) {
        try {
          await conn.invoke("Unsubscribe", orderId);
        } catch {
          // ignore — we're about to close
        }
      }
      await conn.stop();
    })();
  };
}
