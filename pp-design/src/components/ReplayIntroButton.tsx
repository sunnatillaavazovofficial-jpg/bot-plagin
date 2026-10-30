"use client";

import { REPLAY_EVENT } from "@/components/intro/Intro";

export function ReplayIntroButton() {
  return (
    <button
      type="button"
      className="text-sm text-mist/70 transition hover:text-white"
      onClick={() => {
        if (location.pathname !== "/") {
          location.href = "/?intro=1";
          return;
        }
        window.scrollTo({ top: 0 });
        window.dispatchEvent(new Event(REPLAY_EVENT));
      }}
    >
      ▶ Introni qayta ko&apos;rish
    </button>
  );
}
