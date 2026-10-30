import type { Metadata } from "next";
import { CardStrokeDefs, DesignerCard } from "@/components/home/DesignerCard";
import { Reveal } from "@/components/ui/Reveal";
import { approvedDesignerCards } from "@/lib/designers";

export const dynamic = "force-dynamic";
export const metadata: Metadata = {
  title: "Barcha dizaynerlar",
  description: "PP Design platformasidagi dizaynerlar: portfolio, narxlar va mijozlar otzivlari.",
  alternates: { canonical: "/designers" },
};

export default async function DesignersPage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  const { q = "" } = await searchParams;
  const query = q.trim().toLowerCase();
  const all = await approvedDesignerCards();
  const designers = query
    ? all.filter((d) => [d.fullName, d.specialty, ...d.skills].some((field) => field.toLowerCase().includes(query)))
    : all;

  return (
    <section className="relative pt-28 pb-24">
      <CardStrokeDefs />
      <div className="glow -top-20 left-1/4 h-96 w-96 bg-brand opacity-25" />
      <div className="container-x relative">
        <span className="section-eyebrow">Jamoa</span>
        <h1 className="section-title">
          Barcha <span className="text-gradient">dizaynerlar</span>
        </h1>
        <form className="mt-8 flex max-w-lg gap-2">
          <input name="q" defaultValue={q} className="input" placeholder="Ism, yo'nalish yoki ko'nikma bo'yicha qidirish" />
          <button className="btn-primary shrink-0">Qidirish</button>
        </form>
        <div className="mt-10 flex flex-wrap justify-center gap-6 sm:justify-start">
          {designers.map((designer, i) => (
            <Reveal key={designer.id} delay={(i % 4) * 80}>
              <DesignerCard designer={designer} />
            </Reveal>
          ))}
        </div>
        {!designers.length && <p className="mt-10 text-mist/70">Hech narsa topilmadi.</p>}
      </div>
    </section>
  );
}
