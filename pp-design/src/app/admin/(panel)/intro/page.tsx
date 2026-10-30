import { PageHeader } from "@/components/admin/PageHeader";
import { IntroForm } from "@/components/admin/SettingsForms";
import { getSettings } from "@/lib/settings";

export default async function AdminIntro() {
  const settings = await getSettings();
  return (
    <>
      <PageHeader title="Intro video" />
      <IntroForm initial={settings.intro} />
      <p className="mt-6 text-sm text-mist/70">
        AI video generator (Sora, Veo, Runway) uchun tayyor sahnama-sahna prompt: <code className="rounded bg-white/10 px-1.5 py-0.5">docs/intro-video-prompt.md</code>
      </p>
    </>
  );
}
