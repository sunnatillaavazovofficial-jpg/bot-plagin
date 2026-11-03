const MONTHS = ["yan", "fev", "mar", "apr", "may", "iyn", "iyl", "avg", "sen", "okt", "noy", "dek"];

export function formatSum(value: number | null | undefined, withUnit = true): string {
  const n = Math.round(value ?? 0);
  const s = Math.abs(n)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, " ");
  return `${n < 0 ? "-" : ""}${s}${withUnit ? " so'm" : ""}`;
}

export function compactSum(value: number): string {
  const abs = Math.abs(value);
  if (abs >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1).replace(/\.0$/, "")} mlrd`;
  if (abs >= 1_000_000) return `${(value / 1_000_000).toFixed(1).replace(/\.0$/, "")} mln`;
  if (abs >= 1_000) return `${Math.round(value / 1_000)} ming`;
  return String(value);
}

export function parseSum(text: string): number {
  const digits = text.replace(/[^\d-]/g, "");
  return digits ? parseInt(digits, 10) : 0;
}

const pad = (n: number) => String(n).padStart(2, "0");

export function formatTime(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function formatDate(iso: string | null, withYear = false): string {
  if (!iso) return "";
  const d = new Date(iso.length === 10 ? iso + "T00:00:00" : iso);
  const base = `${d.getDate()}-${MONTHS[d.getMonth()]}`;
  return withYear || d.getFullYear() !== new Date().getFullYear() ? `${base} ${d.getFullYear()}` : base;
}

export function formatDateTime(iso: string | null): string {
  if (!iso) return "";
  return `${formatDate(iso)}, ${formatTime(iso)}`;
}

export function chatTime(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const now = new Date();
  if (d.toDateString() === now.toDateString()) return formatTime(iso);
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (d.toDateString() === yesterday.toDateString()) return "kecha";
  return formatDate(iso);
}

export function dayLabel(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  if (d.toDateString() === now.toDateString()) return "Bugun";
  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (d.toDateString() === yesterday.toDateString()) return "Kecha";
  return formatDate(iso, true);
}

export function minutesAgo(iso: string | null): number {
  if (!iso) return 0;
  return Math.floor((Date.now() - new Date(iso).getTime()) / 60000);
}

export function waitLabel(iso: string | null): string {
  const m = minutesAgo(iso);
  if (m < 60) return `${m} daq`;
  if (m < 24 * 60) return `${Math.floor(m / 60)} soat`;
  return `${Math.floor(m / 1440)} kun`;
}

export function initials(name: string): string {
  const parts = name.replace(/[^\p{L}\p{N} ]/gu, " ").trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return "?";
  return (parts[0][0] + (parts[1]?.[0] ?? "")).toUpperCase();
}

export function fileSize(bytes: number | null): string {
  if (!bytes) return "";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export function todayISO(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function deadlineState(deadline: string | null, closed: boolean): "overdue" | "today" | "soon" | null {
  if (!deadline || closed) return null;
  const today = todayISO();
  if (deadline < today) return "overdue";
  if (deadline === today) return "today";
  if (deadline === todayISO(1)) return "soon";
  return null;
}

export const ROLE_LABELS: Record<string, string> = { admin: "Admin", menejer: "Menejer", xodim: "Xodim" };
export const PAYMENT_LABELS: Record<string, string> = {
  naqd: "Naqd",
  karta: "Karta",
  "o'tkazma": "O'tkazma",
  boshqa: "Boshqa",
};
