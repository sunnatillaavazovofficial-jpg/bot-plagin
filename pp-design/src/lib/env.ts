/** Muhit o'zgaruvchilari bilan ishlash uchun yordamchilar (.env.example ga qarang). */

export function siteUrl(): string {
  return (process.env.SITE_URL || "http://localhost:3000").replace(/\/+$/, "");
}

export function isHttps(): boolean {
  return siteUrl().startsWith("https://");
}

export function sessionSecret(): Uint8Array {
  const secret = process.env.SESSION_SECRET;
  if (!secret || secret.length < 32) {
    if (process.env.NODE_ENV === "production") {
      throw new Error("SESSION_SECRET kamida 32 belgidan iborat bo'lishi kerak");
    }
    return new TextEncoder().encode("dev-only-secret-change-me-dev-only-secret");
  }
  return new TextEncoder().encode(secret);
}

/** Lokal sinov rejimi: SMS yuborilmaydi va test kirish tugmalari ko'rinadi. Productionda hech qachon yoqilmaydi. */
export function devLoginEnabled(): boolean {
  return process.env.NODE_ENV !== "production" && process.env.DEV_LOGIN === "1";
}

export function botToken(): string | null {
  return process.env.TELEGRAM_BOT_TOKEN?.trim() || null;
}

export function botUsername(): string | null {
  return process.env.TELEGRAM_BOT_USERNAME?.trim().replace(/^@/, "") || null;
}

export function adminTelegramIds(): bigint[] {
  return (process.env.TELEGRAM_ADMIN_IDS || "")
    .split(/[\s,]+/)
    .filter((s) => /^-?\d+$/.test(s))
    .map((s) => BigInt(s));
}

export function isAdminTelegramId(id: bigint | number | null | undefined): boolean {
  if (id === null || id === undefined) return false;
  const value = BigInt(id);
  return adminTelegramIds().some((adminId) => adminId === value);
}

export function uploadDir(): string {
  return process.env.UPLOAD_DIR || "./uploads";
}
