"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { updateProfile, type ProfileInput } from "@/app/actions/designer";
import { ImageUpload } from "@/components/ui/ImageUpload";
import { TagInput } from "@/components/ui/TagInput";
import { Toast, type ToastState } from "@/components/ui/Toast";

export function ProfileForm({ initial, disabled }: { initial: ProfileInput; disabled?: boolean }) {
  const [form, setForm] = useState<ProfileInput>(initial);
  const [toast, setToast] = useState<ToastState>(null);
  const [pending, startTransition] = useTransition();
  const router = useRouter();
  const set = <K extends keyof ProfileInput>(key: K, value: ProfileInput[K]) => setForm((f) => ({ ...f, [key]: value }));
  const setSocial = (key: keyof ProfileInput["socials"], value: string) => setForm((f) => ({ ...f, socials: { ...f.socials, [key]: value } }));

  const save = (e: React.FormEvent) => {
    e.preventDefault();
    startTransition(async () => {
      const res = await updateProfile({ ...form, prices: form.prices.filter((p) => p.title.trim()) });
      setToast(res.ok ? { ok: true, text: res.message ?? "Saqlandi" } : { ok: false, text: res.error });
      if (res.ok) router.refresh();
    });
  };

  return (
    <form onSubmit={save} className="space-y-8">
      <fieldset disabled={disabled || pending} className="space-y-8 disabled:opacity-60">
        <div>
          <p className="label">Profil rasmi</p>
          <ImageUpload kind="avatar" value={form.avatarUrl} onChange={(url) => set("avatarUrl", url)} previewClass="h-28 w-28 rounded-full" label="Rasm tanlash" />
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <div>
            <label className="label" htmlFor="fullName">Ism familiya</label>
            <input id="fullName" className="input" value={form.fullName} onChange={(e) => set("fullName", e.target.value)} required maxLength={60} />
          </div>
          <div>
            <label className="label" htmlFor="username">Profil manzili</label>
            <div className="flex">
              <span className="flex items-center rounded-l-xl border border-r-0 border-white/10 bg-white/5 px-3 text-xs text-muted">/designer/</span>
              <input
                id="username"
                className="input rounded-l-none"
                value={form.username}
                onChange={(e) => set("username", e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, ""))}
                required
                maxLength={30}
              />
            </div>
          </div>
          <div>
            <label className="label" htmlFor="specialty">Mutaxassislik</label>
            <input id="specialty" className="input" value={form.specialty} onChange={(e) => set("specialty", e.target.value)} placeholder="Masalan: Brending va logotip dizayneri" maxLength={80} />
          </div>
          <div>
            <label className="label" htmlFor="years">Tajriba (yil)</label>
            <input id="years" type="number" min={0} max={60} className="input" value={form.experienceYears} onChange={(e) => set("experienceYears", Number(e.target.value) || 0)} />
          </div>
        </div>

        <div>
          <label className="label" htmlFor="bio">O&apos;zingiz haqida</label>
          <textarea id="bio" className="input min-h-28" value={form.bio} onChange={(e) => set("bio", e.target.value)} maxLength={1200} placeholder="Qanday ishlar qilasiz, uslubingiz, kimlar bilan ishlagansiz…" />
        </div>
        <div>
          <label className="label" htmlFor="experience">Tajriba haqida</label>
          <textarea id="experience" className="input min-h-24" value={form.experience} onChange={(e) => set("experience", e.target.value)} maxLength={2000} placeholder="Qayerlarda ishlagansiz, qanday loyihalarda qatnashgansiz" />
        </div>
        <div>
          <p className="label">Ko&apos;nikmalar</p>
          <TagInput value={form.skills} onChange={(v) => set("skills", v)} placeholder="Figma, Logotip, 3D… (Enter bosing)" />
        </div>

        <div>
          <p className="label">Narxlar</p>
          <div className="space-y-2">
            {form.prices.map((item, i) => (
              <div key={i} className="flex gap-2">
                <input
                  className="input"
                  value={item.title}
                  placeholder="Xizmat nomi"
                  onChange={(e) => set("prices", form.prices.map((p, k) => (k === i ? { ...p, title: e.target.value } : p)))}
                />
                <input
                  className="input max-w-[200px]"
                  value={item.price}
                  placeholder="Narxi"
                  onChange={(e) => set("prices", form.prices.map((p, k) => (k === i ? { ...p, price: e.target.value } : p)))}
                />
                <button type="button" className="btn-danger !px-3" onClick={() => set("prices", form.prices.filter((_, k) => k !== i))} aria-label="O'chirish">
                  ✕
                </button>
              </div>
            ))}
            <button type="button" className="btn-ghost !py-2 text-xs" onClick={() => set("prices", [...form.prices, { title: "", price: "" }])} disabled={form.prices.length >= 15}>
              + Narx qo&apos;shish
            </button>
          </div>
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <div>
            <label className="label" htmlFor="contactPhone">Telefon (mijozlar uchun)</label>
            <input id="contactPhone" className="input" value={form.contactPhone} onChange={(e) => set("contactPhone", e.target.value)} placeholder="+998 90 123 45 67" inputMode="tel" />
          </div>
          <div>
            <label className="label" htmlFor="contactEmail">Email</label>
            <input id="contactEmail" type="email" className="input" value={form.contactEmail} onChange={(e) => set("contactEmail", e.target.value)} />
          </div>
          {(
            [
              ["telegram", "Telegram", "@username"],
              ["instagram", "Instagram", "@username"],
              ["behance", "Behance", "username"],
              ["dribbble", "Dribbble", "username"],
              ["website", "Veb-sayt", "https://…"],
            ] as const
          ).map(([key, label, ph]) => (
            <div key={key}>
              <label className="label" htmlFor={key}>{label}</label>
              <input id={key} className="input" value={form.socials[key] ?? ""} onChange={(e) => setSocial(key, e.target.value)} placeholder={ph} />
            </div>
          ))}
        </div>
      </fieldset>

      <div className="sticky bottom-4 z-10 flex flex-wrap items-center gap-4 rounded-2xl border border-white/10 bg-ink-900/90 p-3 backdrop-blur">
        <button className="btn-primary px-8" disabled={disabled || pending}>
          {pending ? "Saqlanmoqda…" : "Saqlash"}
        </button>
        <Toast state={toast} />
      </div>
    </form>
  );
}
