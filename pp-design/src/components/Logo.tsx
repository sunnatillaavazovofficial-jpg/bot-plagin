import { cn } from "@/lib/utils";

/** Vaqtinchalik "PP" belgisi. Admin panelda haqiqiy logo yuklansa, o'sha ko'rsatiladi. */
export function LogoMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 48 48" className={className} aria-hidden="true">
      <defs>
        <linearGradient id="pp-g1" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" style={{ stopColor: "var(--color-brand)" }} />
          <stop offset="1" style={{ stopColor: "var(--color-brand-2)" }} />
        </linearGradient>
        <linearGradient id="pp-g2" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" style={{ stopColor: "var(--color-brand-2)" }} />
          <stop offset="1" style={{ stopColor: "var(--color-brand-3)" }} />
        </linearGradient>
      </defs>
      <rect x="1.5" y="1.5" width="45" height="45" rx="13" style={{ fill: "var(--color-ink-800)" }} stroke="url(#pp-g1)" strokeWidth="1.5" />
      <path d="M13 35V13h8.5a6.5 6.5 0 0 1 0 13H13" fill="none" stroke="url(#pp-g1)" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M24 37V20h6.5a6 6 0 0 1 0 12H24" fill="none" stroke="url(#pp-g2)" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" opacity="0.95" />
    </svg>
  );
}

export function Logo({ name, logoUrl, className }: { name: string; logoUrl?: string | null; className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-2.5", className)}>
      {logoUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={logoUrl} alt={name} className="h-10 w-auto max-w-[140px] object-contain" />
      ) : (
        <LogoMark className="h-10 w-10 shrink-0" />
      )}
      {!logoUrl && (
        <span className="font-display text-lg leading-none font-semibold tracking-tight">
          {name.split(" ")[0]}
          <span className="text-gradient"> {name.split(" ").slice(1).join(" ")}</span>
        </span>
      )}
    </span>
  );
}
