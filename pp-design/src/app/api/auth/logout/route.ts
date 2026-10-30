import { NextResponse } from "next/server";
import { isSameOrigin, jsonError } from "@/lib/http";
import { destroySession } from "@/lib/session";

export async function POST(request: Request) {
  if (!isSameOrigin(request)) return jsonError("Ruxsat yo'q", 403);
  await destroySession();
  return NextResponse.json({ ok: true });
}
