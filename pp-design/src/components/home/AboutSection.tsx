import type { SiteSettings } from "@/lib/settings";
import { Reveal } from "@/components/ui/Reveal";

const ICONS = [
  "M12 3l2.5 5 5.5.8-4 3.9.9 5.5L12 15.6 7.1 18.2 8 12.7 4 8.8l5.5-.8L12 3z",
  "M3 7h18v10H3zM7 7V5m10 2V5M6 11h4m-4 3h8",
  "M6 3h9l3 3v15H6zM9 9h6m-6 4h6m-6 4h4",
  "M3 5h18v12H3zM8 21h8m-4-4v4M7 9h4v4H7zm7 0h3m-3 3h3",
  "M4 4h16v16H4zM4 9h16M9 9v11",
  "M12 2v4m0 12v4M2 12h4m12 0h4M5 5l3 3m8 8 3 3m0-14-3 3M8 16l-3 3M12 8a4 4 0 1 1 0 8 4 4 0 0 1 0-8z",
];

export function AboutSection({ about, brandName }: { about: SiteSettings["about"]; brandName: string }) {
  return (
    <section id="kompaniya" className="relative scroll-mt-20 py-24">
      <div className="glow -left-40 top-1/3 h-96 w-96 bg-brand-2 opacity-25" />
      <div className="container-x grid gap-12 lg:grid-cols-2 lg:gap-16">
        <Reveal>
          <span className="section-eyebrow">Kompaniya</span>
          <h2 className="section-title">{about.title}</h2>
          <p className="mt-6 text-lg leading-relaxed whitespace-pre-line text-mist/85">{about.text}</p>
          <div className="mt-10 grid grid-cols-2 gap-4">
            {about.stats.map((stat) => (
              <div key={stat.label} className="glass rounded-2xl p-5">
                <p className="text-gradient font-display text-3xl font-semibold sm:text-4xl">{stat.value}</p>
                <p className="mt-1 text-sm text-mist/75">{stat.label}</p>
              </div>
            ))}
          </div>
        </Reveal>
        <div className="grid gap-4 sm:grid-cols-2">
          {about.services.map((service, i) => (
            <Reveal key={service.title} delay={i * 80} className="glass group rounded-2xl p-6 transition hover:border-brand/40">
              <span className="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-brand/10 text-brand ring-1 ring-brand/25 transition group-hover:scale-110">
                <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
                  <path d={ICONS[i % ICONS.length]} />
                </svg>
              </span>
              <h3 className="font-display text-base font-semibold">{service.title}</h3>
              <p className="mt-2 text-sm leading-relaxed text-mist/75">{service.text}</p>
            </Reveal>
          ))}
        </div>
      </div>
      <p className="sr-only">{brandName}</p>
    </section>
  );
}
