import { useEffect, useRef, useSyncExternalStore } from "react";
import type { LiveEvent } from "./types";

type Listener = (e: LiveEvent) => void;

const listeners = new Set<Listener>();
let socket: WebSocket | null = null;
let connected = false;
let stopped = true;
let retry = 0;
let pingTimer: number | undefined;
const stateListeners = new Set<() => void>();

function setConnected(value: boolean) {
  connected = value;
  stateListeners.forEach((fn) => fn());
}

function open() {
  if (stopped) return;
  const proto = location.protocol === "https:" ? "wss" : "ws";
  const ws = new WebSocket(`${proto}://${location.host}/ws`);
  socket = ws;
  ws.onopen = () => {
    retry = 0;
    setConnected(true);
    // Uzilish paytida o'tkazib yuborilgan hodisalar uchun hamma ro'yxatlarni yangilaymiz
    listeners.forEach((fn) => fn({ type: "chats.reload", account_id: 0 }));
    window.clearInterval(pingTimer);
    pingTimer = window.setInterval(() => ws.readyState === 1 && ws.send("ping"), 25000);
  };
  ws.onmessage = (ev) => {
    if (ev.data === "pong") return;
    try {
      const event = JSON.parse(ev.data) as LiveEvent;
      listeners.forEach((fn) => fn(event));
    } catch {
      /* e'tiborsiz */
    }
  };
  ws.onclose = () => {
    window.clearInterval(pingTimer);
    setConnected(false);
    if (socket === ws) socket = null;
    if (!stopped) {
      retry = Math.min(retry + 1, 6);
      window.setTimeout(open, 1000 * retry);
    }
  };
}

export function startLive() {
  if (!stopped) return;
  stopped = false;
  open();
}

export function stopLive() {
  stopped = true;
  socket?.close();
  socket = null;
}

export function useLive(handler: Listener) {
  const ref = useRef(handler);
  ref.current = handler;
  useEffect(() => {
    const fn: Listener = (e) => ref.current(e);
    listeners.add(fn);
    return () => {
      listeners.delete(fn);
    };
  }, []);
}

export function useLiveConnected() {
  return useSyncExternalStore(
    (cb) => {
      stateListeners.add(cb);
      return () => stateListeners.delete(cb);
    },
    () => connected,
  );
}
