import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { cache } from "react";
import { ReviewForm } from "@/components/designer/ReviewForm";
import { WorksGallery } from "@/components/home/WorksGallery";
import { Avatar } from "@/components/ui/Avatar";
import { Stars } from "@/components/ui/Stars";
import { getCurrentUser } from "@/lib/auth";
import { db } from "@/lib/db";
import { parsePrices, parseSocials } from "@/lib/designers";
import { siteUrl } from "@/lib/env";
import { formatUzPhone, normalizeUzPhone, telHref } from "@/lib/phone";
import { averageRating, formatDate, handleFrom, safeUrl } from "@/lib/utils";

export const dynamic = "force-dynamic";

const loadDesigner = cache((username: string) =>
  db.designer.findUnique({
    where: { username: username.toLowerCase() },
    include: {
      portfolio: { orderBy: [{ order: "asc" }, { createdAt: "asc" }] },
      reviews: { orderBy: { createdAt: "desc" }, include: { client: { select: { name: true } } } },
    },
  }),
);

type Props = { params: Promise<{ username: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const designer = await loadDesigner((await params).username);
  if (!designer || designer.status !== "APPROVED") return { title: "Dizayner topilmadi", robots: { index: false } };
  const description = designer.bio.slice(0, 160) || `${designer.fullName} — ${designer.specialty}. Portfolio va mijozlar otzivlari.`;
  return {
    title: `${designer.fullName} — ${designer.specialty || "dizayner"}`,
    description,
    alternates: { canonical: `/designer/${designer.username}` },
    openGraph: { title: designer.fullName, description, images: designer.avatarUrl ? [designer.avatarUrl] : undefined, type: "profile" },
  };
}

const SOCIAL_LABELS = { telegram: "Telegram", instagram: "Instagram", behance: "Behance", dribbble: "Dribbble", website: "Veb-sayt" } as const;

function socialHref(key: keyof typeof SOCIAL_LABELS, value: string) {
  if (key === "website") return safeUrl(value);
  const handle = handleFrom(value);
  if (!handle) return null;
  const base = { telegram: "https://t.me/", instagram: "https://instagram.com/", behance: "https://www.behance.net/", dribbble: "https://dribbble.com/" }[key];
  return base + encodeURIComponent(handle);
}

export default async function DesignerPage({ params }: Props) {
  const { username } = await params;
  const [designer, viewer] = await Promise.all([loadDesigner(username), getCurrentUser()]);
  if (!designer) notFound();

  const isOwner = viewer?.designer?.id === designer.id;
  const isAdmin = viewer?.role === "ADMIN";
  if (designer.status !== "APPROVED" && !isOwner && !isAdmin) notFound();

  const rating = averageRating(designer.reviews.map((r) => r.rating));
  const prices = parsePrices(designer.prices);
  const socials = parseSocials(designer.socials);
  const ownReview = viewer ? designer.reviews.find((r) => r.clientId === viewer.id) : undefined;
  const phone = designer.contactPhone ? normalizeUzPhone(designer.contactPhone) : null;
  const distribution = [5, 4, 3, 2, 1].map((star) => ({ star, count: designer.reviews.filter((r) => r.rating === star).length }));

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "Person",
    name: designer.fullName,
    jobTitle: designer.specialty || undefined,
    url: `${siteUrl()}/designer/${designer.username}`,
    image: designer.avatarUrl ? new URL(designer.avatarUrl, siteUrl()).toString() : undefined,
    ...(designer.reviews.length
      ? { aggregateRating: { "@type": "AggregateRating", ratingValue: rating, reviewCount: designer.reviews.length, bestRating: 5 } }
      : {}),
  };

  return (
    <div className="relative pt-24 pb-20 sm:pt-28">
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }} />
      <div className="glow -top-10 left-0 h-96 w-96 bg-brand opacity-30" />
      <div className="glow top-40 right-0 h-96 w-96 bg-brand-2 opacity-30" />

      <div className="container-x relative">
        {designer.status !== "APPROVED" && (
          <div className="mb-6 rounded-2xl border border-amber-300/30 bg-amber-300/10 p-4 text-sm text-amber-100">
            Bu profil hali saytda ko&apos;rinmaydi (holat: {designer.status === "PENDING" ? "tasdiqlash kutilmoqda" : designer.status === "REJECTED" ? "rad etilgan" : "bloklangan"}). Uni faqat siz
            {isAdmin ? " (admin)" : ""} ko&apos;ryapsiz.
          </div>
        )}

        {/* Sarlavha kartochkasi */}
        <section className="glass overflow-hidden rounded-3xl">
          <div
            className="h-36 sm:h-48"
            style={{
              background:
                "radial-gradient(120% 140% at 0% 0%, color-mix(in oklab, var(--color-brand) 55%, transparent), transparent 60%), radial-gradient(120% 140% at 100% 100%, color-mix(in oklab, var(--color-brand-3) 45%, transparent), transparent 60%), var(--color-ink-800)",
            }}
          />
          <div className="flex flex-col gap-6 px-5 pb-6 sm:flex-row sm:items-end sm:px-8 sm:pb-8">
            <Avatar src={designer.avatarUrl} name={designer.fullName} className="-mt-16 h-32 w-32 shrink-0 border-4 border-ink-850 text-3xl" />
            <div className="min-w-0 flex-1">
              <h1 className="font-display text-2xl font-semibold sm:text-4xl">{designer.fullName}</h1>
              <p className="mt-1 text-brand">{designer.specialty || "Dizayner"}</p>
              <div className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-mist/80">
                <span className="inline-flex items-center gap-2">
                  <Stars value={rating} />
                  {designer.reviews.length ? `${rating.toFixed(1)} (${designer.reviews.length} otziv)` : "Hali otziv yo'q"}
                </span>
                {designer.experienceYears > 0 && <span>🏆 {designer.experienceYears} yillik tajriba</span>}
                <span>🖼 {designer.portfolio.length} ta ish</span>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              {phone && (
                <a href={telHref(phone)} className="btn-primary">
                  Qo&apos;ng&apos;iroq qilish
                </a>
              )}
              {socials.telegram && (
                <a href={socialHref("telegram", socials.telegram) ?? "#"} target="_blank" rel="noopener noreferrer" className="btn-ghost">
                  Telegramda yozish
                </a>
              )}
              {isOwner && (
                <Link href="/dashboard" className="btn-ghost">
                  ✏️ Profilni tahrirlash
                </Link>
              )}
            </div>
          </div>
        </section>

        <div className="mt-8 grid gap-8 lg:grid-cols-[1fr_340px]">
          <div className="min-w-0 space-y-10">
            {(designer.bio || designer.experience) && (
              <section>
                <h2 className="font-display text-xl font-semibold">O&apos;zim haqimda</h2>
                {designer.bio && <p className="mt-3 leading-relaxed whitespace-pre-line text-mist/85">{designer.bio}</p>}
                {designer.experience && (
                  <>
                    <h3 className="mt-6 text-sm font-semibold tracking-wide text-muted uppercase">Tajriba</h3>
                    <p className="mt-2 leading-relaxed whitespace-pre-line text-mist/85">{designer.experience}</p>
                  </>
                )}
              </section>
            )}

            <section>
              <h2 className="font-display text-xl font-semibold">Portfolio</h2>
              {designer.portfolio.length ? (
                <WorksGallery
                  works={designer.portfolio.map((item) => ({ id: item.id, title: item.title, description: item.description, imageUrl: item.imageUrl, category: "" }))}
                />
              ) : (
                <p className="mt-3 text-mist/60">Portfolio hali to&apos;ldirilmagan.</p>
              )}
            </section>

            <section id="otzivlar" className="scroll-mt-28">
              <h2 className="font-display text-xl font-semibold">Mijozlar otzivlari</h2>
              <div className="mt-4 grid gap-4">
                {designer.reviews.map((review) => (
                  <article key={review.id} className="glass rounded-2xl p-5">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <p className="font-semibold">{review.client.name || "Mijoz"}</p>
                      <span className="text-xs text-muted">{formatDate(review.createdAt)}</span>
                    </div>
                    <Stars value={review.rating} className="mt-1" size="h-3.5 w-3.5" />
                    <p className="mt-3 leading-relaxed whitespace-pre-line text-mist/85">{review.text}</p>
                  </article>
                ))}
                {!designer.reviews.length && <p className="text-mist/60">Hali otziv yo&apos;q. Birinchi bo&apos;lib fikr bildiring!</p>}
                {designer.status === "APPROVED" && (
                  <ReviewForm
                    designerId={designer.id}
                    username={designer.username}
                    viewer={viewer ? { role: viewer.role, hasName: Boolean(viewer.name) } : null}
                    existing={ownReview ? { rating: ownReview.rating, text: ownReview.text } : null}
                  />
                )}
              </div>
            </section>
          </div>

          <aside className="space-y-6 lg:sticky lg:top-28 lg:self-start">
            {designer.reviews.length > 0 && (
              <div className="glass rounded-2xl p-5">
                <p className="font-display text-4xl font-semibold">{rating.toFixed(1)}</p>
                <Stars value={rating} className="mt-1" />
                <div className="mt-4 space-y-1.5">
                  {distribution.map(({ star, count }) => (
                    <div key={star} className="flex items-center gap-2 text-xs text-muted">
                      <span className="w-3">{star}</span>
                      <span className="h-1.5 flex-1 overflow-hidden rounded-full bg-white/10">
                        <span className="block h-full rounded-full bg-amber-300" style={{ width: `${(count / designer.reviews.length) * 100}%` }} />
                      </span>
                      <span className="w-5 text-right">{count}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {designer.skills.length > 0 && (
              <div className="glass rounded-2xl p-5">
                <h2 className="mb-3 text-sm font-semibold tracking-wide text-muted uppercase">Ko&apos;nikmalar</h2>
                <div className="flex flex-wrap gap-2">
                  {designer.skills.map((skill) => (
                    <span key={skill} className="chip">
                      {skill}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {prices.length > 0 && (
              <div className="glass rounded-2xl p-5">
                <h2 className="mb-3 text-sm font-semibold tracking-wide text-muted uppercase">Narxlar</h2>
                <ul className="divide-y divide-white/5">
                  {prices.map((item) => (
                    <li key={item.title} className="flex items-start justify-between gap-4 py-2.5 text-sm">
                      <span className="text-mist/85">{item.title}</span>
                      <span className="shrink-0 font-semibold text-brand">{item.price}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <div className="glass rounded-2xl p-5">
              <h2 className="mb-3 text-sm font-semibold tracking-wide text-muted uppercase">Aloqa</h2>
              <ul className="space-y-2.5 text-sm">
                {phone && (
                  <li>
                    <a href={telHref(phone)} className="hover:text-brand">
                      📞 {formatUzPhone(phone)}
                    </a>
                  </li>
                )}
                {designer.contactEmail && (
                  <li>
                    <a href={`mailto:${designer.contactEmail}`} className="hover:text-brand">
                      ✉️ {designer.contactEmail}
                    </a>
                  </li>
                )}
                {(Object.keys(SOCIAL_LABELS) as (keyof typeof SOCIAL_LABELS)[]).map((key) => {
                  const value = socials[key];
                  const href = value ? socialHref(key, value) : null;
                  return href ? (
                    <li key={key}>
                      <a href={href} target="_blank" rel="noopener noreferrer nofollow" className="hover:text-brand">
                        🔗 {SOCIAL_LABELS[key]}: {key === "website" ? value : `@${handleFrom(value)}`}
                      </a>
                    </li>
                  ) : null;
                })}
                {!phone && !designer.contactEmail && Object.keys(socials).length === 0 && <li className="text-mist/60">Aloqa ma&apos;lumotlari kiritilmagan</li>}
              </ul>
            </div>
          </aside>
        </div>
      </div>
    </div>
  );
}
