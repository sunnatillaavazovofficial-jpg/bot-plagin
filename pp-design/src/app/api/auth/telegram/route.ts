import { NextResponse } from "next/server";
import { loginWithTelegram, verifyTelegramAuth, type TelegramAuthData } from "@/lib/auth";
import { isSameOrigin, jsonError } from "@/lib/http";
import { createSession } from "@/lib/session";

/** Telegram Login Widget'dan kelgan ma'lumot bilan kirish (dizayner yoki admin). */
export async function POST(request: Request) {
  if (!isSameOrigin(request)) return jsonError("Ruxsat yo'q", 403);
  const data = (await request.json().catch(() => null)) as TelegramAuthData | null;
  if (!data || !verifyTelegramAuth(data)) return jsonError("Telegram ma'lumotlari tasdiqlanmadi", 401);

  const { userId, redirectTo } = await loginWithTelegram(data);
  await createSession(userId);
  return NextResponse.json({ ok: true, redirectTo });
}
