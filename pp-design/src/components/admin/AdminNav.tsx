"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { cn } from "@/lib/utils";

const LINKS = [
  { href: "/admin", label: "Bosh panel", icon: "M3 12l9-9 9 9M5 10v10h14V10" },
  { href: "/admin/designers", label: "Dizaynerlar", icon: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8m13 10v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8" },
  { href: "/admin/reviews", label: "Otzivlar", icon: "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" },
  { href: "/admin/content", label: "Matnlar va logo", icon: "M4 7V4h16v3M9 20h6M12 4v16" },
  { href: "/admin/works", label: "Ishlar", icon: "M3 3h7v7H3zM14 3h7v7h-7zM14 14h7v7h-7zM3 14h7v7H3z" },
  { href: "/admin/partners", label: "Hamkorlar", icon: "M20 7h-9M14 17H5M17 20a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM7 10a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" },
  { href: "/admin/contact", label: "Aloqa va manzil", icon: "M12 22s7-5.6 7-12a7 7 0 1 0-14 0c0 6.4 7 12 7 12Zm0-9a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" },
  { href: "/admin/intro", label: "Intro video", icon: "M23 7l-7 5 7 5V7zM1 5h15v14H1z" },
];

export function AdminNav({ pending }: { pending: number }) {
  const pathname = usePathname();
  const router = useRouter();
  const logout = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push("/admin/login");
    router.refresh();
  };
  return (
    <nav className="flex gap-1 overflow-x-auto pb-1 lg:flex-col lg:overflow-visible lg:pb-0">
      {LINKS.map((link) => {
        const active = link.href === "/admin" ? pathname === "/admin" : pathname.startsWith(link.href);
        return (
          <Link
            key={link.href}
            href={link.href}
            className={cn(
              "flex shrink-0 items-center gap-3 rounded-xl px-3 py-2.5 text-sm whitespace-nowrap transition",
              active ? "bg-brand/15 text-white ring-1 ring-brand/30" : "text-mist/75 hover:bg-white/5 hover:text-white",
            )}
          >
            <svg viewBox="0 0 24 24" className="h-4.5 w-4.5 shrink-0" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
              <path d={link.icon} />
            </svg>
            {link.label}
            {link.href === "/admin/designers" && pending > 0 && (
              <span className="ml-auto rounded-full bg-amber-300 px-2 py-0.5 text-[11px] font-bold text-ink-950">{pending}</span>
            )}
          </Link>
        );
      })}
      <button onClick={logout} className="flex shrink-0 items-center gap-3 rounded-xl px-3 py-2.5 text-sm text-rose-200/80 hover:bg-white/5 lg:mt-4">
        Chiqish
      </button>
    </nav>
  );
}
