"use client";

import { useRef, useState } from "react";
import { cn } from "@/lib/utils";

type Kind = "avatar" | "portfolio" | "project" | "partner" | "logo" | "video";

export async function uploadFile(file: File, kind: Kind): Promise<string> {
  const form = new FormData();
  form.set("file", file);
  form.set("kind", kind);
  const res = await fetch("/api/upload", { method: "POST", body: form });
  const json = await res.json().catch(() => ({}));
  if (!res.ok || !json.ok) throw new Error(json.error || "Yuklab bo'lmadi");
  return json.url as string;
}

/** Rasm (yoki video) tanlash va darhol serverga yuklash */
export function ImageUpload({
  kind,
  value,
  onChange,
  className,
  previewClass = "h-32 w-32 rounded-2xl",
  accept = "image/jpeg,image/png,image/webp,image/gif",
  label = "Rasm yuklash",
}: {
  kind: Kind;
  value: string | null;
  onChange: (url: string | null) => void;
  className?: string;
  previewClass?: string;
  accept?: string;
  label?: string;
}) {
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const isVideo = kind === "video";

  const pick = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    setError("");
    try {
      onChange(await uploadFile(file, kind));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Xatolik");
    } finally {
      setBusy(false);
      if (input.current) input.current.value = "";
    }
  };

  return (
    <div className={cn("flex flex-wrap items-center gap-4", className)}>
      <button
        type="button"
        onClick={() => input.current?.click()}
        className={cn(
          "relative flex shrink-0 items-center justify-center overflow-hidden border border-dashed border-white/20 bg-ink-950/50 text-xs text-muted transition hover:border-brand/60",
          previewClass,
        )}
      >
        {value ? (
          isVideo ? (
            <video src={value} className="h-full w-full object-cover" muted />
          ) : (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={value} alt="" className="h-full w-full object-cover" />
          )
        ) : (
          <span className="px-2 text-center">+ {label}</span>
        )}
        {busy && <span className="absolute inset-0 flex items-center justify-center bg-ink-950/70 text-sm text-white">Yuklanmoqda…</span>}
      </button>
      <div className="flex flex-col gap-2">
        <button type="button" className="btn-ghost !py-2 text-xs" onClick={() => input.current?.click()} disabled={busy}>
          {value ? "Almashtirish" : label}
        </button>
        {value && (
          <button type="button" className="text-left text-xs text-rose-300 hover:underline" onClick={() => onChange(null)}>
            Olib tashlash
          </button>
        )}
        {error && <p className="max-w-[220px] text-xs text-rose-300">{error}</p>}
      </div>
      <input ref={input} type="file" accept={isVideo ? "video/mp4,video/webm" : accept} className="hidden" onChange={(e) => pick(e.target.files?.[0])} />
    </div>
  );
}
