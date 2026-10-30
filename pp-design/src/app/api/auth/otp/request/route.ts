import { randomInt } from "node:crypto";
import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { devLoginEnabled } from "@/lib/env";
import { clientIp, isSameOrigin, jsonError, rateLimit } from "@/lib/http";
import { hashOtp } from "@/lib/otp";
import { normalizeUzPhone } from "@/lib/phone";
import { sendLoginCode } from "@/lib/sms";

const RESEND_SECONDS = 60;
const CODE_TTL_MINUTES = 5;
const MAX_CODES_PER_DAY = 8;

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return jsonError("Ruxsat yo'q", 403);
  if (!rateLimit(`otp-ip:${clientIp(request)}`, 15, 60 * 60 * 1000)) {
    return jsonError("Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring.", 429);
  }

  const body = (await request.json().catch(() => null)) as { phone?: string } | null;
  const phone = normalizeUzPhone(body?.phone ?? "");
  if (!phone) return jsonError("Telefon raqamni to'g'ri kiriting: +998 XX XXX XX XX");

  const existing = await db.user.findUnique({ where: { phone }, select: { role: true } });
  if (existing && existing.role !== "CLIENT") return jsonError("Bu raqam mijoz hisobiga tegishli emas");

  const last = await db.otpCode.findFirst({ where: { phone }, orderBy: { createdAt: "desc" } });
  if (last && Date.now() - last.createdAt.getTime() < RESEND_SECONDS * 1000) {
    const wait = Math.ceil(RESEND_SECONDS - (Date.now() - last.createdAt.getTime()) / 1000);
    return jsonError(`Kodni qayta olish uchun ${wait} soniya kuting`, 429);
  }
  const today = await db.otpCode.count({
    where: { phone, createdAt: { gt: new Date(Date.now() - 24 * 60 * 60 * 1000) } },
  });
  if (today >= MAX_CODES_PER_DAY) return jsonError("Bugungi limit tugadi. Ertaga qayta urinib ko'ring.", 429);

  const code = String(randomInt(100000, 1000000));
  const otp = await db.otpCode.create({
    data: { phone, codeHash: hashOtp(phone, code), expiresAt: new Date(Date.now() + CODE_TTL_MINUTES * 60 * 1000) },
  });

  try {
    const { delivered } = await sendLoginCode(phone, code);
    return NextResponse.json({
      ok: true,
      phone,
      resendIn: RESEND_SECONDS,
      // Faqat lokal sinovda (SMS sozlanmagan bo'lsa) kod javobda qaytariladi
      devCode: !delivered && devLoginEnabled() ? code : undefined,
    });
  } catch (error) {
    console.error(error);
    // SMS ketmagan bo'lsa, bu urinish limitga hisoblanmasin
    await db.otpCode.delete({ where: { id: otp.id } }).catch(() => undefined);
    return jsonError("SMS yuborib bo'lmadi. Birozdan keyin qayta urinib ko'ring.", 502);
  }
}
