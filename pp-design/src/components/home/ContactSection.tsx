import type { SiteSettings } from "@/lib/settings";
import { formatUzPhone, normalizeUzPhone, telHref } from "@/lib/phone";
import { handleFrom } from "@/lib/utils";
import { Reveal } from "@/components/ui/Reveal";

function mapEmbed(c: SiteSettings["contact"]) {
  return c.mapProvider === "google"
    ? `https://maps.google.com/maps?q=${c.lat},${c.lng}&z=16&output=embed`
    : `https://yandex.uz/map-widget/v1/?ll=${c.lng},${c.lat}&z=16&pt=${c.lng},${c.lat},pm2rdm`;
}

function mapLink(c: SiteSettings["contact"]) {
  return c.mapProvider === "google"
    ? `https://www.google.com/maps/search/?api=1&query=${c.lat},${c.lng}`
    : `https://yandex.uz/maps/?pt=${c.lng},${c.lat}&z=17&l=map`;
}

const Icon = ({ d }: { d: string }) => (
  <span className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-brand/10 text-brand ring-1 ring-brand/25">
    <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d={d} />
    </svg>
  </span>
);

export function ContactSection({ contact }: { contact: SiteSettings["contact"] }) {
  const phones = [contact.phone, contact.phone2].filter(Boolean);
  const telegram = handleFrom(contact.telegram);
  const instagram = handleFrom(contact.instagram);
  const pretty = (p: string) => (normalizeUzPhone(p) ? formatUzPhone(normalizeUzPhone(p)!) : p);

  return (
    <section id="aloqa" className="relative scroll-mt-20 py-24">
      <div className="container-x">
        <Reveal>
          <span className="section-eyebrow">Aloqa</span>
          <h2 className="section-title">
            Biz bilan <span className="text-gradient">bog&apos;laning</span>
          </h2>
        </Reveal>

        <div className="mt-10 grid gap-6 lg:grid-cols-[0.9fr_1.1fr]">
          <Reveal className="glass flex flex-col gap-5 rounded-3xl p-6 sm:p-8">
            {phones.length > 0 && (
              <div className="flex gap-4">
                <Icon d="M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2Z" />
                <div>
                  <p className="text-sm text-muted">Telefon</p>
                  {phones.map((p) => (
                    <a key={p} href={telHref(p)} className="block font-display text-xl font-semibold transition hover:text-brand">
                      {pretty(p)}
                    </a>
                  ))}
                </div>
              </div>
            )}
            <div className="flex gap-4">
              <Icon d="M12 22s7-5.6 7-12a7 7 0 1 0-14 0c0 6.4 7 12 7 12Zm0-9a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" />
              <div>
                <p className="text-sm text-muted">Manzil</p>
                <p className="font-semibold">{contact.address}</p>
                <a href={mapLink(contact)} target="_blank" rel="noopener noreferrer" className="mt-1 inline-block text-sm text-brand hover:underline">
                  Xaritada ochish →
                </a>
              </div>
            </div>
            {contact.workHours && (
              <div className="flex gap-4">
                <Icon d="M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20Zm0-15v5l3 2" />
                <div>
                  <p className="text-sm text-muted">Ish vaqti</p>
                  <p className="font-semibold">{contact.workHours}</p>
                </div>
              </div>
            )}
            {contact.email && (
              <div className="flex gap-4">
                <Icon d="M4 4h16v16H4zM22 6l-10 7L2 6" />
                <div>
                  <p className="text-sm text-muted">Email</p>
                  <a href={`mailto:${contact.email}`} className="font-semibold hover:text-brand">
                    {contact.email}
                  </a>
                </div>
              </div>
            )}
            <div className="mt-auto flex flex-wrap gap-3 pt-2">
              {phones[0] && (
                <a href={telHref(phones[0])} className="btn-primary">
                  Qo&apos;ng&apos;iroq qilish
                </a>
              )}
              {telegram && (
                <a href={`https://t.me/${telegram}`} target="_blank" rel="noopener noreferrer" className="btn-ghost">
                  Telegram
                </a>
              )}
              {instagram && (
                <a href={`https://instagram.com/${instagram}`} target="_blank" rel="noopener noreferrer" className="btn-ghost">
                  Instagram
                </a>
              )}
            </div>
          </Reveal>

          <Reveal delay={120} className="overflow-hidden rounded-3xl border border-white/10 bg-ink-850">
            <iframe
              title="Xarita"
              src={mapEmbed(contact)}
              className="h-[360px] w-full [filter:invert(0.9)_hue-rotate(185deg)_saturate(0.75)_brightness(0.95)] sm:h-full sm:min-h-[420px]"
              loading="lazy"
              referrerPolicy="no-referrer-when-downgrade"
              allowFullScreen
            />
          </Reveal>
        </div>
      </div>
    </section>
  );
}
