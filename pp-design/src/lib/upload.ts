import { randomBytes } from "node:crypto";
import { mkdir, unlink, writeFile } from "node:fs/promises";
import path from "node:path";
import sharp, { type ResizeOptions } from "sharp";
import { uploadDir } from "./env";

export type UploadKind = "avatar" | "portfolio" | "project" | "partner" | "logo" | "video";

const IMAGE_TYPES = new Set(["image/jpeg", "image/png", "image/webp", "image/gif", "image/avif"]);
const VIDEO_TYPES = new Set(["video/mp4", "video/webm"]);
const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
const MAX_VIDEO_BYTES = 80 * 1024 * 1024;

/** Har bir tur uchun rasm o'lchami (webp ga o'giriladi) */
const RESIZE: Record<Exclude<UploadKind, "video">, ResizeOptions> = {
  avatar: { width: 512, height: 512, fit: "cover" },
  portfolio: { width: 1600, height: 1600, fit: "inside", withoutEnlargement: true },
  project: { width: 1600, height: 1600, fit: "inside", withoutEnlargement: true },
  partner: { width: 480, height: 240, fit: "inside", withoutEnlargement: true },
  logo: { width: 512, height: 512, fit: "inside", withoutEnlargement: true },
};

export class UploadError extends Error {}

function isSafeSvg(text: string): boolean {
  const head = text.slice(0, 500).toLowerCase();
  if (!head.includes("<svg")) return false;
  return !/<script|javascript:|\son\w+\s*=|<foreignobject|<iframe|<embed|<object/i.test(text);
}

async function store(kind: UploadKind, ext: string, data: Buffer): Promise<string> {
  const month = new Date().toISOString().slice(0, 7);
  const relDir = path.posix.join(kind, month);
  const name = `${randomBytes(12).toString("hex")}.${ext}`;
  const absDir = path.resolve(/*turbopackIgnore: true*/ uploadDir(), relDir);
  await mkdir(absDir, { recursive: true });
  await writeFile(path.join(/*turbopackIgnore: true*/ absDir, name), data);
  return `/uploads/${relDir}/${name}`;
}

/** Faylni tekshiradi, kerak bo'lsa siqadi va saqlaydi. Natija — /uploads/... URL. */
export async function saveUpload(file: File, kind: UploadKind): Promise<string> {
  if (!file || typeof file.arrayBuffer !== "function" || file.size === 0) throw new UploadError("Fayl tanlanmagan");

  if (kind === "video") {
    if (!VIDEO_TYPES.has(file.type)) throw new UploadError("Faqat MP4 yoki WebM video yuklash mumkin");
    if (file.size > MAX_VIDEO_BYTES) throw new UploadError("Video hajmi 80 MB dan oshmasin");
    return store(kind, file.type === "video/webm" ? "webm" : "mp4", Buffer.from(await file.arrayBuffer()));
  }

  if (file.size > MAX_IMAGE_BYTES) throw new UploadError("Rasm hajmi 10 MB dan oshmasin");
  const buffer = Buffer.from(await file.arrayBuffer());

  // Logotiplar uchun SVG ham qabul qilinadi (faqat admin yuklaydi), xavfli teglar bo'lsa rad etiladi
  if (file.type === "image/svg+xml" && (kind === "partner" || kind === "logo")) {
    if (!isSafeSvg(buffer.toString("utf8"))) throw new UploadError("SVG fayl xavfsiz emas");
    return store(kind, "svg", buffer);
  }

  if (!IMAGE_TYPES.has(file.type)) throw new UploadError("Faqat JPG, PNG, WEBP yoki GIF rasm yuklash mumkin");

  try {
    const output = await sharp(buffer, { animated: false })
      .rotate()
      .resize(RESIZE[kind])
      .webp({ quality: kind === "avatar" ? 85 : 82 })
      .toBuffer();
    return store(kind, "webp", output);
  } catch {
    throw new UploadError("Rasmni o'qib bo'lmadi");
  }
}

/** Eski faylni o'chiradi (faqat bizning /uploads ichidagi fayllar). */
export async function removeUpload(url: string | null | undefined): Promise<void> {
  if (!url || !url.startsWith("/uploads/")) return;
  const root = path.resolve(/*turbopackIgnore: true*/ uploadDir());
  const target = path.resolve(/*turbopackIgnore: true*/ root, url.slice("/uploads/".length));
  if (!target.startsWith(root + path.sep)) return;
  await unlink(target).catch(() => undefined);
}
