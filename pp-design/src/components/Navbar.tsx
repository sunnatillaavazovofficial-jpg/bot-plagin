"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { NAV_LINKS } from "@/lib/nav";
import { cn } from "@/lib/utils";
import { Logo } from "./Logo";

export type NavUser = { name: string; role: "CLIENT" | "DESIGNER" | "ADMIN" } | null;

export function Navbar({ brandName, logoUrl, user }: { brandName: string; logoUrl: string | null; user: NavUser }) {
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);
  const [menu, setMenu] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const pathname = usePathname();
  const router = useRouter();

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 24);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => {
    setOpen(false);
    setMenu(false);
  }, [pathname]);

  useEffect(() => {
    if (!menu) return;
    const close = (e: MouseEvent) => !menuRef.current?.contains(e.target as Node) && setMenu(false);
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [menu]);

  const logout = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push("/");
    router.refresh();
  };

  const accountLinks =
    user?.role === "ADMIN"
      ? [{ href: "/admin", label: "Admin panel" }]
      : user?.role === "DESIGNER"
        ? [{ href: "/dashboard", label: "Mening profilim" }]
        : [];

  return (
    <header className={cn("fixed inset-x-0 top-0 z-50 transition-all duration-300", scrolled || open ? "py-2" : "py-4")}>
      <div className="container-x">
        <nav
          className={cn(
            "flex items-center justify-between rounded-2xl px-3 py-2 transition-all duration-300 sm:px-4",
            scrolled || open ? "glass shadow-2xl shadow-black/30" : "border border-transparent",
          )}
        >
          <Link href="/" aria-label={brandName} className="shrink-0">
            <Logo name={brandName} logoUrl={logoUrl} />
          </Link>

          <ul className="hidden items-center gap-1 lg:flex">
            {NAV_LINKS.map((link) => (
              <li key={link.href}>
                <Link href={link.href} className="rounded-full px-3.5 py-2 text-sm text-mist/85 transition hover:bg-white/5 hover:text-white">
                  {link.label}
                </Link>
              </li>
            ))}
          </ul>

          <div className="flex items-center gap-2">
            <div className="relative" ref={menuRef}>
              {user ? (
                <button className="btn-ghost !py-2" onClick={() => setMenu((m) => !m)} aria-expanded={menu}>
                  <span className="max-w-[120px] truncate">{user.name}</span>
                  <span aria-hidden>▾</span>
                </button>
              ) : (
                <button className="btn-primary !py-2" onClick={() => setMenu((m) => !m)} aria-expanded={menu}>
                  Kirish <span aria-hidden>▾</span>
                </button>
              )}
              {menu && (
                <div className="glass absolute right-0 mt-2 w-60 overflow-hidden rounded-2xl p-1.5 shadow-2xl">
                  {user ? (
                    <>
                      {accountLinks.map((link) => (
                        <Link key={link.href} href={link.href} className="block rounded-xl px-4 py-2.5 text-sm hover:bg-white/10">
                          {link.label}
                        </Link>
                      ))}
                      <button onClick={logout} className="block w-full rounded-xl px-4 py-2.5 text-left text-sm text-rose-200 hover:bg-white/10">
                        Chiqish
                      </button>
                    </>
                  ) : (
                    <>
                      <Link href="/login?tab=client" className="block rounded-xl px-4 py-2.5 hover:bg-white/10">
                        <span className="block text-sm font-semibold">Mijoz sifatida</span>
                        <span className="block text-xs text-muted">Telefon raqam bilan</span>
                      </Link>
                      <Link href="/login?tab=designer" className="block rounded-xl px-4 py-2.5 hover:bg-white/10">
                        <span className="block text-sm font-semibold">Dizayner sifatida</span>
                        <span className="block text-xs text-muted">Telegram orqali</span>
                      </Link>
                    </>
                  )}
                </div>
              )}
            </div>

            <button className="btn-ghost h-10 w-10 !p-0 lg:hidden" onClick={() => setOpen((o) => !o)} aria-label="Menyu" aria-expanded={open}>
              <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden>
                {open ? <path d="M6 6l12 12M18 6 6 18" /> : <path d="M4 7h16M4 12h16M4 17h16" />}
              </svg>
            </button>
          </div>
        </nav>

        {open && (
          <div className="glass mt-2 rounded-2xl p-2 lg:hidden">
            {NAV_LINKS.map((link) => (
              <Link key={link.href} href={link.href} onClick={() => setOpen(false)} className="block rounded-xl px-4 py-3 text-base hover:bg-white/10">
                {link.label}
              </Link>
            ))}
          </div>
        )}
      </div>
    </header>
  );
}
