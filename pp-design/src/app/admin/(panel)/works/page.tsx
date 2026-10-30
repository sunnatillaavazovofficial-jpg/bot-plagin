import { adminDeleteProject, adminMove } from "@/app/actions/admin";
import { ActionButton } from "@/components/admin/ActionButton";
import { ProjectForm } from "@/components/admin/CollectionForms";
import { PageHeader } from "@/components/admin/PageHeader";
import { db } from "@/lib/db";

export default async function AdminWorks() {
  const projects = await db.project.findMany({ orderBy: [{ order: "asc" }, { createdAt: "desc" }] });
  const categories = Array.from(new Set(projects.map((p) => p.category).filter(Boolean)));
  return (
    <>
      <PageHeader title="Asosiy ishlar" description="Bosh sahifadagi «Asosiy qilgan ishlarimiz» galereyasi. Kategoriya bo'yicha filtr avtomatik chiqadi." />
      <ProjectForm categories={categories} />
      <div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {projects.map((project, i) => (
          <div key={project.id} className="glass overflow-hidden rounded-2xl">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={project.imageUrl} alt={project.title} className="aspect-[4/3] w-full object-cover" />
            <div className="p-4">
              {project.category && <p className="text-xs font-semibold tracking-wider text-brand uppercase">{project.category}</p>}
              <p className="truncate font-semibold">{project.title}</p>
              <div className="mt-3 flex gap-2">
                <ActionButton action={adminMove.bind(null, "project", project.id, -1)} title="Oldinga" className={i === 0 ? "invisible" : ""}>
                  ←
                </ActionButton>
                <ActionButton action={adminMove.bind(null, "project", project.id, 1)} title="Orqaga" className={i === projects.length - 1 ? "invisible" : ""}>
                  →
                </ActionButton>
                <ActionButton variant="danger" className="ml-auto" action={adminDeleteProject.bind(null, project.id)} confirmText="Bu ishni o'chirasizmi?">
                  O&apos;chirish
                </ActionButton>
              </div>
            </div>
          </div>
        ))}
      </div>
      {!projects.length && <p className="mt-6 text-mist/60">Hali ish qo&apos;shilmagan.</p>}
    </>
  );
}
