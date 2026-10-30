import { PageHeader } from "@/components/admin/PageHeader";
import { ContactForm } from "@/components/admin/SettingsForms";
import { getSettings } from "@/lib/settings";

export default async function AdminContact() {
  const settings = await getSettings();
  return (
    <>
      <PageHeader title="Aloqa va manzil" />
      <ContactForm initial={settings.contact} />
    </>
  );
}
