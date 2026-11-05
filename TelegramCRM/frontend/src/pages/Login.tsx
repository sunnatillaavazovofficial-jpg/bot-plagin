import { Clock, Globe, LogIn, MapPin, Phone, Send } from "lucide-react";
import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { Button, Field, Input, cx } from "../components/ui";
import { api } from "../lib/api";

type Lang = "uz" | "ru";

interface PublicSite {
  configured: boolean;
  name: string;
  title_uz: string;
  title_ru: string;
  tagline_uz: string;
  tagline_ru: string;
  about_uz: string;
  about_ru: string;
  services_uz: string[];
  services_ru: string[];
  facts_uz: string[];
  facts_ru: string[];
  phone: string;
  address_uz: string;
  address_ru: string;
  hours_uz: string;
  hours_ru: string;
  telegram: string;
  instagram: string;
  website: string;
}

const T = {
  uz: {
    loginTitle: "Xodimlar uchun kirish",
    loginHint: "Ish tizimiga faqat login va parol bilan kiriladi.",
    login: "Login",
    password: "Parol",
    submit: "Kirish",
    busy: "Kirilmoqda...",
    services: "Xizmatlar",
    contacts: "Bog'lanish",
    call: "Qo'ng'iroq qilish",
    write: "Telegram'da yozish",
    staff: "Xodimlar",
  },
  ru: {
    loginTitle: "Вход для сотрудников",
    loginHint: "Вход в рабочую систему — только по логину и паролю.",
    login: "Логин",
    password: "Пароль",
    submit: "Войти",
    busy: "Вход...",
    services: "Услуги",
    contacts: "Контакты",
    call: "Позвонить",
    write: "Написать в Telegram",
    staff: "Сотрудникам",
  },
} as const;

function pick(site: PublicSite, field: "title" | "tagline" | "about" | "address" | "hours", lang: Lang): string {
  const other: Lang = lang === "uz" ? "ru" : "uz";
  return site[`${field}_${lang}`] || site[`${field}_${other}`] || "";
}

function pickList(site: PublicSite, field: "services" | "facts", lang: Lang): string[] {
  const own = site[`${field}_${lang}`];
  return own.length ? own : site[`${field}_${lang === "uz" ? "ru" : "uz"}`];
}

export default function LoginPage({ onLogin, note }: { onLogin: () => void; note?: string }) {
  const [site, setSite] = useState<PublicSite | null>(null);
  const [lang, setLang] = useState<Lang>(() => (location.pathname.replace(/\/$/, "") === "/ru" ? "ru" : "uz"));

  useEffect(() => {
    api
      .get<PublicSite>("/api/public/site")
      .then(setSite)
      .catch(() => setSite(null));
  }, []);

  useEffect(() => {
    document.documentElement.lang = lang;
    if (site?.configured) {
      const title = pick(site, "title", lang);
      const tagline = pick(site, "tagline", lang);
      document.title = title || (tagline ? `${site.name} — ${tagline}` : site.name);
    }
  }, [lang, site]);

  function switchLang(next: Lang) {
    setLang(next);
    try {
      history.replaceState(null, "", next === "ru" ? "/ru" : "/");
    } catch {
      /* e'tiborsiz */
    }
  }

  if (!site?.configured) {
    return (
      <div className="flex min-h-full items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-blue-900 p-4">
        <LoginCard lang="uz" onLogin={onLogin} note={note} brand={<SimpleBrand />} />
      </div>
    );
  }

  const t = T[lang];
  const facts = pickList(site, "facts", lang);
  const services = pickList(site, "services", lang);
  const tel = site.phone.replace(/[^\d+]/g, "");
  const address = pick(site, "address", lang);
  const hours = pick(site, "hours", lang);

  return (
    <div className="min-h-full bg-slate-50 text-slate-900">
      <header className="sticky top-0 z-20 border-b border-white/10 bg-slate-950/95 text-white backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center gap-3 px-4 py-3">
          <span className="text-lg font-bold tracking-tight">{site.name}</span>
          <div className="ml-auto flex items-center gap-1 rounded-lg bg-white/10 p-0.5 text-xs font-semibold">
            {(["uz", "ru"] as Lang[]).map((l) => (
              <button
                key={l}
                onClick={() => switchLang(l)}
                className={cx("rounded-md px-2.5 py-1 uppercase", lang === l ? "bg-white text-slate-900" : "text-white/70 hover:text-white")}
                aria-pressed={lang === l}
              >
                {l}
              </button>
            ))}
          </div>
          <a href="#kirish" className="hidden items-center gap-1.5 rounded-lg bg-blue-600 px-3 py-1.5 text-sm font-medium hover:bg-blue-500 sm:inline-flex">
            <LogIn size={15} /> {t.staff}
          </a>
        </div>
      </header>

      <section className="bg-gradient-to-br from-slate-950 via-slate-900 to-blue-950 text-white">
        <div className="mx-auto max-w-6xl px-4 pb-14 pt-12">
          <h1 className="max-w-3xl text-3xl font-bold leading-tight tracking-tight [text-wrap:balance] sm:text-5xl">
            {pick(site, "tagline", lang) || site.name}
          </h1>
          <p className="mt-5 max-w-2xl text-base leading-relaxed text-slate-300 sm:text-lg">{pick(site, "about", lang)}</p>
          <div className="mt-7 flex flex-wrap gap-3">
            {site.phone && (
              <a href={`tel:${tel}`} className="inline-flex items-center gap-2 rounded-xl bg-white px-4 py-2.5 font-semibold text-slate-900 hover:bg-slate-100">
                <Phone size={17} /> {site.phone}
              </a>
            )}
            {site.telegram && (
              <a
                href={site.telegram}
                target="_blank"
                rel="noopener"
                className="inline-flex items-center gap-2 rounded-xl border border-white/25 px-4 py-2.5 font-semibold text-white hover:bg-white/10"
              >
                <Send size={17} /> {t.write}
              </a>
            )}
          </div>
          {facts.length > 0 && (
            <dl className="mt-10 grid grid-cols-2 gap-3 sm:grid-cols-4">
              {facts.map((f) => {
                const m = f.match(/^([\d\s.,+]+[+%]?)\s+(.*)$/);
                return (
                  <div key={f} className="rounded-xl border border-white/10 bg-white/5 p-4">
                    {m ? (
                      <>
                        <dt className="text-2xl font-bold">{m[1].trim()}</dt>
                        <dd className="text-sm text-slate-300">{m[2]}</dd>
                      </>
                    ) : (
                      <dd className="text-sm text-slate-200">{f}</dd>
                    )}
                  </div>
                );
              })}
            </dl>
          )}
        </div>
      </section>

      <main className="mx-auto grid max-w-6xl gap-6 px-4 py-10 lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="order-2 space-y-6 lg:order-1">
          {services.length > 0 && (
            <section>
              <h2 className="mb-3 text-xl font-semibold">{t.services}</h2>
              <ul className="grid gap-2 sm:grid-cols-2">
                {services.map((s) => (
                  <li key={s} className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-[15px] shadow-sm">
                    {s}
                  </li>
                ))}
              </ul>
            </section>
          )}
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-3 text-xl font-semibold">{t.contacts}</h2>
            <ul className="space-y-2.5 text-[15px]">
              {site.phone && (
                <li className="flex items-center gap-2.5">
                  <Phone size={17} className="text-slate-400" />
                  <a href={`tel:${tel}`} className="font-medium text-blue-700 hover:underline">
                    {site.phone}
                  </a>
                </li>
              )}
              {address && (
                <li className="flex items-center gap-2.5">
                  <MapPin size={17} className="text-slate-400" /> {address}
                </li>
              )}
              {hours && (
                <li className="flex items-center gap-2.5">
                  <Clock size={17} className="text-slate-400" /> {hours}
                </li>
              )}
              {site.telegram && (
                <li className="flex items-center gap-2.5">
                  <Send size={17} className="text-slate-400" />
                  <a href={site.telegram} target="_blank" rel="noopener" className="text-blue-700 hover:underline">
                    {site.telegram.replace(/^https?:\/\/(t\.me\/)?/, "@")}
                  </a>
                </li>
              )}
              {site.instagram && (
                <li className="flex items-center gap-2.5">
                  <Globe size={17} className="text-slate-400" />
                  <a href={site.instagram} target="_blank" rel="noopener" className="text-blue-700 hover:underline">
                    Instagram
                  </a>
                </li>
              )}
              {site.website && (
                <li className="flex items-center gap-2.5">
                  <Globe size={17} className="text-slate-400" />
                  <a href={site.website} target="_blank" rel="noopener" className="text-blue-700 hover:underline">
                    {site.website.replace(/^https?:\/\//, "")}
                  </a>
                </li>
              )}
            </ul>
          </section>
        </div>
        <div id="kirish" className="order-1 scroll-mt-20 lg:order-2">
          <div className="lg:sticky lg:top-20">
            <LoginCard
              lang={lang}
              onLogin={onLogin}
              note={note}
              brand={
                <div>
                  <div className="text-lg font-semibold text-slate-900">{t.loginTitle}</div>
                  <div className="text-sm text-slate-500">{t.loginHint}</div>
                </div>
              }
            />
          </div>
        </div>
      </main>

      <footer className="border-t border-slate-200 py-6 text-center text-sm text-slate-500">
        © {new Date().getFullYear()} {site.name}
      </footer>
    </div>
  );
}

function SimpleBrand() {
  return (
    <div className="flex items-center gap-3">
      <img src="/favicon.svg" alt="" className="h-11 w-11" />
      <div>
        <div className="text-lg font-semibold text-slate-900">TelegramCRM</div>
        <div className="text-sm text-slate-500">Ish tizimiga kirish</div>
      </div>
    </div>
  );
}

function LoginCard({ lang, onLogin, note, brand }: { lang: Lang; onLogin: () => void; note?: string; brand: ReactNode }) {
  const t = T[lang];
  const [login, setLogin] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      await api.post("/api/auth/login", { login, password });
      onLogin();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <form onSubmit={submit} className="w-full max-w-sm rounded-2xl bg-white p-7 shadow-2xl ring-1 ring-slate-200 lg:max-w-none">
      <div className="mb-6">{brand}</div>
      {note && <div className="mb-4 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">{note}</div>}
      <div className="space-y-3">
        <Field label={t.login}>
          <Input id="login-name" value={login} onChange={(e) => setLogin(e.target.value)} autoComplete="username" autoCapitalize="none" />
        </Field>
        <Field label={t.password}>
          <Input id="login-password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
        </Field>
      </div>
      {error && <div className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
      <Button type="submit" className="mt-5 w-full" disabled={busy || !login || !password}>
        {busy ? t.busy : t.submit}
      </Button>
    </form>
  );
}
