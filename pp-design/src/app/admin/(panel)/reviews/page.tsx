import Link from "next/link";
import { adminDeleteReview } from "@/app/actions/admin";
import { ActionButton } from "@/components/admin/ActionButton";
import { PageHeader } from "@/components/admin/PageHeader";
import { Stars } from "@/components/ui/Stars";
import { db } from "@/lib/db";
import { formatUzPhone } from "@/lib/phone";
import { formatDate } from "@/lib/utils";

export default async function AdminReviews() {
  const reviews = await db.review.findMany({
    orderBy: { createdAt: "desc" },
    take: 200,
    include: { client: { select: { name: true, phone: true } }, designer: { select: { fullName: true, username: true } } },
  });
  return (
    <>
      <PageHeader title="Otzivlar" description="Mijozlar qoldirgan barcha otzivlar. Har bir yangi otziv Telegram botga ham keladi." />
      <div className="grid gap-3">
        {reviews.map((review) => (
          <div key={review.id} className="glass flex flex-col gap-3 rounded-2xl p-4 sm:flex-row sm:items-start">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
                <b>{review.client.name || "Mijoz"}</b>
                {review.client.phone && <span className="text-muted">{formatUzPhone(review.client.phone)}</span>}
                <span className="text-muted">→</span>
                <Link href={`/designer/${review.designer.username}`} target="_blank" className="text-brand hover:underline">
                  {review.designer.fullName}
                </Link>
                <span className="text-xs text-muted">{formatDate(review.createdAt)}</span>
              </div>
              <Stars value={review.rating} className="mt-1.5" size="h-3.5 w-3.5" />
              <p className="mt-2 text-sm whitespace-pre-line text-mist/85">{review.text}</p>
            </div>
            <ActionButton variant="danger" action={adminDeleteReview.bind(null, review.id)} confirmText="Otzivni o'chirasizmi?">
              O&apos;chirish
            </ActionButton>
          </div>
        ))}
        {!reviews.length && <p className="text-mist/60">Hali otziv yo&apos;q.</p>}
      </div>
    </>
  );
}
