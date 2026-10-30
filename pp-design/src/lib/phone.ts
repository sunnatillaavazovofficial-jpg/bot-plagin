/** O'zbekiston raqamini +998XXXXXXXXX ko'rinishiga keltiradi. Noto'g'ri bo'lsa null. */
export function normalizeUzPhone(input: string): string | null {
  let digits = input.replace(/\D/g, "");
  if (digits.length === 9) digits = "998" + digits;
  if (digits.length === 12 && digits.startsWith("998")) return "+" + digits;
  return null;
}

/** +998901234567 → +998 90 123 45 67 */
export function formatUzPhone(phone: string): string {
  const d = phone.replace(/\D/g, "");
  if (d.length !== 12) return phone;
  return `+${d.slice(0, 3)} ${d.slice(3, 5)} ${d.slice(5, 8)} ${d.slice(8, 10)} ${d.slice(10)}`;
}

/** +998 90 123 45 67 → +998901234567 (tel: havolasi uchun) */
export function telHref(phone: string): string {
  return "tel:+" + phone.replace(/\D/g, "");
}
