import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AdminLoginForm } from "@/components/admin/AdminLoginForm";
import { DevLogin } from "@/components/auth/DevLogin";
import { TelegramLogin } from "@/components/auth/TelegramLogin";
import { LogoMark } from "@/components/Logo";
import { getCurrentUser } from "@/lib/auth";
import { botUsername, devLoginEnabled } from "@/lib/env";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Admin — kirish", robots: { index: false, follow: false } };

export default async function AdminLogin() {
  const user = await getCurrentUser();
  if (user?.role === "ADMIN") redirect("/admin");
  const passwordEnabled = Boolean(process.env.ADMIN_PASSWORD);

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-16">
      <div className="bg-grid absolute inset-0 -z-10" />
      <div className="glow -z-10 top-10 left-1/3 h-80 w-80 bg-brand-2" />
      <div className="glass w-full max-w-sm rounded-3xl p-7 shadow-2xl">
        <LogoMark className="mb-5 h-12 w-12" />
        <h1 className="font-display text-2xl font-semibold">Admin panel</h1>
        <p className="mt-2 mb-6 text-sm text-mist/70">Telegram orqali kiring — Telegram ID raqamingiz TELEGRAM_ADMIN_IDS ro&apos;yxatida bo&apos;lishi kerak.</p>
        <TelegramLogin botUsername={botUsername()} />
        {user && (
          <p className="mt-4 rounded-xl bg-rose-400/10 p-3 text-sm text-rose-200">Siz kirgan hisob admin emas. Admin hisobi bilan qayta kiring.</p>
        )}
        {passwordEnabled && (
          <>
            <div className="my-6 flex items-center gap-3 text-xs text-muted">
              <span className="h-px flex-1 bg-white/10" /> yoki parol bilan <span className="h-px flex-1 bg-white/10" />
            </div>
            <AdminLoginForm />
          </>
        )}
        {devLoginEnabled() && <DevLogin />}
        <Link href="/" className="mt-6 block text-center text-xs text-muted hover:text-white">
          ← Saytga qaytish
        </Link>
      </div>
    </div>
  );
}
