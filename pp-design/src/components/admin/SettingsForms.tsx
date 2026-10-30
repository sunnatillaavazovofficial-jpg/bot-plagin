"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { adminSaveSettings } from "@/app/actions/admin";
import { ImageUpload } from "@/components/ui/ImageUpload";
import { Toast, type ToastState } from "@/components/ui/Toast";
import type { SiteSettings } from "@/lib/settings";

/** Sozlamalar bo'limini tahrirlash uchun umumiy qobiq */
function useSection<K extends keyof SiteSettings>(section: K, initial: SiteSettings[K]) {
  const [value, setValue] = useState(initial);
  const [toast, setToast] = useState<ToastState>(null);
  const [pending, startTransition] = useTransition();
  const router = useRouter();
  const save = (e?: React.FormEvent) => {
    e?.preventDefault();
    startTransition(async () => {
      const res = await adminSaveSettings(section, value);
      setToast(res.ok ? { ok: true, text: res.message ?? "Saqlandi" } : { ok: false, text: res.error });
      if (res.ok) router.refresh();
    });
  };
  return { value, setValue, toast, pending, save };
}

function Card({ title, description, children, onSubmit, pending, toast }: {
  title: string;
  description?: string;
  children: React.ReactNode;
  onSubmit: (e: React.FormEvent) => void;
  pending: boolean;
  toast: ToastState;
}) {
  return (
    <form onSubmit={onSubmit} className="glass space-y-5 rounded-3xl p-5 sm:p-7">
      <div>
        <h2 className="font-display text-lg font-semibold">{title}</h2>
        {description && <p className="mt-1 text-sm text-mist/65">{description}</p>}
      </div>
      {children}
      <div className="flex flex-wrap items-center gap-4 pt-1">
        <button className="btn-primary px-7" disabled={pending}>
          {pending ? "Saqlanmoqda…" : "Saqlash"}
        </button>
        <Toast state={toast} />
      </div>
    </form>
  );
}

function Field({ label, children, hint }: { label: string; children: React.ReactNode; hint?: string }) {
  return (
    <label className="block">
      <span className="label">{label}</span>
      {children}
      {hint && <span className="mt-1 block text-xs text-muted">{hint}</span>}
    </label>
  );
}

export function BrandForm({ initial }: { initial: SiteSettings["brand"] }) {
  const { value, setValue, toast, pending, save } = useSection("brand", initial);
  return (
    <Card title="Logo va nom" description="Logo yuklasangiz, saytning yuqori qismi va pastida ko'rinadi. Logo ranglarini saytga moslash uchun README'ga qarang." onSubmit={save} pending={pending} toast={toast}>
      <div>
        <span className="label">Logo (PNG/SVG, shaffof fon tavsiya etiladi)</span>
        <ImageUpload
          kind="logo"
          value={value.logoUrl}
          onChange={(url) => setValue({ ...value, logoUrl: url })}
          previewClass="h-24 w-40 rounded-xl"
          accept="image/png,image/svg+xml,image/webp,image/jpeg"
          label="Logo yuklash"
        />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Kompaniya nomi">
          <input className="input" value={value.name} onChange={(e) => setValue({ ...value, name: e.target.value })} maxLength={40} required />
        </Field>
        <Field label="Shior (footer)">
          <input className="input" value={value.tagline} onChange={(e) => setValue({ ...value, tagline: e.target.value })} maxLength={160} />
        </Field>
      </div>
    </Card>
  );
}

export function HeroForm({ initial }: { initial: SiteSettings["hero"] }) {
  const { value, setValue, toast, pending, save } = useSection("hero", initial);
  return (
    <Card title="Bosh ekran (robot yonidagi matnlar)" description="Robot yonida shu gaplar navbat bilan yozilib turadi." onSubmit={save} pending={pending} toast={toast}>
      <Field label="Yorliq (badge)">
        <input className="input" value={value.badge} onChange={(e) => setValue({ ...value, badge: e.target.value })} maxLength={60} />
      </Field>
      <div>
        <span className="label">Yozilib turadigan gaplar</span>
        <div className="space-y-2">
          {value.lines.map((line, i) => (
            <div key={i} className="flex gap-2">
              <input
                className="input"
                value={line}
                maxLength={90}
                onChange={(e) => setValue({ ...value, lines: value.lines.map((l, k) => (k === i ? e.target.value : l)) })}
              />
              <button type="button" className="btn-danger !px-3" onClick={() => setValue({ ...value, lines: value.lines.filter((_, k) => k !== i) })} disabled={value.lines.length <= 1}>
                ✕
              </button>
            </div>
          ))}
          <button type="button" className="btn-ghost !py-2 text-xs" onClick={() => setValue({ ...value, lines: [...value.lines, ""] })} disabled={value.lines.length >= 8}>
            + Gap qo&apos;shish
          </button>
        </div>
      </div>
      <Field label="Qisqa tavsif">
        <textarea className="input min-h-24" value={value.subtitle} onChange={(e) => setValue({ ...value, subtitle: e.target.value })} maxLength={400} />
      </Field>
    </Card>
  );
}

export function AboutForm({ initial }: { initial: SiteSettings["about"] }) {
  const { value, setValue, toast, pending, save } = useSection("about", initial);
  return (
    <Card title="Kompaniya haqida" onSubmit={save} pending={pending} toast={toast}>
      <Field label="Sarlavha">
        <input className="input" value={value.title} onChange={(e) => setValue({ ...value, title: e.target.value })} maxLength={80} />
      </Field>
      <Field label="Matn">
        <textarea className="input min-h-36" value={value.text} onChange={(e) => setValue({ ...value, text: e.target.value })} maxLength={2000} />
      </Field>
      <div>
        <span className="label">Raqamlar (masalan: 8+ / yillik tajriba)</span>
        <div className="grid gap-2 sm:grid-cols-2">
          {value.stats.map((stat, i) => (
            <div key={i} className="flex gap-2">
              <input className="input max-w-[110px]" value={stat.value} maxLength={12} onChange={(e) => setValue({ ...value, stats: value.stats.map((s, k) => (k === i ? { ...s, value: e.target.value } : s)) })} />
              <input className="input" value={stat.label} maxLength={40} onChange={(e) => setValue({ ...value, stats: value.stats.map((s, k) => (k === i ? { ...s, label: e.target.value } : s)) })} />
              <button type="button" className="btn-danger !px-3" onClick={() => setValue({ ...value, stats: value.stats.filter((_, k) => k !== i) })}>
                ✕
              </button>
            </div>
          ))}
        </div>
        <button type="button" className="btn-ghost mt-2 !py-2 text-xs" onClick={() => setValue({ ...value, stats: [...value.stats, { value: "", label: "" }] })} disabled={value.stats.length >= 6}>
          + Raqam qo&apos;shish
        </button>
      </div>
      <div>
        <span className="label">Xizmatlar</span>
        <div className="space-y-3">
          {value.services.map((service, i) => (
            <div key={i} className="grid gap-2 rounded-2xl border border-white/5 p-3 sm:grid-cols-[220px_1fr_auto]">
              <input className="input" value={service.title} placeholder="Nomi" maxLength={60} onChange={(e) => setValue({ ...value, services: value.services.map((s, k) => (k === i ? { ...s, title: e.target.value } : s)) })} />
              <input className="input" value={service.text} placeholder="Qisqa tavsif" maxLength={240} onChange={(e) => setValue({ ...value, services: value.services.map((s, k) => (k === i ? { ...s, text: e.target.value } : s)) })} />
              <button type="button" className="btn-danger !px-3" onClick={() => setValue({ ...value, services: value.services.filter((_, k) => k !== i) })}>
                ✕
              </button>
            </div>
          ))}
        </div>
        <button type="button" className="btn-ghost mt-2 !py-2 text-xs" onClick={() => setValue({ ...value, services: [...value.services, { title: "", text: "" }] })} disabled={value.services.length >= 9}>
          + Xizmat qo&apos;shish
        </button>
      </div>
    </Card>
  );
}

export function SeoForm({ initial }: { initial: SiteSettings["seo"] }) {
  const { value, setValue, toast, pending, save } = useSection("seo", initial);
  return (
    <Card title="SEO" description="Google va ijtimoiy tarmoqlarda sayt havolasi ostida chiqadigan tavsif (150–160 belgi tavsiya etiladi)." onSubmit={save} pending={pending} toast={toast}>
      <Field label={`Tavsif (${value.description.length}/300)`}>
        <textarea className="input min-h-24" value={value.description} onChange={(e) => setValue({ description: e.target.value })} maxLength={300} />
      </Field>
    </Card>
  );
}

export function ContactForm({ initial }: { initial: SiteSettings["contact"] }) {
  const { value, setValue, toast, pending, save } = useSection("contact", initial);
  const set = <K extends keyof SiteSettings["contact"]>(key: K, v: SiteSettings["contact"][K]) => setValue({ ...value, [key]: v });
  return (
    <Card title="Aloqa va lokatsiya" description="Bosh sahifaning eng pastidagi «Aloqa» bo'limi." onSubmit={save} pending={pending} toast={toast}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Telefon">
          <input className="input" value={value.phone} onChange={(e) => set("phone", e.target.value)} placeholder="+998 90 000 00 00" />
        </Field>
        <Field label="Qo'shimcha telefon">
          <input className="input" value={value.phone2} onChange={(e) => set("phone2", e.target.value)} />
        </Field>
        <Field label="Telegram (username)">
          <input className="input" value={value.telegram} onChange={(e) => set("telegram", e.target.value)} placeholder="ppdesign" />
        </Field>
        <Field label="Instagram (username)">
          <input className="input" value={value.instagram} onChange={(e) => set("instagram", e.target.value)} />
        </Field>
        <Field label="Email">
          <input className="input" type="email" value={value.email} onChange={(e) => set("email", e.target.value)} />
        </Field>
        <Field label="Ish vaqti">
          <input className="input" value={value.workHours} onChange={(e) => set("workHours", e.target.value)} />
        </Field>
      </div>
      <Field label="Manzil">
        <input className="input" value={value.address} onChange={(e) => set("address", e.target.value)} />
      </Field>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Kenglik (latitude)" hint="Google/Yandex xaritada joyni bosib, koordinatani nusxalang">
          <input className="input" type="number" step="any" value={value.lat} onChange={(e) => set("lat", Number(e.target.value))} />
        </Field>
        <Field label="Uzunlik (longitude)">
          <input className="input" type="number" step="any" value={value.lng} onChange={(e) => set("lng", Number(e.target.value))} />
        </Field>
        <Field label="Xarita">
          <select className="input" value={value.mapProvider} onChange={(e) => set("mapProvider", e.target.value as "yandex" | "google")}>
            <option value="yandex">Yandex</option>
            <option value="google">Google</option>
          </select>
        </Field>
      </div>
      <Field label="Ikkala koordinatani birga qo'yish (masalan: 41.311081, 69.279737)">
        <input
          className="input"
          placeholder="41.311081, 69.279737"
          onChange={(e) => {
            const m = e.target.value.match(/(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)/);
            if (m) setValue({ ...value, lat: Number(m[1]), lng: Number(m[2]) });
          }}
        />
      </Field>
    </Card>
  );
}

export function IntroForm({ initial }: { initial: SiteSettings["intro"] }) {
  const { value, setValue, toast, pending, save } = useSection("intro", initial);
  return (
    <Card
      title="Intro (dizayn tarixi)"
      description="Sayt ochilganda chiqadigan ~10 soniyalik intro. Video yuklamasangiz, kodda chizilgan animatsiya (ovozi bilan) ko'rsatiladi. Video yuklasangiz — o'sha video o'ynaydi."
      onSubmit={save}
      pending={pending}
      toast={toast}
    >
      <label className="flex items-center gap-3">
        <input type="checkbox" className="h-5 w-5 accent-[var(--color-brand)]" checked={value.enabled} onChange={(e) => setValue({ ...value, enabled: e.target.checked })} />
        <span>Introni ko&apos;rsatish</span>
      </label>
      <div>
        <span className="label">Tayyor video (MP4/WebM, 80 MB gacha, ixtiyoriy)</span>
        <ImageUpload kind="video" value={value.videoUrl} onChange={(url) => setValue({ ...value, videoUrl: url })} previewClass="h-32 w-56 rounded-xl" label="Video yuklash" />
      </div>
      <a href="/?intro=1" target="_blank" className="inline-block text-sm text-brand hover:underline">
        Introni ko&apos;rib chiqish →
      </a>
    </Card>
  );
}
