import { cache } from "react";
import type { Prisma } from "@prisma/client";
import { db } from "./db";

export type Stat = { value: string; label: string };
export type Service = { title: string; text: string };

export type SiteSettings = {
  brand: { name: string; tagline: string; logoUrl: string | null };
  hero: { badge: string; lines: string[]; subtitle: string };
  about: { title: string; text: string; stats: Stat[]; services: Service[] };
  contact: {
    address: string;
    phone: string;
    phone2: string;
    email: string;
    telegram: string;
    instagram: string;
    workHours: string;
    lat: number;
    lng: number;
    mapProvider: "yandex" | "google";
  };
  intro: { enabled: boolean; videoUrl: string | null };
  seo: { description: string };
};

export const DEFAULT_SETTINGS: SiteSettings = {
  brand: { name: "PP Design", tagline: "Dizayn — g'or devoridan sun'iy intellektgacha", logoUrl: null },
  hero: {
    badge: "Dizaynerlar platformasi",
    lines: [
      "G'oyangizni dizaynga aylantiramiz.",
      "Logotip, brending va reklama — bir joyda.",
      "Eng yaxshi dizaynerlar — bitta platformada.",
      "Inson ijodi + sun'iy intellekt kuchi.",
    ],
    subtitle:
      "PP Design — O'zbekistondagi dizaynerlar va mijozlarni bog'laydigan platforma. Dizayner tanlang, portfoliosini ko'ring va loyihangizni ishonchli qo'llarga topshiring.",
  },
  about: {
    title: "Biz haqimizda",
    text:
      "PP Design — reklama va dizayn agentligi. Biz brendlar uchun logotip, firma uslubi, tashqi reklama, poligrafiya va raqamli dizayn yaratamiz. Jamoamizda turli yo'nalishdagi tajribali dizaynerlar ishlaydi va har bir loyihaga individual yondashamiz.",
    stats: [
      { value: "8+", label: "yillik tajriba" },
      { value: "1200+", label: "bajarilgan loyiha" },
      { value: "300+", label: "mamnun mijoz" },
      { value: "40+", label: "hamkor kompaniya" },
    ],
    services: [
      { title: "Logotip va brending", text: "Brend g'oyasidan to'liq firma uslubigacha: logo, ranglar, shriftlar, brendbuk." },
      { title: "Tashqi reklama", text: "Hajmli harflar, lightbox, banner va vivesekalar uchun maket va vizualizatsiya." },
      { title: "Poligrafiya", text: "Vizitka, katalog, buklet, qadoq va boshqa bosma mahsulotlar dizayni." },
      { title: "UI/UX va veb", text: "Sayt, ilova va Telegram bot interfeyslari — qulay va chiroyli." },
      { title: "SMM dizayn", text: "Ijtimoiy tarmoqlar uchun post, story va reklama kreativlari." },
      { title: "AI dizayn", text: "Sun'iy intellekt yordamida tezkor konseptlar, vizualizatsiya va kreativlar." },
    ],
  },
  contact: {
    address: "Toshkent shahri, Amir Temur ko'chasi",
    phone: "+998 90 000 00 00",
    phone2: "",
    email: "info@pp-design.uz",
    telegram: "ppdesign",
    instagram: "ppdesign",
    workHours: "Du–Sha, 09:00–19:00",
    lat: 41.311081,
    lng: 69.279737,
    mapProvider: "yandex",
  },
  intro: { enabled: true, videoUrl: null },
  seo: {
    description:
      "PP Design — dizaynerlar platformasi: eng yaxshi dizaynerlar portfoliosi, mijozlar otzivlari, logotip, brending va reklama dizayni.",
  },
};

const KEY = "site";

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Saqlangan qiymatni standart qiymatlar ustiga qo'yadi (yangi maydon qo'shilsa ham ishlayveradi). */
function merge<T>(defaults: T, saved: unknown): T {
  if (!isObject(defaults) || !isObject(saved)) {
    if (saved === undefined) return defaults;
    if (Array.isArray(defaults)) return (Array.isArray(saved) ? saved : defaults) as T;
    return (typeof saved === typeof defaults || (defaults === null && typeof saved === "string") ? saved : defaults) as T;
  }
  const result: Record<string, unknown> = { ...defaults };
  for (const key of Object.keys(defaults)) result[key] = merge((defaults as Record<string, unknown>)[key], saved[key]);
  return result as T;
}

export const getSettings = cache(async (): Promise<SiteSettings> => {
  try {
    const row = await db.setting.findUnique({ where: { key: KEY } });
    return merge(DEFAULT_SETTINGS, row?.value);
  } catch (error) {
    console.error("Sozlamalarni o'qib bo'lmadi, standart qiymatlar ishlatiladi:", error);
    return DEFAULT_SETTINGS;
  }
});

export async function saveSettings(update: (current: SiteSettings) => SiteSettings): Promise<SiteSettings> {
  const row = await db.setting.findUnique({ where: { key: KEY } });
  const next = update(merge(DEFAULT_SETTINGS, row?.value));
  await db.setting.upsert({
    where: { key: KEY },
    create: { key: KEY, value: next as unknown as Prisma.InputJsonValue },
    update: { value: next as unknown as Prisma.InputJsonValue },
  });
  return next;
}
