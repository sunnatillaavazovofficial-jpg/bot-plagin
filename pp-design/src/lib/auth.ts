import { createHash, createHmac, timingSafeEqual } from "node:crypto";
import { cache } from "react";
import { redirect } from "next/navigation";
import type { Designer, Role } from "@prisma/client";
import { db } from "./db";
import { botToken, isAdminTelegramId } from "./env";
import { PASSWORD_ADMIN_ID, readSession } from "./session";
import { slugify } from "./utils";
import { notifyNewDesigner } from "./notify";

export type CurrentUser = {
  id: string;
  role: Role;
  name: string | null;
  phone: string | null;
  telegramId: bigint | null;
  telegramUsername: string | null;
  designer: Designer | null;
};

/** Joriy so'rovdagi foydalanuvchi (har so'rovda bir marta bazadan o'qiladi). */
export const getCurrentUser = cache(async (): Promise<CurrentUser | null> => {
  const session = await readSession();
  if (!session) return null;

  if (session.uid === PASSWORD_ADMIN_ID) {
    if (!process.env.ADMIN_PASSWORD) return null;
    return {
      id: PASSWORD_ADMIN_ID,
      role: "ADMIN",
      name: "Admin",
      phone: null,
      telegramId: null,
      telegramUsername: null,
      designer: null,
    };
  }

  const user = await db.user.findUnique({ where: { id: session.uid }, include: { designer: true } });
  if (!user) return null;
  // Admin huquqi faqat TELEGRAM_ADMIN_IDS ro'yxatida bo'lsa amal qiladi
  if (user.role === "ADMIN" && !isAdminTelegramId(user.telegramId)) return null;
  return user;
});

export async function requireAdmin(): Promise<CurrentUser> {
  const user = await getCurrentUser();
  if (!user || user.role !== "ADMIN") redirect("/admin/login");
  return user;
}

export async function requireDesigner(): Promise<CurrentUser & { designer: Designer }> {
  const user = await getCurrentUser();
  if (!user || user.role !== "DESIGNER" || !user.designer) redirect("/login?tab=designer");
  return user as CurrentUser & { designer: Designer };
}

/** Server action / API ichida: admin bo'lmasa xato. */
export async function assertAdmin(): Promise<CurrentUser> {
  const user = await getCurrentUser();
  if (!user || user.role !== "ADMIN") throw new Error("Ruxsat yo'q");
  return user;
}

export type TelegramAuthData = {
  id: number | string;
  first_name?: string;
  last_name?: string;
  username?: string;
  photo_url?: string;
  auth_date: number | string;
  hash: string;
};

/** Telegram Login Widget ma'lumotini tekshirish: https://core.telegram.org/widgets/login#checking-authorization */
export function verifyTelegramAuth(data: TelegramAuthData): boolean {
  const token = botToken();
  if (!token || !data?.hash) return false;

  const { hash, ...fields } = data;
  const checkString = Object.keys(fields)
    .filter((key) => fields[key as keyof typeof fields] !== undefined && fields[key as keyof typeof fields] !== null)
    .sort()
    .map((key) => `${key}=${fields[key as keyof typeof fields]}`)
    .join("\n");

  const secret = createHash("sha256").update(token).digest();
  const expected = createHmac("sha256", secret).update(checkString).digest();
  const received = Buffer.from(String(hash), "hex");
  if (received.length !== expected.length || !timingSafeEqual(received, expected)) return false;

  const ageSeconds = Date.now() / 1000 - Number(data.auth_date);
  return ageSeconds < 60 * 60 * 24;
}

async function uniqueUsername(base: string): Promise<string> {
  const root = slugify(base).slice(0, 24) || "dizayner";
  for (let i = 0; i < 50; i++) {
    const candidate = i === 0 ? root : `${root}-${i + 1}`;
    const taken = await db.designer.findUnique({ where: { username: candidate }, select: { id: true } });
    if (!taken && !RESERVED_USERNAMES.has(candidate)) return candidate;
  }
  return `${root}-${Date.now().toString(36)}`;
}

const RESERVED_USERNAMES = new Set(["admin", "login", "dashboard", "api", "new", "edit"]);

/**
 * Telegram orqali kirgan foydalanuvchini bazaga yozadi.
 * - TELEGRAM_ADMIN_IDS da bo'lsa → admin
 * - aks holda → dizayner; birinchi marta bo'lsa PENDING holatda yaratiladi va adminga so'rov ketadi
 */
export async function loginWithTelegram(tg: TelegramAuthData): Promise<{ userId: string; redirectTo: string }> {
  const telegramId = BigInt(tg.id);
  const name = [tg.first_name, tg.last_name].filter(Boolean).join(" ").trim() || tg.username || "Dizayner";
  const telegramUsername = tg.username || null;

  if (isAdminTelegramId(telegramId)) {
    const user = await db.user.upsert({
      where: { telegramId },
      create: { role: "ADMIN", telegramId, telegramUsername, name },
      update: { role: "ADMIN", telegramUsername, name },
    });
    return { userId: user.id, redirectTo: "/admin" };
  }

  const existing = await db.user.findUnique({ where: { telegramId }, include: { designer: true } });
  const user = existing
    ? await db.user.update({
        where: { id: existing.id },
        data: { role: "DESIGNER", telegramUsername, name: existing.name || name },
      })
    : await db.user.create({ data: { role: "DESIGNER", telegramId, telegramUsername, name } });

  if (!existing?.designer) {
    const designer = await db.designer.create({
      data: {
        userId: user.id,
        username: await uniqueUsername(tg.username || `dizayner-${String(tg.id).slice(-5)}`),
        fullName: name,
        avatarUrl: tg.photo_url || null,
        socials: telegramUsername ? { telegram: telegramUsername } : {},
        status: "PENDING",
      },
    });
    await notifyNewDesigner(designer, user);
  }

  return { userId: user.id, redirectTo: "/dashboard" };
}
