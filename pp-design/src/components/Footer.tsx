import Link from "next/link";
import type { SiteSettings } from "@/lib/settings";
import { NAV_LINKS } from "@/lib/nav";
import { handleFrom } from "@/lib/utils";
import { Logo } from "./Logo";
import { ReplayIntroButton } from "./ReplayIntroButton";


export function Footer({ settings }: { settings: SiteSettings }) {
  const telegram = handleFrom(settings.contact.telegram);
  const instagram = handleFrom(settings.contact.instagram);
  return (
    <footer className="relative border-t border-white/10 bg-ink-950/60 pt-14 pb-10">
      <div className="container-x grid gap-10 md:grid-cols-[1.4fr_1fr_1fr]">
        <div>
          <Logo name={settings.brand.name} logoUrl={settings.brand.logoUrl} />
          <p className="mt-4 max-w-sm text-sm leading-relaxed text-mist/70">{settings.brand.tagline}</p>
          <div className="mt-5">
            <ReplayIntroButton />
          </div>
        </div>
        <div>
          <p className="mb-4 text-xs font-semibold tracking-widest text-muted uppercase">Bo&apos;limlar</p>
          <ul className="space-y-2.5 text-sm">
            {NAV_LINKS.map((link) => (
              <li key={link.href}>
                <Link href={link.href} className="text-mist/80 transition hover:text-white">
                  {link.label}
                </Link>
              </li>
            ))}
          </ul>
        </div>
        <div>
          <p className="mb-4 text-xs font-semibold tracking-widest text-muted uppercase">Hisob</p>
          <ul className="space-y-2.5 text-sm">
            <li>
              <Link href="/login?tab=client" className="text-mist/80 hover:text-white">
                Mijoz sifatida kirish
              </Link>
            </li>
            <li>
              <Link href="/login?tab=designer" className="text-mist/80 hover:text-white">
                Dizayner bo&apos;lib kirish
              </Link>
            </li>
            {telegram && (
              <li>
                <a href={`https://t.me/${telegram}`} className="text-mist/80 hover:text-white" target="_blank" rel="noopener noreferrer">
                  Telegram
                </a>
              </li>
            )}
            {instagram && (
              <li>
                <a href={`https://instagram.com/${instagram}`} className="text-mist/80 hover:text-white" target="_blank" rel="noopener noreferrer">
                  Instagram
                </a>
              </li>
            )}
          </ul>
        </div>
      </div>
      <div className="container-x mt-12 flex flex-col justify-between gap-3 border-t border-white/5 pt-6 text-xs text-muted sm:flex-row">
        <p>
          © {new Date().getFullYear()} {settings.brand.name}. Barcha huquqlar himoyalangan.
        </p>
        <p>pp-design.duckdns.org</p>
      </div>
    </footer>
  );
}
