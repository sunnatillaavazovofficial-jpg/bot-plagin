import type { DesignerStatus, Prisma } from "@prisma/client";
import { db } from "./db";
import { notifyDesignerStatus } from "./notify";
import { averageRating } from "./utils";

/** Dizayner holatini o'zgartiradi (admin panel ham, bot ham shu funksiyadan foydalanadi) va dizaynerga xabar beradi. */
export async function setDesignerStatus(designerId: string, status: DesignerStatus) {
  const current = await db.designer.findUnique({ where: { id: designerId } });
  if (!current) return null;
  if (current.status === status) return { designer: current, changed: false };

  const designer = await db.designer.update({
    where: { id: designerId },
    data: { status, approvedAt: status === "APPROVED" ? new Date() : current.approvedAt },
    include: { user: { select: { telegramId: true } } },
  });
  await notifyDesignerStatus(designer);
  return { designer, changed: true };
}

export type PriceItem = { title: string; price: string };
export type Socials = Partial<Record<"instagram" | "telegram" | "behance" | "dribbble" | "website", string>>;

export function parsePrices(value: Prisma.JsonValue): PriceItem[] {
  if (!Array.isArray(value)) return [];
  return value
    .filter((item): item is Prisma.JsonObject => typeof item === "object" && item !== null && !Array.isArray(item))
    .map((item) => ({ title: String(item.title ?? ""), price: String(item.price ?? "") }))
    .filter((item) => item.title);
}

export function parseSocials(value: Prisma.JsonValue): Socials {
  if (typeof value !== "object" || value === null || Array.isArray(value)) return {};
  const result: Socials = {};
  for (const key of ["instagram", "telegram", "behance", "dribbble", "website"] as const) {
    const v = (value as Prisma.JsonObject)[key];
    if (typeof v === "string" && v.trim()) result[key] = v.trim();
  }
  return result;
}

export type DesignerCard = {
  id: string;
  username: string;
  fullName: string;
  specialty: string;
  avatarUrl: string | null;
  skills: string[];
  rating: number;
  reviewCount: number;
  cover: string | null;
};

/** Bosh sahifa va ro'yxat uchun tasdiqlangan dizaynerlar */
export async function approvedDesignerCards(limit?: number): Promise<DesignerCard[]> {
  const designers = await db.designer.findMany({
    where: { status: "APPROVED" },
    orderBy: [{ approvedAt: "asc" }, { createdAt: "asc" }],
    take: limit,
    include: {
      reviews: { select: { rating: true } },
      portfolio: { orderBy: { order: "asc" }, take: 1, select: { imageUrl: true } },
    },
  });
  return designers.map((d) => ({
    id: d.id,
    username: d.username,
    fullName: d.fullName,
    specialty: d.specialty,
    avatarUrl: d.avatarUrl,
    skills: d.skills.slice(0, 3),
    rating: averageRating(d.reviews.map((r) => r.rating)),
    reviewCount: d.reviews.length,
    cover: d.portfolio[0]?.imageUrl ?? null,
  }));
}
