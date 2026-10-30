"use server";

import { revalidatePath } from "next/cache";
import { z } from "zod";
import { getCurrentUser } from "@/lib/auth";
import { db } from "@/lib/db";
import { rateLimit } from "@/lib/http";
import { notifyNewReview } from "@/lib/notify";

export type ActionResult = { ok: true; message?: string } | { ok: false; error: string };

const reviewSchema = z.object({
  designerId: z.string().min(1),
  rating: z.number().int().min(1).max(5),
  text: z.string().trim().min(5, "Otziv kamida 5 belgidan iborat bo'lsin").max(1500, "Otziv juda uzun (1500 belgigacha)"),
  name: z.string().trim().max(60).optional(),
});

/** Mijoz dizaynerga otziv qoldiradi (yoki avvalgisini yangilaydi). Otziv Telegram botga yuboriladi. */
export async function submitReview(input: z.infer<typeof reviewSchema>): Promise<ActionResult> {
  const user = await getCurrentUser();
  if (!user || user.role !== "CLIENT") return { ok: false, error: "Otziv qoldirish uchun mijoz sifatida kiring" };

  const parsed = reviewSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: parsed.error.issues[0]?.message ?? "Ma'lumot noto'g'ri" };
  const { designerId, rating, text, name } = parsed.data;

  if (!rateLimit(`review:${user.id}`, 10, 60 * 60 * 1000)) return { ok: false, error: "Juda ko'p urinish. Keyinroq urinib ko'ring." };

  const designer = await db.designer.findUnique({
    where: { id: designerId },
    include: { user: { select: { telegramId: true } } },
  });
  if (!designer || designer.status !== "APPROVED") return { ok: false, error: "Dizayner topilmadi" };

  let clientName = user.name;
  if (!clientName && name) {
    await db.user.update({ where: { id: user.id }, data: { name } });
    clientName = name;
  }
  if (!clientName) return { ok: false, error: "Ismingizni kiriting" };

  const existing = await db.review.findUnique({ where: { designerId_clientId: { designerId, clientId: user.id } } });
  const review = await db.review.upsert({
    where: { designerId_clientId: { designerId, clientId: user.id } },
    create: { designerId, clientId: user.id, rating, text },
    update: { rating, text },
  });

  await notifyNewReview(review, designer, { name: clientName, phone: user.phone }, Boolean(existing));
  revalidatePath(`/designer/${designer.username}`);
  revalidatePath("/");
  return { ok: true, message: existing ? "Otzivingiz yangilandi" : "Rahmat! Otzivingiz qabul qilindi" };
}
