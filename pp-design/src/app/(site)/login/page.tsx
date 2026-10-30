import type { Metadata } from "next";
import { redirect } from "next/navigation";
import { LoginTabs } from "@/components/auth/LoginTabs";
import { getCurrentUser } from "@/lib/auth";
import { botUsername, devLoginEnabled } from "@/lib/env";

export const metadata: Metadata = { title: "Kirish", robots: { index: false } };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ tab?: string; next?: string }> }) {
  const { tab, next } = await searchParams;
  const user = await getCurrentUser();
  if (user) {
    if (next?.startsWith("/") && !next.startsWith("//")) redirect(next);
    redirect(user.role === "ADMIN" ? "/admin" : user.role === "DESIGNER" ? "/dashboard" : "/");
  }

  return (
    <section className="relative flex min-h-[100svh] items-center justify-center overflow-hidden px-4 pt-28 pb-16">
      <div className="bg-grid absolute inset-0 -z-10" />
      <div className="glow -z-10 top-20 left-1/4 h-80 w-80 bg-brand" />
      <div className="glow -z-10 right-1/4 bottom-10 h-96 w-96 bg-brand-2" />
      <LoginTabs initialTab={tab === "designer" ? "designer" : "client"} next={next} botUsername={botUsername()} devLogin={devLoginEnabled()} />
    </section>
  );
}
