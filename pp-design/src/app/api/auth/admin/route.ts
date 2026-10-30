import { createHash, timingSafeEqual } from "node:crypto";
import { NextResponse } from "next/server";
import { clientIp, isSameOrigin, jsonError, rateLimit } from "@/lib/http";
import { PASSWORD_ADMIN_ID, createSession } from "@/lib/session";

/** Zaxira yo'l: ADMIN_PASSWORD bilan admin panelga kirish. */
export async function POST(request: Request) {
  if (!isSameOrigin(request)) return jsonError("Ruxsat yo'q", 403);
  if (!rateLimit(`admin-login:${clientIp(request)}`, 10, 15 * 60 * 1000)) {
    return jsonError("Juda ko'p urinish. 15 daqiqadan keyin urinib ko'ring.", 429);
  }

  const expected = process.env.ADMIN_PASSWORD;
  if (!expected) return jsonError("Parol bilan kirish o'chirilgan", 403);

  const body = (await request.json().catch(() => null)) as { password?: string } | null;
  const digest = (value: string) => createHash("sha256").update(value).digest();
  if (!timingSafeEqual(digest(body?.password ?? ""), digest(expected))) return jsonError("Parol noto'g'ri", 401);

  await createSession(PASSWORD_ADMIN_ID);
  return NextResponse.json({ ok: true, redirectTo: "/admin" });
}
