import { adminDeletePartner, adminMove } from "@/app/actions/admin";
import { ActionButton } from "@/components/admin/ActionButton";
import { PartnerForm } from "@/components/admin/CollectionForms";
import { PageHeader } from "@/components/admin/PageHeader";
import { db } from "@/lib/db";

export default async function AdminPartners() {
  const partners = await db.partner.findMany({ orderBy: [{ order: "asc" }, { createdAt: "asc" }] });
  return (
    <>
      <PageHeader title="Hamkorlar" description="Hamkorlar logolari bosh sahifada harakatlanuvchi lentada ko'rinadi." />
      <PartnerForm />
      <div className="mt-8 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {partners.map((partner, i) => (
          <div key={partner.id} className="glass flex items-center gap-3 rounded-2xl p-3">
            <div className="flex h-16 w-28 shrink-0 items-center justify-center rounded-xl bg-ink-950/60 p-2">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={partner.logoUrl} alt={partner.name} className="max-h-full max-w-full object-contain" />
            </div>
            <div className="min-w-0 flex-1">
              <p className="truncate font-semibold">{partner.name}</p>
              {partner.url && <p className="truncate text-xs text-muted">{partner.url}</p>}
              <div className="mt-2 flex gap-1.5">
                <ActionButton action={adminMove.bind(null, "partner", partner.id, -1)} className={i === 0 ? "invisible" : ""}>
                  ←
                </ActionButton>
                <ActionButton action={adminMove.bind(null, "partner", partner.id, 1)} className={i === partners.length - 1 ? "invisible" : ""}>
                  →
                </ActionButton>
                <ActionButton variant="danger" action={adminDeletePartner.bind(null, partner.id)} confirmText="Hamkorni o'chirasizmi?">
                  ✕
                </ActionButton>
              </div>
            </div>
          </div>
        ))}
      </div>
      {!partners.length && <p className="mt-6 text-mist/60">Hali hamkor qo&apos;shilmagan.</p>}
    </>
  );
}
