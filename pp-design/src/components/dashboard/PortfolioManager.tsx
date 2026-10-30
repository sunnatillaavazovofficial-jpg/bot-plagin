"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { addPortfolioItem, deletePortfolioItem, movePortfolioItem } from "@/app/actions/designer";
import { ImageUpload } from "@/components/ui/ImageUpload";
import { Toast, type ToastState } from "@/components/ui/Toast";

type Item = { id: string; title: string; description: string; imageUrl: string };

export function PortfolioManager({ items, disabled }: { items: Item[]; disabled?: boolean }) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [toast, setToast] = useState<ToastState>(null);
  const [pending, startTransition] = useTransition();
  const router = useRouter();

  const run = (fn: () => Promise<{ ok: boolean; message?: string; error?: string }>, after?: () => void) =>
    startTransition(async () => {
      const res = await fn();
      setToast(res.ok ? (res.message ? { ok: true, text: res.message } : null) : { ok: false, text: res.error ?? "Xatolik" });
      if (res.ok) {
        after?.();
        router.refresh();
      }
    });

  return (
    <div className="space-y-6">
      <form
        className="glass grid gap-4 rounded-2xl p-5 sm:grid-cols-[auto_1fr]"
        onSubmit={(e) => {
          e.preventDefault();
          if (!imageUrl) return setToast({ ok: false, text: "Avval rasm yuklang" });
          run(
            () => addPortfolioItem({ title, description, imageUrl }),
            () => {
              setImageUrl(null);
              setTitle("");
              setDescription("");
            },
          );
        }}
      >
        <ImageUpload kind="portfolio" value={imageUrl} onChange={setImageUrl} previewClass="h-36 w-48 rounded-xl" label="Ish rasmi" />
        <fieldset disabled={disabled || pending} className="space-y-3">
          <input className="input" value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Ish nomi (masalan: «Mevazor» logotipi)" maxLength={100} required />
          <textarea className="input min-h-20" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Qisqacha tavsif (ixtiyoriy)" maxLength={500} />
          <button className="btn-primary" disabled={!imageUrl || !title.trim()}>
            + Portfolioga qo&apos;shish
          </button>
        </fieldset>
      </form>
      <Toast state={toast} />

      {items.length ? (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((item, i) => (
            <li key={item.id} className="glass overflow-hidden rounded-2xl">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={item.imageUrl} alt={item.title} className="aspect-[4/3] w-full object-cover" />
              <div className="p-4">
                <p className="truncate font-semibold">{item.title}</p>
                {item.description && <p className="mt-1 line-clamp-2 text-sm text-mist/70">{item.description}</p>}
                <div className="mt-3 flex gap-2">
                  <button className="btn-ghost !px-3 !py-1.5 text-xs" disabled={disabled || pending || i === 0} onClick={() => run(() => movePortfolioItem(item.id, -1))} aria-label="Oldinga">
                    ←
                  </button>
                  <button className="btn-ghost !px-3 !py-1.5 text-xs" disabled={disabled || pending || i === items.length - 1} onClick={() => run(() => movePortfolioItem(item.id, 1))} aria-label="Orqaga">
                    →
                  </button>
                  <button
                    className="btn-danger ml-auto !px-3 !py-1.5 text-xs"
                    disabled={disabled || pending}
                    onClick={() => confirm("Bu ishni o'chirasizmi?") && run(() => deletePortfolioItem(item.id))}
                  >
                    O&apos;chirish
                  </button>
                </div>
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-mist/60">Hali ish qo&apos;shilmagan. Eng yaxshi ishlaringizni yuklang — mijozlar aynan shularni ko&apos;radi.</p>
      )}
    </div>
  );
}
