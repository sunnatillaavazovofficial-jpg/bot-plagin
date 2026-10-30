"use client";

import { useEffect, useMemo, useState } from "react";
import { cn } from "@/lib/utils";

export type Work = { id: string; title: string; category: string; description: string; imageUrl: string };

/** Asosiy ishlar galereyasi: kategoriya bo'yicha filtr va kattalashtirib ko'rish */
export function WorksGallery({ works }: { works: Work[] }) {
  const categories = useMemo(() => ["Barchasi", ...Array.from(new Set(works.map((w) => w.category).filter(Boolean)))], [works]);
  const [active, setActive] = useState("Barchasi");
  const [open, setOpen] = useState<number | null>(null);
  const shown = active === "Barchasi" ? works : works.filter((w) => w.category === active);

  useEffect(() => {
    if (open === null) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(null);
      if (e.key === "ArrowRight") setOpen((i) => (i === null ? i : (i + 1) % shown.length));
      if (e.key === "ArrowLeft") setOpen((i) => (i === null ? i : (i - 1 + shown.length) % shown.length));
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, shown.length]);

  if (!works.length) return <p className="mt-10 text-mist/70">Ishlar tez orada qo&apos;shiladi.</p>;
  const current = open !== null ? shown[open] : null;

  return (
    <>
      {categories.length > 2 && (
        <div className="mt-8 flex flex-wrap gap-2">
          {categories.map((cat) => (
            <button
              key={cat}
              onClick={() => setActive(cat)}
              className={cn(
                "rounded-full border px-4 py-1.5 text-sm transition",
                active === cat ? "border-brand/60 bg-brand/15 text-white" : "border-white/10 text-mist/75 hover:border-white/25 hover:text-white",
              )}
            >
              {cat}
            </button>
          ))}
        </div>
      )}

      <div className="mt-8 columns-2 gap-3 sm:gap-4 lg:columns-3">
        {shown.map((work, i) => (
          <button
            key={work.id}
            onClick={() => setOpen(i)}
            className="group relative mb-3 block w-full sm:mb-4 overflow-hidden rounded-2xl border border-white/10 text-left break-inside-avoid"
          >
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={work.imageUrl} alt={work.title} className="w-full transition duration-700 group-hover:scale-105" loading="lazy" decoding="async" />
            <div className="absolute inset-0 flex flex-col justify-end bg-gradient-to-t from-ink-950/90 via-ink-950/10 to-transparent p-3 sm:p-5 opacity-90 transition group-hover:opacity-100">
              {work.category && <span className="mb-1 text-[10px] font-semibold tracking-widest text-brand uppercase sm:text-xs">{work.category}</span>}
              <span className="font-display text-xs leading-snug font-semibold sm:text-lg">{work.title}</span>
            </div>
          </button>
        ))}
      </div>

      {current && (
        <div className="fixed inset-0 z-[90] flex items-center justify-center bg-ink-950/90 p-4 backdrop-blur" onClick={() => setOpen(null)} role="dialog" aria-modal>
          <div className="relative max-h-full w-full max-w-5xl" onClick={(e) => e.stopPropagation()}>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={current.imageUrl} alt={current.title} className="mx-auto max-h-[78vh] rounded-2xl object-contain" />
            <div className="mt-4 text-center">
              <p className="font-display text-xl font-semibold">{current.title}</p>
              {current.description && <p className="mt-1 text-mist/75">{current.description}</p>}
            </div>
            <button className="btn-ghost absolute -top-2 right-0 h-10 w-10 !p-0" onClick={() => setOpen(null)} aria-label="Yopish">
              ✕
            </button>
            {shown.length > 1 && (
              <>
                <button className="btn-ghost absolute top-1/2 left-0 h-11 w-11 -translate-y-1/2 !p-0" onClick={() => setOpen((open! - 1 + shown.length) % shown.length)} aria-label="Oldingi">
                  ←
                </button>
                <button className="btn-ghost absolute top-1/2 right-0 h-11 w-11 -translate-y-1/2 !p-0" onClick={() => setOpen((open! + 1) % shown.length)} aria-label="Keyingi">
                  →
                </button>
              </>
            )}
          </div>
        </div>
      )}
    </>
  );
}
