"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

declare global {
  interface Window {
    onTelegramAuth?: (user: Record<string, string | number>) => void;
  }
}

/**
 * Telegram Login Widget. Ishlashi uchun @BotFather'da /setdomain orqali sayt domenini botga bog'lash kerak.
 * https://core.telegram.org/widgets/login
 */
export function TelegramLogin({ botUsername, next }: { botUsername: string | null; next?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const router = useRouter();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!botUsername || !ref.current) return;
    window.onTelegramAuth = async (user) => {
      setBusy(true);
      setError("");
      const res = await fetch("/api/auth/telegram", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(user),
      });
      const json = await res.json().catch(() => ({}));
      if (!res.ok || !json.ok) {
        setBusy(false);
        setError(json.error || "Kirishda xatolik");
        return;
      }
      router.push(next && next.startsWith("/") && !next.startsWith("//") ? next : json.redirectTo);
      router.refresh();
    };
    const script = document.createElement("script");
    script.src = "https://telegram.org/js/telegram-widget.js?22";
    script.async = true;
    script.setAttribute("data-telegram-login", botUsername);
    script.setAttribute("data-size", "large");
    script.setAttribute("data-radius", "14");
    script.setAttribute("data-onauth", "onTelegramAuth(user)");
    script.setAttribute("data-request-access", "write");
    ref.current.innerHTML = "";
    ref.current.appendChild(script);
    return () => {
      delete window.onTelegramAuth;
    };
  }, [botUsername, next, router]);

  if (!botUsername) {
    return (
      <p className="rounded-xl border border-amber-300/30 bg-amber-300/10 p-4 text-sm text-amber-100">
        Telegram bot hali sozlanmagan (TELEGRAM_BOT_USERNAME). Administrator bilan bog&apos;laning.
      </p>
    );
  }

  return (
    <div>
      <div ref={ref} className="flex min-h-12 justify-center" />
      {busy && <p className="mt-3 text-center text-sm text-mist">Kirilmoqda…</p>}
      {error && <p className="mt-3 text-center text-sm text-rose-300">{error}</p>}
    </div>
  );
}
