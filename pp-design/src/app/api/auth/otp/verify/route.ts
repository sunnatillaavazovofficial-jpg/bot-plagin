import { timingSafeEqual } from "node:crypto";
import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { clientIp, isSameOrigin, jsonError, rateLimit } from "@/lib/http";
import { hashOtp } from "@/lib/otp";
import { normalizeUzPhone } from "@/lib/phone";
import { createSession } from "@/lib/session";

const MAX_ATTEMPTS = 5;

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return jsonError("Ruxsat yo'q", 403);
  if (!rateLimit(`otp-verify:${clientIp(request)}`, 30, 60 * 60 * 1000)) {
    return jsonError("Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring.", 429);
  }

  const body = (await request.json().catch(() => null)) as { phone?: string; code?: string; name?: string } | null;
  const phone = normalizeUzPhone(body?.phone ?? "");
  const code = (body?.code ?? "").replace(/\D/g, "");
  const name = (body?.name ?? "").trim().slice(0, 60);
  if (!phone || code.length !== 6) return jsonError("Kodni to'liq kiriting");

  const otp = await db.otpCode.findFirst({
    where: { phone, usedAt: null, expiresAt: { gt: new Date() } },
    orderBy: { createdAt: "desc" },
  });
  if (!otp) return jsonError("Kod eskirgan. Yangi kod oling.");
  if (otp.attempts >= MAX_ATTEMPTS) return jsonError("Urinishlar tugadi. Yangi kod oling.");

  const expected = Buffer.from(otp.codeHash, "hex");
  const received = Buffer.from(hashOtp(phone, code), "hex");
  if (!timingSafeEqual(expected, received)) {
    await db.otpCode.update({ where: { id: otp.id }, data: { attempts: { increment: 1 } } });
    const left = MAX_ATTEMPTS - otp.attempts - 1;
    return jsonError(left > 0 ? `Kod noto'g'ri. Yana ${left} ta urinish qoldi.` : "Urinishlar tugadi. Yangi kod oling.");
  }

  await db.otpCode.update({ where: { id: otp.id }, data: { usedAt: new Date() } });

  const existing = await db.user.findUnique({ where: { phone } });
  if (existing && existing.role !== "CLIENT") return jsonError("Bu raqam mijoz hisobiga tegishli emas");
  const user = existing
    ? name && name !== existing.name
      ? await db.user.update({ where: { id: existing.id }, data: { name } })
      : existing
    : await db.user.create({ data: { role: "CLIENT", phone, name: name || null } });

  await createSession(user.id);
  return NextResponse.json({ ok: true, needsName: !user.name });
}
