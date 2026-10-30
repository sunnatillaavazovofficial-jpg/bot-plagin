import Link from "next/link";
import { DesignerRow } from "@/components/admin/DesignerRow";
import { PageHeader } from "@/components/admin/PageHeader";
import { Stars } from "@/components/ui/Stars";
import { db } from "@/lib/db";
import { botToken, adminTelegramIds } from "@/lib/env";
import { formatDate } from "@/lib/utils";

export default async function AdminHome() {
  const [designers, approved, pending, clients, reviews, projects, partners, latestReviews] = await Promise.all([
    db.designer.findMany({
      where: { status: "PENDING" },
      orderBy: { createdAt: "asc" },
      include: { user: { select: { telegramId: true, telegramUsername: true } }, _count: { select: { reviews: true, portfolio: true } } },
    }),
    db.designer.count({ where: { status: "APPROVED" } }),
    db.designer.count({ where: { status: "PENDING" } }),
    db.user.count({ where: { role: "CLIENT" } }),
    db.review.count(),
    db.project.count(),
    db.partner.count(),
    db.review.findMany({ orderBy: { createdAt: "desc" }, take: 5, include: { client: { select: { name: true } }, designer: { select: { fullName: true, username: true } } } }),
  ]);

  const stats = [
    { label: "Faol dizaynerlar", value: approved, href: "/admin/designers?status=APPROVED" },
    { label: "Kutilayotgan so'rovlar", value: pending, href: "/admin/designers?status=PENDING" },
    { label: "Mijozlar", value: clients },
    { label: "Otzivlar", value: reviews, href: "/admin/reviews" },
    { label: "Ishlar", value: projects, href: "/admin/works" },
    { label: "Hamkorlar", value: partners, href: "/admin/partners" },
  ];
  const botReady = Boolean(botToken());
  const admins = adminTelegramIds().length;

  return (
    <>
      <PageHeader title="Bosh panel" description="Saytdagi asosiy ko'rsatkichlar va tasdiqlanishini kutayotgan dizaynerlar." />

      {(!botReady || admins === 0) && (
        <div className="mb-6 rounded-2xl border border-amber-300/30 bg-amber-300/10 p-4 text-sm text-amber-100">
          ⚠️ Telegram bot to&apos;liq sozlanmagan:
          {!botReady && " TELEGRAM_BOT_TOKEN ko'rsatilmagan."}
          {admins === 0 && " TELEGRAM_ADMIN_IDS bo'sh — so'rov va otzivlar hech kimga yuborilmaydi."} Batafsil: README.md
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
        {stats.map((stat) => {
          const body = (
            <>
              <p className="text-gradient font-display text-3xl font-semibold">{stat.value}</p>
              <p className="mt-1 text-sm text-mist/70">{stat.label}</p>
            </>
          );
          return stat.href ? (
            <Link key={stat.label} href={stat.href} className="glass rounded-2xl p-5 transition hover:border-brand/40">
              {body}
            </Link>
          ) : (
            <div key={stat.label} className="glass rounded-2xl p-5">
              {body}
            </div>
          );
        })}
      </div>

      <h2 className="mt-10 mb-4 font-display text-xl font-semibold">Tasdiqlash kutilmoqda</h2>
      <div className="grid gap-3">
        {designers.map((designer) => (
          <DesignerRow key={designer.id} designer={designer} />
        ))}
        {!designers.length && <p className="text-mist/60">Yangi so&apos;rovlar yo&apos;q ✨</p>}
      </div>

      <h2 className="mt-10 mb-4 font-display text-xl font-semibold">So&apos;nggi otzivlar</h2>
      <div className="grid gap-3">
        {latestReviews.map((review) => (
          <div key={review.id} className="glass rounded-2xl p-4">
            <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
              <span>
                <b>{review.client.name || "Mijoz"}</b> →{" "}
                <Link href={`/designer/${review.designer.username}`} className="text-brand hover:underline" target="_blank">
                  {review.designer.fullName}
                </Link>
              </span>
              <span className="text-xs text-muted">{formatDate(review.createdAt)}</span>
            </div>
            <Stars value={review.rating} className="mt-1" size="h-3.5 w-3.5" />
            <p className="mt-2 line-clamp-2 text-sm text-mist/80">{review.text}</p>
          </div>
        ))}
        {!latestReviews.length && <p className="text-mist/60">Hali otziv yo&apos;q.</p>}
      </div>
    </>
  );
}
