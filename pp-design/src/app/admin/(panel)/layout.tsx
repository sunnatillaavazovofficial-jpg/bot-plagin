import type { Metadata } from "next";
import Link from "next/link";
import { AdminNav } from "@/components/admin/AdminNav";
import { Logo } from "@/components/Logo";
import { requireAdmin } from "@/lib/auth";
import { db } from "@/lib/db";
import { getSettings } from "@/lib/settings";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Admin panel", robots: { index: false, follow: false } };

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  const admin = await requireAdmin();
  const [settings, pending] = await Promise.all([getSettings(), db.designer.count({ where: { status: "PENDING" } })]);
  return (
    <div className="relative min-h-screen">
      <div className="glow -top-40 -left-40 h-[30rem] w-[30rem] bg-brand opacity-20" />
      <div className="glow right-0 bottom-0 h-[30rem] w-[30rem] bg-brand-2 opacity-20" />
      <div className="relative mx-auto flex max-w-7xl flex-col gap-6 px-4 py-6 lg:flex-row lg:px-6">
        <aside className="lg:sticky lg:top-6 lg:h-[calc(100vh-3rem)] lg:w-64 lg:shrink-0">
          <div className="glass flex h-full flex-col gap-5 rounded-3xl p-4">
            <div className="flex items-center justify-between">
              <Link href="/">
                <Logo name={settings.brand.name} logoUrl={settings.brand.logoUrl} />
              </Link>
            </div>
            <div className="rounded-xl bg-ink-950/50 px-3 py-2 text-xs text-muted">
              Admin: <span className="text-white">{admin.name || "Admin"}</span>
            </div>
            <AdminNav pending={pending} />
            <Link href="/" className="mt-auto hidden text-xs text-muted hover:text-white lg:block">
              ← Saytga qaytish
            </Link>
          </div>
        </aside>
        <main className="min-w-0 flex-1 pb-16">{children}</main>
      </div>
    </div>
  );
}
