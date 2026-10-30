import type { Partner } from "@prisma/client";
import { Reveal } from "@/components/ui/Reveal";
import { Marquee, fillTo } from "./Marquee";
import { WorksGallery, type Work } from "./WorksGallery";

export function WorksSection({ works, partners }: { works: Work[]; partners: Partner[] }) {
  const logos = fillTo(partners, 10);
  return (
    <section id="ishlar" className="relative scroll-mt-20 py-24">
      <div className="container-x">
        <Reveal>
          <span className="section-eyebrow">Portfolio</span>
          <h2 className="section-title">
            Asosiy <span className="text-gradient">qilgan ishlarimiz</span>
          </h2>
          <p className="mt-3 max-w-xl text-mist/80">Brendlar, reklama va raqamli loyihalar — har biri o&apos;z hikoyasiga ega.</p>
        </Reveal>
        <WorksGallery works={works} />
      </div>

      {partners.length > 0 && (
        <div id="hamkorlar" className="mt-24 scroll-mt-24">
          <div className="container-x text-center">
            <span className="section-eyebrow">Hamkorlar</span>
            <h2 className="font-display text-2xl font-semibold sm:text-3xl">Bizga ishonch bildirgan kompaniyalar</h2>
          </div>
          <Marquee count={logos.length} secondsPerItem={3} direction="left" className="mt-10" gapClass="gap-5">
            {logos.map((partner, i) => {
              const content = (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={partner.logoUrl} alt={partner.name} title={partner.name} className="max-h-14 max-w-[150px] object-contain opacity-70 grayscale transition duration-300 group-hover:opacity-100 group-hover:grayscale-0" />
              );
              const cls = "glass group flex h-24 w-48 shrink-0 items-center justify-center rounded-2xl px-6";
              return partner.url ? (
                <a key={`${partner.id}-${i}`} href={partner.url} target="_blank" rel="noopener noreferrer" className={cls}>
                  {content}
                </a>
              ) : (
                <div key={`${partner.id}-${i}`} className={cls}>
                  {content}
                </div>
              );
            })}
          </Marquee>
        </div>
      )}
    </section>
  );
}
