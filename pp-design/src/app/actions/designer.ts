"use server";

import { revalidatePath } from "next/cache";
import { z } from "zod";
import { getCurrentUser } from "@/lib/auth";
import { db } from "@/lib/db";
import { notifyResubmission } from "@/lib/notify";
import { removeUpload } from "@/lib/upload";
import { isValidUsername } from "@/lib/utils";
import type { ActionResult } from "./reviews";

async function currentDesigner() {
  const user = await getCurrentUser();
  if (!user || user.role !== "DESIGNER" || !user.designer) throw new Error("Avval dizayner sifatida kiring");
  if (user.designer.status === "BLOCKED") throw new Error("Profilingiz bloklangan");
  return { user, designer: user.designer };
}

function fail(error: unknown): ActionResult {
  return { ok: false, error: error instanceof Error ? error.message : "Xatolik yuz berdi" };
}

const ownUpload = (prefix: string) =>
  z
    .string()
    .refine((url) => url.startsWith(`/uploads/${prefix}/`) && !url.includes(".."), "Rasm noto'g'ri");

const profileSchema = z.object({
  fullName: z.string().trim().min(2, "Ismni kiriting").max(60),
  username: z.string().trim().toLowerCase().refine(isValidUsername, "Manzil 3–30 belgi: lotin harflari, raqam va «-»"),
  specialty: z.string().trim().max(80),
  bio: z.string().trim().max(1200),
  experienceYears: z.number().int().min(0).max(60),
  experience: z.string().trim().max(2000),
  skills: z.array(z.string().trim().min(1).max(30)).max(15),
  prices: z.array(z.object({ title: z.string().trim().min(1).max(80), price: z.string().trim().max(60) })).max(15),
  socials: z.object({
    instagram: z.string().trim().max(120).optional(),
    telegram: z.string().trim().max(120).optional(),
    behance: z.string().trim().max(120).optional(),
    dribbble: z.string().trim().max(120).optional(),
    website: z.string().trim().max(200).optional(),
  }),
  contactPhone: z.string().trim().max(30),
  contactEmail: z.union([z.literal(""), z.string().trim().email("Email noto'g'ri")]),
  avatarUrl: z.union([ownUpload("avatar"), z.string().url(), z.null()]),
});

export type ProfileInput = z.infer<typeof profileSchema>;

export async function updateProfile(input: ProfileInput): Promise<ActionResult> {
  try {
    const { designer } = await currentDesigner();
    const parsed = profileSchema.safeParse(input);
    if (!parsed.success) return { ok: false, error: parsed.error.issues[0]?.message ?? "Ma'lumot noto'g'ri" };
    const data = parsed.data;

    // Avatar faqat o'zi yuklagan fayl yoki avvalgi (Telegram) rasm bo'lishi mumkin
    if (data.avatarUrl && !data.avatarUrl.startsWith("/uploads/") && data.avatarUrl !== designer.avatarUrl) {
      return { ok: false, error: "Rasm noto'g'ri" };
    }
    if (data.username !== designer.username) {
      const taken = await db.designer.findUnique({ where: { username: data.username }, select: { id: true } });
      if (taken) return { ok: false, error: "Bu manzil band, boshqasini tanlang" };
    }

    const skills = Array.from(new Set(data.skills));
    const socials = Object.fromEntries(Object.entries(data.socials).filter(([, v]) => v));
    await db.designer.update({
      where: { id: designer.id },
      data: { ...data, skills, socials, contactPhone: data.contactPhone || null, contactEmail: data.contactEmail || null },
    });
    if (designer.avatarUrl !== data.avatarUrl) await removeUpload(designer.avatarUrl);

    revalidatePath("/dashboard");
    revalidatePath(`/designer/${designer.username}`);
    revalidatePath(`/designer/${data.username}`);
    revalidatePath("/");
    return { ok: true, message: "Profil saqlandi" };
  } catch (error) {
    return fail(error);
  }
}

const portfolioSchema = z.object({
  title: z.string().trim().min(1, "Ish nomini kiriting").max(100),
  description: z.string().trim().max(500),
  imageUrl: ownUpload("portfolio"),
});

export async function addPortfolioItem(input: z.infer<typeof portfolioSchema>): Promise<ActionResult> {
  try {
    const { designer } = await currentDesigner();
    const parsed = portfolioSchema.safeParse(input);
    if (!parsed.success) return { ok: false, error: parsed.error.issues[0]?.message ?? "Ma'lumot noto'g'ri" };
    const count = await db.portfolioItem.count({ where: { designerId: designer.id } });
    if (count >= 40) return { ok: false, error: "Portfolio uchun 40 tagacha ish qo'shish mumkin" };
    await db.portfolioItem.create({ data: { ...parsed.data, designerId: designer.id, order: count } });
    revalidatePath("/dashboard");
    revalidatePath(`/designer/${designer.username}`);
    revalidatePath("/");
    return { ok: true, message: "Ish qo'shildi" };
  } catch (error) {
    return fail(error);
  }
}

export async function deletePortfolioItem(id: string): Promise<ActionResult> {
  try {
    const { designer } = await currentDesigner();
    const item = await db.portfolioItem.findFirst({ where: { id, designerId: designer.id } });
    if (!item) return { ok: false, error: "Topilmadi" };
    await db.portfolioItem.delete({ where: { id } });
    await removeUpload(item.imageUrl);
    revalidatePath("/dashboard");
    revalidatePath(`/designer/${designer.username}`);
    return { ok: true, message: "O'chirildi" };
  } catch (error) {
    return fail(error);
  }
}

export async function movePortfolioItem(id: string, direction: -1 | 1): Promise<ActionResult> {
  try {
    const { designer } = await currentDesigner();
    const items = await db.portfolioItem.findMany({ where: { designerId: designer.id }, orderBy: [{ order: "asc" }, { createdAt: "asc" }] });
    const index = items.findIndex((item) => item.id === id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= items.length) return { ok: true };
    [items[index], items[target]] = [items[target], items[index]];
    await db.$transaction(items.map((item, order) => db.portfolioItem.update({ where: { id: item.id }, data: { order } })));
    revalidatePath("/dashboard");
    revalidatePath(`/designer/${designer.username}`);
    return { ok: true };
  } catch (error) {
    return fail(error);
  }
}

/** Rad etilgan dizayner ma'lumotlarini to'ldirib, qayta so'rov yuboradi */
export async function resubmitApplication(): Promise<ActionResult> {
  try {
    const { user, designer } = await currentDesigner();
    if (designer.status !== "REJECTED") return { ok: false, error: "So'rov allaqachon ko'rib chiqilmoqda" };
    const updated = await db.designer.update({ where: { id: designer.id }, data: { status: "PENDING" } });
    await notifyResubmission(updated, user);
    revalidatePath("/dashboard");
    return { ok: true, message: "So'rov adminga qayta yuborildi" };
  } catch (error) {
    return fail(error);
  }
}
