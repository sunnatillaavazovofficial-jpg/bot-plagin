import { AboutSection } from "@/components/home/AboutSection";
import { ContactSection } from "@/components/home/ContactSection";
import { DesignersSection } from "@/components/home/DesignersSection";
import { Hero } from "@/components/home/Hero";
import { WorksSection } from "@/components/home/WorksSection";
import { Intro } from "@/components/intro/Intro";
import { db } from "@/lib/db";
import { approvedDesignerCards } from "@/lib/designers";
import { siteUrl } from "@/lib/env";
import { getSettings } from "@/lib/settings";

export const dynamic = "force-dynamic";

export default async function HomePage() {
  const [settings, designers, projects, partners] = await Promise.all([
    getSettings(),
    approvedDesignerCards(30),
    db.project.findMany({ orderBy: [{ order: "asc" }, { createdAt: "desc" }] }),
    db.partner.findMany({ orderBy: [{ order: "asc" }, { createdAt: "asc" }] }),
  ]);

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: settings.brand.name,
    url: siteUrl(),
    logo: `${siteUrl()}/logo.svg`,
    description: settings.seo.description,
    telephone: settings.contact.phone,
    email: settings.contact.email || undefined,
    address: { "@type": "PostalAddress", streetAddress: settings.contact.address, addressCountry: "UZ" },
  };

  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, "\\u003c") }} />
      <Intro brandName={settings.brand.name} videoUrl={settings.intro.videoUrl} enabled={settings.intro.enabled} />
      <Hero badge={settings.hero.badge} lines={settings.hero.lines} subtitle={settings.hero.subtitle} />
      <DesignersSection designers={designers} />
      <AboutSection about={settings.about} brandName={settings.brand.name} />
      <WorksSection
        works={projects.map((p) => ({ id: p.id, title: p.title, category: p.category, description: p.description, imageUrl: p.imageUrl }))}
        partners={partners}
      />
      <ContactSection contact={settings.contact} />
    </>
  );
}
