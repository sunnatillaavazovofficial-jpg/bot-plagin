import { devLoginEnabled } from "./env";

/**
 * Eskiz.uz orqali SMS yuborish. https://documenter.getpostman.com/view/663428/RzfmES4z
 * Eslatma: Eskiz faqat oldindan tasdiqlangan shablon matnlarini yuboradi — SMS_TEMPLATE shu shablon bilan bir xil bo'lsin.
 */
const ESKIZ_API = "https://notify.eskiz.uz/api";

let cachedToken: string | null = null;

export function smsConfigured(): boolean {
  return Boolean(process.env.ESKIZ_EMAIL && process.env.ESKIZ_PASSWORD);
}

async function eskizToken(force = false): Promise<string> {
  if (cachedToken && !force) return cachedToken;
  const form = new FormData();
  form.set("email", process.env.ESKIZ_EMAIL!);
  form.set("password", process.env.ESKIZ_PASSWORD!);
  const res = await fetch(`${ESKIZ_API}/auth/login`, { method: "POST", body: form });
  if (!res.ok) throw new Error(`Eskiz login xatosi: ${res.status}`);
  const json = (await res.json()) as { data?: { token?: string } };
  if (!json.data?.token) throw new Error("Eskiz token qaytarmadi");
  cachedToken = json.data.token;
  return cachedToken;
}

async function eskizSend(phone: string, message: string, retry = true): Promise<void> {
  const token = await eskizToken();
  const form = new FormData();
  form.set("mobile_phone", phone.replace(/\D/g, ""));
  form.set("message", message);
  form.set("from", process.env.ESKIZ_FROM || "4546");
  const res = await fetch(`${ESKIZ_API}/message/sms/send`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    body: form,
  });
  if (res.status === 401 && retry) {
    await eskizToken(true);
    return eskizSend(phone, message, false);
  }
  if (!res.ok) throw new Error(`Eskiz SMS xatosi: ${res.status} ${await res.text()}`);
}

/** Kodni SMS orqali yuboradi. Sinov rejimida (DEV_LOGIN=1) faqat konsolga chiqaradi. */
export async function sendLoginCode(phone: string, code: string): Promise<{ delivered: boolean }> {
  if (devLoginEnabled() && !smsConfigured()) {
    console.log(`[SMS sinov] ${phone} uchun kod: ${code}`);
    return { delivered: false };
  }
  if (!smsConfigured()) throw new Error("SMS xizmati sozlanmagan (ESKIZ_EMAIL / ESKIZ_PASSWORD)");
  const template = process.env.SMS_TEMPLATE || "PP Design saytiga kirish kodi: {code}";
  await eskizSend(phone, template.replace("{code}", code));
  return { delivered: true };
}
