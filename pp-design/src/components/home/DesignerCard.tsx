import Link from "next/link";
import type { DesignerCard as Card } from "@/lib/designers";
import { Avatar } from "@/components/ui/Avatar";
import { Stars } from "@/components/ui/Stars";

/** Dizayner kartochkasi: ekranga kirganda chegarasi "chiziladi", keyin ichi paydo bo'ladi. */
export function DesignerCard({ designer }: { designer: Card }) {
  return (
    <Link
      href={`/designer/${designer.username}`}
      className="group relative block w-[270px] shrink-0 overflow-hidden rounded-3xl bg-ink-850/80 transition duration-300 hover:-translate-y-1.5 sm:w-[300px]"
    >
      <svg className="draw-border pointer-events-none absolute inset-0 z-10 h-full w-full" aria-hidden>
        <rect
          x="0.75"
          y="0.75"
          width="100%"
          height="100%"
          rx="23"
          fill="none"
          stroke="url(#card-stroke)"
          strokeWidth="1.5"
          pathLength={1}
          style={{ width: "calc(100% - 1.5px)", height: "calc(100% - 1.5px)" }}
        />
      </svg>
      <div className="draw-content">
        <div className="relative h-36 overflow-hidden">
          {designer.cover ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={designer.cover} alt="" className="h-full w-full object-cover transition duration-500 group-hover:scale-105" loading="lazy" />
          ) : (
            <div className="h-full w-full" style={{ background: "linear-gradient(135deg, color-mix(in oklab, var(--color-brand) 40%, transparent), color-mix(in oklab, var(--color-brand-2) 45%, transparent))" }} />
          )}
          <div className="absolute inset-0 bg-gradient-to-t from-ink-850 via-ink-850/30 to-transparent" />
        </div>
        <div className="relative -mt-10 px-5 pb-5">
          <Avatar src={designer.avatarUrl} name={designer.fullName} className="h-16 w-16 border-4 border-ink-850 text-lg" />
          <h3 className="mt-3 truncate font-display text-base font-semibold">{designer.fullName}</h3>
          <p className="truncate text-sm text-brand">{designer.specialty || "Dizayner"}</p>
          <div className="mt-2 flex items-center gap-2 text-xs text-muted">
            <Stars value={designer.rating} size="h-3.5 w-3.5" />
            <span>{designer.reviewCount ? `${designer.rating.toFixed(1)} · ${designer.reviewCount} otziv` : "Hali otziv yo'q"}</span>
          </div>
          {designer.skills.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-1.5">
              {designer.skills.map((skill) => (
                <span key={skill} className="chip">
                  {skill}
                </span>
              ))}
            </div>
          )}
          <span className="mt-4 inline-flex items-center gap-1 text-sm font-semibold text-white/80 transition group-hover:gap-2 group-hover:text-brand">
            Profilni ko&apos;rish <span aria-hidden>→</span>
          </span>
        </div>
      </div>
    </Link>
  );
}

/** Kartochka chegarasi uchun umumiy gradient (sahifada bir marta qo'yiladi) */
export function CardStrokeDefs() {
  return (
    <svg width="0" height="0" className="absolute" aria-hidden>
      <defs>
        <linearGradient id="card-stroke" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" style={{ stopColor: "var(--color-brand)" }} />
          <stop offset="0.5" style={{ stopColor: "var(--color-brand-2)" }} />
          <stop offset="1" style={{ stopColor: "var(--color-brand-3)" }} />
        </linearGradient>
      </defs>
    </svg>
  );
}
