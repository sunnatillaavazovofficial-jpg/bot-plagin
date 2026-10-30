import { PageHeader } from "@/components/admin/PageHeader";
import { AboutForm, BrandForm, HeroForm, SeoForm } from "@/components/admin/SettingsForms";
import { getSettings } from "@/lib/settings";

export default async function AdminContent() {
  const settings = await getSettings();
  return (
    <>
      <PageHeader title="Matnlar va logo" description="Bosh sahifadagi matnlar, kompaniya haqida ma'lumot va logo." />
      <div className="space-y-6">
        <BrandForm initial={settings.brand} />
        <HeroForm initial={settings.hero} />
        <AboutForm initial={settings.about} />
        <SeoForm initial={settings.seo} />
      </div>
    </>
  );
}
