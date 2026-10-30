import Link from "next/link";
import type { Designer, User } from "@prisma/client";
import { adminDeleteDesigner, adminSetDesignerStatus } from "@/app/actions/admin";
import { Avatar } from "@/components/ui/Avatar";
import { formatDate } from "@/lib/utils";
import { ActionButton } from "./ActionButton";

export const STATUS_BADGE = {
  PENDING: ["Kutilmoqda", "bg-amber-300/15 text-amber-200 ring-amber-300/30"],
  APPROVED: ["Tasdiqlangan", "bg-emerald-400/15 text-emerald-200 ring-emerald-400/30"],
  REJECTED: ["Rad etilgan", "bg-rose-400/15 text-rose-200 ring-rose-400/30"],
  BLOCKED: ["Bloklangan", "bg-zinc-400/15 text-zinc-300 ring-zinc-400/30"],
} as const;

export function DesignerRow({
  designer,
}: {
  designer: Designer & { user: Pick<User, "telegramId" | "telegramUsername">; _count: { reviews: number; portfolio: number } };
}) {
  const [label, cls] = STATUS_BADGE[designer.status];
  const set = (status: Designer["status"]) => adminSetDesignerStatus.bind(null, designer.id, status);
  return (
    <div className="glass flex flex-col gap-4 rounded-2xl p-4 sm:flex-row sm:items-center">
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <Avatar src={designer.avatarUrl} name={designer.fullName} className="h-12 w-12 shrink-0 text-sm" />
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <Link href={`/designer/${designer.username}`} className="truncate font-semibold hover:text-brand" target="_blank">
              {designer.fullName}
            </Link>
            <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ring-1 ${cls}`}>{label}</span>
          </div>
          <p className="truncate text-xs text-muted">
            {designer.specialty || "Mutaxassislik ko'rsatilmagan"} · {designer._count.portfolio} ish · {designer._count.reviews} otziv ·{" "}
            {designer.user.telegramUsername ? (
              <a href={`https://t.me/${designer.user.telegramUsername}`} target="_blank" rel="noopener noreferrer" className="text-brand hover:underline">
                @{designer.user.telegramUsername}
              </a>
            ) : (
              `ID ${designer.user.telegramId ?? "—"}`
            )}{" "}
            · {formatDate(designer.createdAt)}
          </p>
        </div>
      </div>
      <div className="flex flex-wrap gap-2">
        {designer.status !== "APPROVED" && (
          <ActionButton variant="primary" action={set("APPROVED")}>
            ✅ Tasdiqlash
          </ActionButton>
        )}
        {designer.status === "PENDING" && <ActionButton action={set("REJECTED")}>❌ Rad etish</ActionButton>}
        {designer.status !== "BLOCKED" && designer.status !== "PENDING" && (
          <ActionButton action={set("BLOCKED")} confirmText="Dizaynerni bloklaysizmi? Profili saytdan yashiriladi.">
            ⛔ Bloklash
          </ActionButton>
        )}
        <ActionButton
          variant="danger"
          action={adminDeleteDesigner.bind(null, designer.id)}
          confirmText={`${designer.fullName} va uning barcha ma'lumotlari (portfolio, otzivlar) butunlay o'chiriladi. Davom etasizmi?`}
        >
          O&apos;chirish
        </ActionButton>
      </div>
    </div>
  );
}
