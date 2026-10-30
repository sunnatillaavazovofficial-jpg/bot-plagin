import Link from "next/link";
import type { DesignerCard as Card } from "@/lib/designers";
import { Reveal } from "@/components/ui/Reveal";
import { CardStrokeDefs, DesignerCard } from "./DesignerCard";
import { Marquee, fillTo } from "./Marquee";

export function DesignersSection({ designers }: { designers: Card[] }) {
  const items = fillTo(designers, 8);
  return (
    <section id="dizaynerlar" className="relative scroll-mt-20 py-24">
      <CardStrokeDefs />
      <div className="container-x flex flex-col items-start justify-between gap-6 sm:flex-row sm:items-end">
        <Reveal>
          <span className="section-eyebrow">Jamoa</span>
          <h2 className="section-title">
            Bizning <span className="text-gradient">dizaynerlar</span>
          </h2>
          <p className="mt-3 max-w-xl text-mist/80">
            Har bir dizayner o&apos;z profilini o&apos;zi yuritadi: portfolio, narxlar va mijozlar otzivlari. Kartochkani bosib, profilga
            o&apos;ting.
          </p>
        </Reveal>
        <Link href="/designers" className="btn-ghost shrink-0">
          Barcha dizaynerlar →
        </Link>
      </div>

      {designers.length ? (
        <Reveal className="mt-10">
          <Marquee count={items.length} direction="right">
            {items.map((designer, i) => (
              <DesignerCard key={`${designer.id}-${i}`} designer={designer} />
            ))}
          </Marquee>
        </Reveal>
      ) : (
        <div className="container-x mt-10">
          <div className="glass rounded-3xl p-10 text-center">
            <p className="font-display text-xl">Tez orada bu yerda dizaynerlar paydo bo&apos;ladi</p>
            <p className="mt-2 text-mist/70">Siz birinchisi bo&apos;ling — Telegram orqali ro&apos;yxatdan o&apos;ting.</p>
            <Link href="/login?tab=designer" className="btn-primary mt-6">
              Dizayner bo&apos;lib qo&apos;shilish
            </Link>
          </div>
        </div>
      )}
    </section>
  );
}
