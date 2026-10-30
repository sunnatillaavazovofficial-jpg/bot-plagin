"use client";

import { cn } from "@/lib/utils";

export type ToastState = { ok: boolean; text: string } | null;

export function Toast({ state }: { state: ToastState }) {
  if (!state) return null;
  return (
    <p
      role="status"
      className={cn(
        "rounded-xl px-4 py-2.5 text-sm",
        state.ok ? "border border-emerald-400/30 bg-emerald-400/10 text-emerald-200" : "border border-rose-400/30 bg-rose-400/10 text-rose-200",
      )}
    >
      {state.text}
    </p>
  );
}
