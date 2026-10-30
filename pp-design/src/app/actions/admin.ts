"use server";

import { revalidatePath } from "next/cache";
import { z } from "zod";
import type { DesignerStatus } from "@prisma/client";
import { assertAdmin } from "@/lib/auth";
import { db } from "@/lib/db";
import { setDesignerStatus } from "@/lib/designers";
import { saveSettings, type SiteSettings } from "@/lib/settings";
import { removeUpload } from "@/lib/upload";
import type { ActionResult } from "./reviews";

function fail(error: unknown): ActionResult {
  return { ok: false, error: error instanceof Error ? error.message : "Xatolik yuz berdi" };
}

function refreshAll() {
  revalidatePath("/", "layout");
}

const uploadUrl = (prefix: string) => z.string().refine((url) => url.startsWith(`/uploads/${prefix}/`) && !url.includes(".."), "Fayl noto'g'ri");

// ---------------- Dizaynerlar ----------------

export async function adminSetDesignerStatus(id: string, status: DesignerStatus): Promise<ActionResult> {
  try {
    await assertAdmin();
    const result = await setDesignerStatus(id, status);
    if (!result) return { ok: false, error: "Dizayner topilmadi" };
    refreshAll();
    return { ok: true, message: "Holat yangilandi" };
  } catch (error) {
    return fail(error);
  }
}

export async function adminDeleteDesigner(id: string): Promise<ActionResult> {
  try {
    await assertAdmin();
    const designer = await db.designer.findUnique({ where: { id }, include: { portfolio: true } });
    if (!designer) return { ok: false, error: "Topilmadi" };
    await db.user.delete({ where: { id: designer.userId } });
    await Promise.all([removeUpload(designer.avatarUrl), ...designer.portfolio.map((p) => removeUpload(p.imageUrl))]);
    refreshAll();
    return { ok: true, message: "Dizayner o'chirildi" };
  } catch (error) {
    return fail(error);
  }
}

// ---------------- Otzivlar ----------------

export async function adminDeleteReview(id: string): Promise<ActionResult> {
  try {
    await assertAdmin();
    await db.review.delete({ where: { id } });
    refreshAll();
    return { ok: true, message: "Otziv o'chirildi" };
  } catch (error) {
    return fail(error);
  }
}

// ---------------- Ishlar (portfolio) ----------------

const projectSchema = z.object({
  title: z.string().trim().min(1, "Nomini kiriting").max(120),
  category: z.string().trim().max(40),
  description: z.string().trim().max(500),
  imageUrl: uploadUrl("project"),
});

export async function adminAddProject(input: z.infer<typeof projectSchema>): Promise<ActionResult> {
  try {
    await assertAdmin();
    const parsed = projectSchema.safeParse(input);
    if (!parsed.success) return { ok: false, error: parsed.error.issues[0]?.message ?? "Ma'lumot noto'g'ri" };
    const count = await db.project.count();
    await db.project.create({ data: { ...parsed.data, order: count } });
    refreshAll();
    return { ok: true, message: "Ish qo'shildi" };
  } catch (error) {
    return fail(error);
  }
}

export async function adminDeleteProject(id: string): Promise<ActionResult> {
  try {
    await assertAdmin();
    const project = await db.project.delete({ where: { id } });
    await removeUpload(project.imageUrl);
    refreshAll();
    return { ok: true, message: "O'chirildi" };
  } catch (error) {
    return fail(error);
  }
}

export async function adminMove(kind: "project" | "partner", id: string, direction: -1 | 1): Promise<ActionResult> {
  try {
    await assertAdmin();
    const items =
      kind === "project"
        ? await db.project.findMany({ orderBy: [{ order: "asc" }, { createdAt: "desc" }], select: { id: true } })
        : await db.partner.findMany({ orderBy: [{ order: "asc" }, { createdAt: "asc" }], select: { id: true } });
    const index = items.findIndex((item) => item.id === id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= items.length) return { ok: true };
    [items[index], items[target]] = [items[target], items[index]];
    await db.$transaction(
      items.map((item, order) =>
        kind === "project" ? db.project.update({ where: { id: item.id }, data: { order } }) : db.partner.update({ where: { id: item.id }, data: { order } }),
      ),
    );
    refreshAll();
    return { ok: true };
  } catch (error) {
    return fail(error);
  }
}

// ---------------- Hamkorlar ----------------

const partnerSchema = z.object({
  name: z.string().trim().min(1, "Nomini kiriting").max(80),
  url: z.union([z.literal(""), z.string().trim().url("Havola noto'g'ri (https://...)")]),
  logoUrl: uploadUrl("partner"),
});

export async function adminAddPartner(input: z.infer<typeof partnerSchema>): Promise<ActionResult> {
  try {
    await assertAdmin();
    const parsed = partnerSchema.safeParse(input);
    if (!parsed.success) return { ok: false, error: parsed.error.issues[0]?.message ?? "Ma'lumot noto'g'ri" };
    const count = await db.partner.count();
    await db.partner.create({ data: { ...parsed.data, url: parsed.data.url || null, order: count } });
    refreshAll();
    return { ok: true, message: "Hamkor qo'shildi" };
  } catch (error) {
    return fail(error);
  }
}

export async function adminDeletePartner(id: string): Promise<ActionResult> {
  try {
    await assertAdmin();
    const partner = await db.partner.delete({ where: { id } });
    await removeUpload(partner.logoUrl);
    refreshAll();
    return { ok: true, message: "O'chirildi" };
  } catch (error) {
    return fail(error);
  }
}

// ---------------- Sozlamalar (matnlar, aloqa, intro) ----------------

const text = (max: number) => z.string().trim().max(max);

const settingsSchema = z.object({
  brand: z.object({
    name: text(40).min(1, "Kompaniya nomini kiriting"),
    tagline: text(160),
    logoUrl: z.union([uploadUrl("logo"), z.null()]),
  }),
  hero: z.object({
    badge: text(60),
    lines: z.array(text(90).min(1)).min(1, "Kamida bitta gap kiriting").max(8),
    subtitle: text(400),
  }),
  about: z.object({
    title: text(80),
    text: text(2000),
    stats: z.array(z.object({ value: text(12).min(1), label: text(40).min(1) })).max(6),
    services: z.array(z.object({ title: text(60).min(1), text: text(240) })).max(9),
  }),
  contact: z.object({
    address: text(200),
    phone: text(30),
    phone2: text(30),
    email: z.union([z.literal(""), z.string().trim().email("Email noto'g'ri")]),
    telegram: text(100),
    instagram: text(100),
    workHours: text(80),
    lat: z.number().min(-90).max(90),
    lng: z.number().min(-180).max(180),
    mapProvider: z.enum(["yandex", "google"]),
  }),
  intro: z.object({ enabled: z.boolean(), videoUrl: z.union([uploadUrl("video"), z.null()]) }),
  seo: z.object({ description: text(300) }),
});

/** Sozlamalarning bir bo'limini saqlash (masalan faqat "contact") */
export async function adminSaveSettings<K extends keyof SiteSettings>(section: K, value: SiteSettings[K]): Promise<ActionResult> {
  try {
    await assertAdmin();
    const schema = settingsSchema.shape[section];
    const parsed = schema.safeParse(value);
    if (!parsed.success) return { ok: false, error: parsed.error.issues[0]?.message ?? "Ma'lumot noto'g'ri" };
    let previousFile: string | null = null;
    await saveSettings((current) => {
      if (section === "brand") previousFile = current.brand.logoUrl;
      if (section === "intro") previousFile = current.intro.videoUrl;
      return { ...current, [section]: parsed.data };
    });
    const nextFile = section === "brand" ? (parsed.data as SiteSettings["brand"]).logoUrl : section === "intro" ? (parsed.data as SiteSettings["intro"]).videoUrl : null;
    if (previousFile && previousFile !== nextFile) await removeUpload(previousFile);
    refreshAll();
    return { ok: true, message: "Saqlandi" };
  } catch (error) {
    return fail(error);
  }
}
