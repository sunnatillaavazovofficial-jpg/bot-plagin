"use client";

import Link from "next/link";
import { useState } from "react";
import { cn } from "@/lib/utils";
import { DevLogin } from "./DevLogin";
import { PhoneLogin } from "./PhoneLogin";
import { TelegramLogin } from "./TelegramLogin";

export function LoginTabs({
  initialTab,
  next,
  botUsername,
  devLogin,
}: {
  initialTab: "client" | "designer";
  next?: string;
  botUsername: string | null;
  devLogin: boolean;
}) {
  const [tab, setTab] = useState(initialTab);
  return (
    <div className="glass w-full max-w-md rounded-3xl p-6 shadow-2xl sm:p-8">
      <div className="mb-6 grid grid-cols-2 gap-1 rounded-2xl bg-ink-950/60 p-1">
        {(
          [
            ["client", "Mijoz"],
            ["designer", "Dizayner"],
          ] as const
        ).map(([key, label]) => (
          <button
            key={key}
            onClick={() => setTab(key)}
            className={cn("rounded-xl py-2.5 text-sm font-semibold transition", tab === key ? "bg-white/10 text-white shadow" : "text-muted hover:text-white")}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "client" ? (
        <>
          <h1 className="font-display text-2xl font-semibold">Mijoz sifatida kirish</h1>
          <p className="mt-2 mb-6 text-sm text-mist/75">Telefon raqamingizni kiriting — SMS kod yuboramiz. Parol shart emas.</p>
          <PhoneLogin next={next} />
        </>
      ) : (
        <>
          <h1 className="font-display text-2xl font-semibold">Dizayner bo&apos;lib kirish</h1>
          <ol className="mt-4 mb-6 space-y-2 text-sm text-mist/80">
            <li>1. Telegram orqali ro&apos;yxatdan o&apos;ting.</li>
            <li>2. So&apos;rovingiz adminga yuboriladi.</li>
            <li>3. Admin tasdiqlagach, profilingiz saytda ko&apos;rinadi — uni o&apos;zingiz to&apos;ldirib borasiz.</li>
          </ol>
          <TelegramLogin botUsername={botUsername} next={next} />
          <p className="mt-4 text-center text-xs text-muted">Avval ro&apos;yxatdan o&apos;tgan bo&apos;lsangiz, shu tugma orqali kirasiz.</p>
        </>
      )}

      {devLogin && <DevLogin />}
      <p className="mt-6 text-center text-xs text-muted">
        <Link href="/" className="hover:text-white">
          ← Bosh sahifaga qaytish
        </Link>
      </p>
    </div>
  );
}
