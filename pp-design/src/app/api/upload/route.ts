import { NextResponse } from "next/server";
import { getCurrentUser } from "@/lib/auth";
import { isSameOrigin, jsonError, rateLimit } from "@/lib/http";
import { UploadError, saveUpload, type UploadKind } from "@/lib/upload";

const DESIGNER_KINDS: UploadKind[] = ["avatar", "portfolio"];
const ADMIN_KINDS: UploadKind[] = ["avatar", "portfolio", "project", "partner", "logo", "video"];

/** Rasm/video yuklash. Dizayner — avatar va portfolio; admin — hammasi. */
export async function POST(request: Request) {
  if (!isSameOrigin(request)) return jsonError("Ruxsat yo'q", 403);
  const user = await getCurrentUser();
  if (!user || (user.role !== "DESIGNER" && user.role !== "ADMIN")) return jsonError("Avval tizimga kiring", 401);
  if (user.role === "DESIGNER" && user.designer?.status === "BLOCKED") return jsonError("Profil bloklangan", 403);
  if (!rateLimit(`upload:${user.id}`, 60, 60 * 60 * 1000)) return jsonError("Juda ko'p yuklash. Keyinroq urinib ko'ring.", 429);

  const form = await request.formData().catch(() => null);
  const file = form?.get("file");
  const kind = String(form?.get("kind") ?? "") as UploadKind;
  const allowed = user.role === "ADMIN" ? ADMIN_KINDS : DESIGNER_KINDS;
  if (!allowed.includes(kind)) return jsonError("Bu turdagi faylni yuklash mumkin emas", 403);
  if (!(file instanceof File)) return jsonError("Fayl tanlanmagan");

  try {
    const url = await saveUpload(file, kind);
    return NextResponse.json({ ok: true, url });
  } catch (error) {
    if (error instanceof UploadError) return jsonError(error.message);
    console.error(error);
    return jsonError("Faylni saqlab bo'lmadi", 500);
  }
}
