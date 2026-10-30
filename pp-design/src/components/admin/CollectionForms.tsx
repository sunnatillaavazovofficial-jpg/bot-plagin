"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { adminAddPartner, adminAddProject } from "@/app/actions/admin";
import { ImageUpload } from "@/components/ui/ImageUpload";
import { Toast, type ToastState } from "@/components/ui/Toast";

export function ProjectForm({ categories }: { categories: string[] }) {
  const [form, setForm] = useState({ title: "", category: "", description: "", imageUrl: null as string | null });
  const [toast, setToast] = useState<ToastState>(null);
  const [pending, startTransition] = useTransition();
  const router = useRouter();
  return (
    <form
      className="glass grid gap-5 rounded-3xl p-5 sm:grid-cols-[auto_1fr] sm:p-7"
      onSubmit={(e) => {
        e.preventDefault();
        if (!form.imageUrl) return setToast({ ok: false, text: "Avval rasm yuklang" });
        startTransition(async () => {
          const res = await adminAddProject({ ...form, imageUrl: form.imageUrl! });
          setToast(res.ok ? { ok: true, text: res.message ?? "Qo'shildi" } : { ok: false, text: res.error });
          if (res.ok) {
            setForm({ title: "", category: form.category, description: "", imageUrl: null });
            router.refresh();
          }
        });
      }}
    >
      <ImageUpload kind="project" value={form.imageUrl} onChange={(url) => setForm({ ...form, imageUrl: url })} previewClass="h-40 w-56 rounded-xl" label="Rasm" />
      <div className="space-y-3">
        <input className="input" placeholder="Nomi (masalan: «Mevazor» — brending)" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} maxLength={120} required />
        <input className="input" placeholder="Kategoriya (Brending, Reklama, Veb…)" list="project-categories" value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })} maxLength={40} />
        <datalist id="project-categories">
          {categories.map((c) => (
            <option key={c} value={c} />
          ))}
        </datalist>
        <textarea className="input min-h-20" placeholder="Qisqa tavsif (ixtiyoriy)" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} maxLength={500} />
        <div className="flex flex-wrap items-center gap-4">
          <button className="btn-primary" disabled={pending}>
            + Ish qo&apos;shish
          </button>
          <Toast state={toast} />
        </div>
      </div>
    </form>
  );
}

export function PartnerForm() {
  const [form, setForm] = useState({ name: "", url: "", logoUrl: null as string | null });
  const [toast, setToast] = useState<ToastState>(null);
  const [pending, startTransition] = useTransition();
  const router = useRouter();
  return (
    <form
      className="glass grid gap-5 rounded-3xl p-5 sm:grid-cols-[auto_1fr] sm:p-7"
      onSubmit={(e) => {
        e.preventDefault();
        if (!form.logoUrl) return setToast({ ok: false, text: "Avval logo yuklang" });
        startTransition(async () => {
          const res = await adminAddPartner({ ...form, logoUrl: form.logoUrl! });
          setToast(res.ok ? { ok: true, text: res.message ?? "Qo'shildi" } : { ok: false, text: res.error });
          if (res.ok) {
            setForm({ name: "", url: "", logoUrl: null });
            router.refresh();
          }
        });
      }}
    >
      <ImageUpload
        kind="partner"
        value={form.logoUrl}
        onChange={(url) => setForm({ ...form, logoUrl: url })}
        previewClass="h-24 w-44 rounded-xl [&_img]:object-contain [&_img]:p-2"
        accept="image/png,image/svg+xml,image/webp,image/jpeg"
        label="Logo"
      />
      <div className="space-y-3">
        <input className="input" placeholder="Kompaniya nomi" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} maxLength={80} required />
        <input className="input" placeholder="Sayt havolasi (ixtiyoriy): https://…" value={form.url} onChange={(e) => setForm({ ...form, url: e.target.value })} />
        <div className="flex flex-wrap items-center gap-4">
          <button className="btn-primary" disabled={pending}>
            + Hamkor qo&apos;shish
          </button>
          <Toast state={toast} />
        </div>
      </div>
    </form>
  );
}
