import Link from "next/link";
import type { DesignerStatus, Prisma } from "@prisma/client";
import { DesignerRow, STATUS_BADGE } from "@/components/admin/DesignerRow";
import { PageHeader } from "@/components/admin/PageHeader";
import { db } from "@/lib/db";
import { cn } from "@/lib/utils";

const FILTERS: Array<{ key: DesignerStatus | "ALL"; label: string }> = [
  { key: "ALL", label: "Barchasi" },
  ...(Object.keys(STATUS_BADGE) as DesignerStatus[]).map((key) => ({ key, label: STATUS_BADGE[key][0] })),
];

export default async function AdminDesigners({ searchParams }: { searchParams: Promise<{ status?: string; q?: string }> }) {
  const { status = "ALL", q = "" } = await searchParams;
  const where: Prisma.DesignerWhereInput = {
    ...(status !== "ALL" && status in STATUS_BADGE ? { status: status as DesignerStatus } : {}),
    ...(q.trim()
      ? { OR: [{ fullName: { contains: q.trim(), mode: "insensitive" } }, { username: { contains: q.trim(), mode: "insensitive" } }, { specialty: { contains: q.trim(), mode: "insensitive" } }] }
      : {}),
  };
  const designers = await db.designer.findMany({
    where,
    orderBy: [{ status: "asc" }, { createdAt: "desc" }],
    include: { user: { select: { telegramId: true, telegramUsername: true } }, _count: { select: { reviews: true, portfolio: true } } },
  });

  return (
    <>
      <PageHeader
        title="Dizaynerlar"
        description="Telegram orqali ro'yxatdan o'tgan dizaynerlar. Tasdiqlangan profillar saytda ko'rinadi. Tasdiqlash/rad etishni Telegram botdan ham qilish mumkin."
      />
      <div className="mb-5 flex flex-wrap items-center gap-2">
        {FILTERS.map((filter) => (
          <Link
            key={filter.key}
            href={`/admin/designers?status=${filter.key}${q ? `&q=${encodeURIComponent(q)}` : ""}`}
            className={cn(
              "rounded-full border px-3.5 py-1.5 text-sm transition",
              status === filter.key ? "border-brand/60 bg-brand/15 text-white" : "border-white/10 text-mist/70 hover:text-white",
            )}
          >
            {filter.label}
          </Link>
        ))}
        <form className="ml-auto flex gap-2">
          <input type="hidden" name="status" value={status} />
          <input name="q" defaultValue={q} placeholder="Qidirish…" className="input !py-2" />
        </form>
      </div>
      <div className="grid gap-3">
        {designers.map((designer) => (
          <DesignerRow key={designer.id} designer={designer} />
        ))}
        {!designers.length && <p className="text-mist/60">Hech narsa topilmadi.</p>}
      </div>
    </>
  );
}
