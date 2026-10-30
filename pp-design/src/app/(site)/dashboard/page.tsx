import type { Metadata } from "next";
import Link from "next/link";
import { PortfolioManager } from "@/components/dashboard/PortfolioManager";
import { ProfileForm } from "@/components/dashboard/ProfileForm";
import { ResubmitButton } from "@/components/dashboard/ResubmitButton";
import { Stars } from "@/components/ui/Stars";
import { requireDesigner } from "@/lib/auth";
import { db } from "@/lib/db";
import { parsePrices, parseSocials } from "@/lib/designers";
import { averageRating, formatDate } from "@/lib/utils";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Mening profilim", robots: { index: false } };

const STATUS = {
  PENDING: {
    cls: "border-amber-300/30 bg-amber-300/10 text-amber-100",
    title: "⏳ So'rovingiz admin tomonidan ko'rib chiqilmoqda",
    text: "Hozircha profilingizni to'ldirib turing. Tasdiqlangach, profilingiz saytda ko'rinadi va Telegram orqali xabar olasiz.",
  },
  APPROVED: {
    cls: "border-emerald-400/30 bg-emerald-400/10 text-emerald-100",
    title: "✅ Profilingiz tasdiqlangan va saytda ko'rinadi",
    text: "Mijozlar qoldirgan otzivlar Telegram botga keladi. Portfolioni muntazam yangilab boring.",
  },
  REJECTED: {
    cls: "border-rose-400/30 bg-rose-400/10 text-rose-100",
    title: "❌ So'rovingiz rad etildi",
    text: "Profilni to'liqroq to'ldiring (rasm, mutaxassislik, portfolio) va qayta so'rov yuboring.",
  },
  BLOCKED: {
    cls: "border-rose-400/30 bg-rose-400/10 text-rose-100",
    title: "⛔ Profilingiz bloklangan",
    text: "Profil saytda ko'rinmaydi va tahrirlab bo'lmaydi. Admin bilan bog'laning.",
  },
} as const;

export default async function DashboardPage() {
  const user = await requireDesigner();
  const designer = await db.designer.findUniqueOrThrow({
    where: { id: user.designer.id },
    include: {
      portfolio: { orderBy: [{ order: "asc" }, { createdAt: "asc" }] },
      reviews: { orderBy: { createdAt: "desc" }, include: { client: { select: { name: true } } } },
    },
  });
  const status = STATUS[designer.status];
  const blocked = designer.status === "BLOCKED";
  const socials = parseSocials(designer.socials);
  const rating = averageRating(designer.reviews.map((r) => r.rating));

  return (
    <div className="relative pt-28 pb-20">
      <div className="glow -top-10 right-10 h-96 w-96 bg-brand-2 opacity-25" />
      <div className="container-x relative max-w-5xl">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <span className="section-eyebrow">Dizayner kabineti</span>
            <h1 className="font-display text-3xl font-semibold sm:text-4xl">Salom, {designer.fullName.split(" ")[0]}!</h1>
          </div>
          <Link href={`/designer/${designer.username}`} className="btn-ghost">
            Profilimni ko&apos;rish →
          </Link>
        </div>

        <div className={`mt-6 rounded-2xl border p-5 ${status.cls}`}>
          <p className="font-semibold">{status.title}</p>
          <p className="mt-1 text-sm opacity-90">{status.text}</p>
          {designer.status === "REJECTED" && (
            <div className="mt-3">
              <ResubmitButton />
            </div>
          )}
        </div>

        <div className="mt-6 grid grid-cols-3 gap-3">
          {[
            { value: designer.portfolio.length, label: "ish" },
            { value: designer.reviews.length, label: "otziv" },
            { value: designer.reviews.length ? rating.toFixed(1) : "—", label: "o'rtacha baho" },
          ].map((stat) => (
            <div key={stat.label} className="glass rounded-2xl p-4 text-center">
              <p className="text-gradient font-display text-2xl font-semibold sm:text-3xl">{stat.value}</p>
              <p className="text-xs text-muted sm:text-sm">{stat.label}</p>
            </div>
          ))}
        </div>

        <section className="mt-12">
          <h2 className="mb-6 font-display text-2xl font-semibold">Profil ma&apos;lumotlari</h2>
          <ProfileForm
            disabled={blocked}
            initial={{
              fullName: designer.fullName,
              username: designer.username,
              specialty: designer.specialty,
              bio: designer.bio,
              experienceYears: designer.experienceYears,
              experience: designer.experience,
              skills: designer.skills,
              prices: parsePrices(designer.prices),
              socials: {
                instagram: socials.instagram ?? "",
                telegram: socials.telegram ?? "",
                behance: socials.behance ?? "",
                dribbble: socials.dribbble ?? "",
                website: socials.website ?? "",
              },
              contactPhone: designer.contactPhone ?? "",
              contactEmail: designer.contactEmail ?? "",
              avatarUrl: designer.avatarUrl,
            }}
          />
        </section>

        <section className="mt-16">
          <h2 className="mb-6 font-display text-2xl font-semibold">Portfolio</h2>
          <PortfolioManager
            disabled={blocked}
            items={designer.portfolio.map((p) => ({ id: p.id, title: p.title, description: p.description, imageUrl: p.imageUrl }))}
          />
        </section>

        <section className="mt-16">
          <h2 className="mb-6 font-display text-2xl font-semibold">Menga qoldirilgan otzivlar</h2>
          <div className="grid gap-4">
            {designer.reviews.map((review) => (
              <article key={review.id} className="glass rounded-2xl p-5">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="font-semibold">{review.client.name || "Mijoz"}</p>
                  <span className="text-xs text-muted">{formatDate(review.createdAt)}</span>
                </div>
                <Stars value={review.rating} className="mt-1" size="h-3.5 w-3.5" />
                <p className="mt-3 whitespace-pre-line text-mist/85">{review.text}</p>
              </article>
            ))}
            {!designer.reviews.length && <p className="text-mist/60">Hali otziv yo&apos;q. Mijozlaringizga profilingiz havolasini yuboring.</p>}
          </div>
        </section>
      </div>
    </div>
  );
}
