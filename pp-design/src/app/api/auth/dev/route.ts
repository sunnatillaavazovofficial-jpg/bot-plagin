import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { loginWithTelegram } from "@/lib/auth";
import { adminTelegramIds, devLoginEnabled } from "@/lib/env";
import { jsonError } from "@/lib/http";
import { PASSWORD_ADMIN_ID, createSession } from "@/lib/session";

/**
 * FAQAT LOKAL SINOV UCHUN (DEV_LOGIN=1 va NODE_ENV != production).
 * Telegram Login Widget localhost'da ishlamagani uchun dizayner/admin sifatida kirishni taqlid qiladi.
 */
export async function POST(request: Request) {
  if (!devLoginEnabled()) return jsonError("Topilmadi", 404);
  const { as, telegramId, name } = (await request.json().catch(() => ({}))) as {
    as?: "designer" | "admin" | "client";
    telegramId?: number;
    name?: string;
  };

  if (as === "admin") {
    const adminId = adminTelegramIds()[0];
    if (!adminId) {
      await createSession(PASSWORD_ADMIN_ID);
      return NextResponse.json({ ok: true, redirectTo: "/admin" });
    }
  }

  if (as === "client") {
    const phone = "+998900000001";
    const user = await db.user.upsert({
      where: { phone },
      create: { role: "CLIENT", phone, name: name || "Test mijoz" },
      update: {},
    });
    await createSession(user.id);
    return NextResponse.json({ ok: true, redirectTo: "/" });
  }

  const id = as === "admin" ? Number(adminTelegramIds()[0]) : telegramId || 700000000 + Math.floor(Math.random() * 1000);
  const { userId, redirectTo } = await loginWithTelegram({
    id,
    first_name: name || "Test",
    last_name: as === "admin" ? "Admin" : "Dizayner",
    username: as === "admin" ? undefined : `test_${id}`,
    auth_date: Math.floor(Date.now() / 1000),
    hash: "dev",
  });
  await createSession(userId);
  return NextResponse.json({ ok: true, redirectTo });
}
