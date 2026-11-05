/** Sayt Telegram ichida (Mini App) ochilganda ishlatiladigan yordamchilar. */

declare global {
  interface Window {
    Telegram?: { WebApp?: { initData?: string; ready: () => void; expand: () => void } };
  }
}

const KEY = "tg_init_data";

/** Telegram Mini App ma'lumoti (URL #tgWebAppData=... dan yoki saqlangan nusxadan). */
export function telegramInitData(): string | null {
  try {
    const fromHash = new URLSearchParams(location.hash.slice(1)).get("tgWebAppData");
    if (fromHash) {
      sessionStorage.setItem(KEY, fromHash);
      return fromHash;
    }
    return sessionStorage.getItem(KEY) ?? window.Telegram?.WebApp?.initData ?? null;
  } catch {
    return null;
  }
}

/** Telegram skriptini faqat Mini App ichida yuklaymiz (oddiy brauzerda tashqi skript kerak emas). */
export function setupTelegramWebApp(): void {
  if (!telegramInitData() || document.getElementById("tg-webapp-js")) return;
  const script = document.createElement("script");
  script.id = "tg-webapp-js";
  script.src = "https://telegram.org/js/telegram-web-app.js";
  script.onload = () => {
    try {
      window.Telegram?.WebApp?.ready();
      window.Telegram?.WebApp?.expand();
    } catch {
      /* e'tiborsiz */
    }
  };
  document.head.appendChild(script);
}
