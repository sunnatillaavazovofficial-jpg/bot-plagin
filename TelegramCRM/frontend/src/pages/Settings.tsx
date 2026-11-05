import { ArrowDown, ArrowUp, Bell, Check, KeyRound, ListChecks, Plus, RefreshCw, Smartphone, Trash2, UserCog, Users } from "lucide-react";
import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Button, Field, Input, Modal, Select, cx } from "../components/ui";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { formatDateTime, ROLE_LABELS } from "../lib/format";
import { KIND_LABELS, KINDS } from "../components/chatKinds";
import ChatPicker from "./ChatPicker";
import type { Account, Role, Status, UserItem } from "../lib/types";

const TABS = [
  { key: "profile", label: "Profil", icon: UserCog, admin: false },
  { key: "chats", label: "Chatlarni tanlash", icon: ListChecks, admin: false, manager: true },
  { key: "accounts", label: "Telegram akkauntlar", icon: Smartphone, admin: true },
  { key: "staff", label: "Xodimlar", icon: Users, admin: true },
  { key: "statuses", label: "Zakaz holatlari", icon: Check, admin: true },
  { key: "notify", label: "Eslatmalar", icon: Bell, admin: true },
];

export default function SettingsPage() {
  const { tab = "profile" } = useParams();
  const navigate = useNavigate();
  const { isAdmin, isManager } = useApp();
  const tabs = TABS.filter((t) => (t.admin ? isAdmin : "manager" in t ? isManager : true));
  const current = tabs.find((t) => t.key === tab) ?? tabs[0];

  return (
    <div className="scroll-thin h-full overflow-y-auto">
      <div className="mx-auto max-w-5xl p-4 sm:p-6">
        <h1 className="mb-4 text-xl font-semibold text-slate-900">Sozlamalar</h1>
        <div className="flex flex-col gap-5 md:flex-row">
          <nav className="flex gap-1 overflow-x-auto md:w-56 md:shrink-0 md:flex-col">
            {tabs.map((t) => (
              <button
                key={t.key}
                onClick={() => navigate(`/settings/${t.key}`)}
                className={cx(
                  "flex items-center gap-2.5 whitespace-nowrap rounded-lg px-3 py-2 text-left text-sm font-medium",
                  current.key === t.key ? "bg-white text-slate-900 shadow-sm ring-1 ring-slate-200" : "text-slate-600 hover:bg-white/60",
                )}
              >
                <t.icon size={16} /> {t.label}
              </button>
            ))}
          </nav>
          <div className="min-w-0 flex-1">
            {current.key === "profile" && <ProfileTab />}
            {current.key === "chats" && <ChatPicker />}
            {current.key === "accounts" && <AccountsTab />}
            {current.key === "staff" && <StaffTab />}
            {current.key === "statuses" && <StatusesTab />}
            {current.key === "notify" && <NotifyTab />}
          </div>
        </div>
      </div>
    </div>
  );
}

function Card({ title, children, action }: { title: string; children: ReactNode; action?: ReactNode }) {
  return (
    <section className="mb-5 rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
      <div className="mb-4 flex items-center justify-between gap-3">
        <h2 className="font-semibold text-slate-900">{title}</h2>
        {action}
      </div>
      {children}
    </section>
  );
}

/* ---------------- Profil ---------------- */

function ProfileTab() {
  const { me, settings, reloadMe, toast } = useApp();
  const [oldPw, setOldPw] = useState("");
  const [newPw, setNewPw] = useState("");

  async function link() {
    try {
      const r = await api.post<{ url: string }>("/api/users/me/link-code");
      window.open(r.url, "_blank", "noopener");
      toast("Telegram'da «Start» tugmasini bosing, keyin bu sahifani yangilang");
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function unlink() {
    await api.post("/api/users/me/unlink");
    reloadMe();
  }

  async function changePassword(e: FormEvent) {
    e.preventDefault();
    try {
      await api.post("/api/auth/password", { old_password: oldPw, new_password: newPw });
      setOldPw("");
      setNewPw("");
      toast("Parol o'zgartirildi");
    } catch (err) {
      toast((err as Error).message, "error");
    }
  }

  return (
    <>
      <Card title="Mening ma'lumotlarim">
        <div className="grid gap-2 text-sm sm:grid-cols-3">
          <div>
            <div className="text-slate-500">Ism</div>
            <div className="font-medium">{me.name}</div>
          </div>
          <div>
            <div className="text-slate-500">Login</div>
            <div className="font-medium">{me.login}</div>
          </div>
          <div>
            <div className="text-slate-500">Rol</div>
            <div className="font-medium">{ROLE_LABELS[me.role]}</div>
          </div>
        </div>
      </Card>
      <Card title="Eslatmalarni Telegram'da olish">
        {!settings?.bot.enabled ? (
          <p className="text-sm text-slate-500">Eslatma boti sozlanmagan. Administrator .env faylida BOT_TOKEN ni yozishi kerak.</p>
        ) : me.tg_linked ? (
          <div className="flex flex-wrap items-center gap-3 text-sm">
            <span className="rounded-lg bg-emerald-50 px-3 py-1.5 text-emerald-700">
              ✓ Bog'langan {me.tg_username ? `(@${me.tg_username})` : ""}
            </span>
            <Button variant="ghost" size="sm" onClick={unlink}>
              Uzish
            </Button>
          </div>
        ) : (
          <div className="flex flex-wrap items-center gap-3">
            <Button onClick={link}>Botni ulash (@{settings.bot.username})</Button>
            <Button variant="ghost" onClick={reloadMe}>
              <RefreshCw size={14} /> Tekshirish
            </Button>
          </div>
        )}
      </Card>
      <Card title="Parolni o'zgartirish">
        <form onSubmit={changePassword} className="grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <Field label="Eski parol">
            <Input type="password" value={oldPw} onChange={(e) => setOldPw(e.target.value)} autoComplete="current-password" />
          </Field>
          <Field label="Yangi parol (kamida 6 belgi)">
            <Input type="password" value={newPw} onChange={(e) => setNewPw(e.target.value)} autoComplete="new-password" />
          </Field>
          <Button type="submit" disabled={!oldPw || newPw.length < 6}>
            <KeyRound size={15} /> O'zgartirish
          </Button>
        </form>
      </Card>
    </>
  );
}

/* ---------------- Akkauntlar ---------------- */

function AccountsTab() {
  const { accounts, settings, reloadAccounts, toast } = useApp();
  const [adding, setAdding] = useState(false);

  useEffect(() => {
    reloadAccounts().catch(() => undefined);
  }, [reloadAccounts]);

  async function action(fn: () => Promise<unknown>, ok: string) {
    try {
      await fn();
      toast(ok);
      reloadAccounts();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  return (
    <Card
      title="Telegram akkauntlar"
      action={
        <Button onClick={() => setAdding(true)} disabled={settings ? !settings.telegram_configured : false}>
          <Plus size={16} /> Akkaunt qo'shish
        </Button>
      }
    >
      {settings && !settings.telegram_configured && (
        <div className="mb-4 rounded-xl bg-amber-50 p-4 text-sm text-amber-900">
          <b>Telegram API sozlanmagan.</b> my.telegram.org saytida «API development tools» bo'limidan <code>api_id</code> va{" "}
          <code>api_hash</code> oling, ularni serverdagi <code>.env</code> fayliga <code>TG_API_ID</code> va <code>TG_API_HASH</code> qilib yozing va
          saytni qayta ishga tushiring.
        </div>
      )}
      {settings?.fake_mode && (
        <div className="mb-4 rounded-xl bg-blue-50 p-3 text-sm text-blue-900">
          Sinov rejimi: haqiqiy Telegram'ga ulanmaydi. Yangi akkaunt qo'shishda kod — <b>12345</b>, raqam 0 bilan tugasa parol — <b>parol</b>.
        </div>
      )}
      {accounts.length === 0 && <div className="py-6 text-center text-sm text-slate-500">Hali akkaunt ulanmagan</div>}
      <div className="space-y-3">
        {accounts.map((a) => (
          <AccountRow key={a.id} account={a} onAction={action} />
        ))}
      </div>
      <p className="mt-4 text-xs text-slate-500">
        Xavfsizlik: akkaunt sessiyalari serverda shifrlangan holda saqlanadi. Telegram'dagi kirish kodlari (777000) va «Saqlangan xabarlar»
        saytda ko'rsatilmaydi. Telefoningizdagi Sozlamalar → Qurilmalar bo'limida «TelegramCRM» sessiyasini istalgan vaqt tugatishingiz mumkin.
      </p>
      <AddAccountModal open={adding} onClose={() => setAdding(false)} onDone={() => reloadAccounts()} />
    </Card>
  );
}

function AccountRow({ account: a, onAction }: { account: Account; onAction: (fn: () => Promise<unknown>, ok: string) => void }) {
  const [name, setName] = useState(a.name);
  useEffect(() => setName(a.name), [a.name]);
  const statusBadge =
    a.status === "logged_out" ? (
      <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-500">Chiqilgan</span>
    ) : a.status === "error" ? (
      <span className="rounded-full bg-red-50 px-2 py-0.5 text-xs text-red-600">Xato</span>
    ) : a.connected ? (
      <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs text-emerald-700">Ulangan</span>
    ) : (
      <span className="rounded-full bg-amber-50 px-2 py-0.5 text-xs text-amber-700">Ulanmoqda...</span>
    );

  return (
    <div className="rounded-xl border border-slate-200 p-4">
      <div className="flex flex-wrap items-center gap-3">
        <input
          type="color"
          value={a.color}
          onChange={(e) => onAction(() => api.patch(`/api/accounts/${a.id}`, { color: e.target.value }), "Rang saqlandi")}
          className="h-9 w-9 cursor-pointer rounded-lg border border-slate-200 p-0.5"
          title="Akkaunt rangi"
        />
        <div className="min-w-0 flex-1">
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onBlur={() => name.trim() && name !== a.name && onAction(() => api.patch(`/api/accounts/${a.id}`, { name }), "Nomi saqlandi")}
            className="-ml-1 w-full rounded-md border border-transparent px-1 font-medium text-slate-900 outline-none hover:border-slate-200 focus:border-blue-500"
          />
          <div className="text-sm text-slate-500">
            {a.phone} {a.username && `· @${a.username}`}
          </div>
        </div>
        {statusBadge}
      </div>
      {a.last_error && <div className="mt-2 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{a.last_error}</div>}
      <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-600">
        <span className="text-xs font-medium text-slate-500">Yangi chatlar avtomatik ko'rinsin:</span>
        {KINDS.map((k) => {
          const field = k === "private" ? "show_private" : k === "group" ? "show_groups" : k === "channel" ? "show_channels" : "show_bots";
          return (
            <label key={k} className="flex items-center gap-1.5">
              <input
                type="checkbox"
                checked={a[field]}
                onChange={(e) => onAction(() => api.patch(`/api/accounts/${a.id}`, { [field]: e.target.checked }), "Saqlandi")}
              />
              {KIND_LABELS[k]}
            </label>
          );
        })}
      </div>
      <div className="mt-3 flex flex-wrap items-center justify-end gap-2">
        {a.synced_at && <span className="text-xs text-slate-400">Sinxron: {formatDateTime(a.synced_at)}</span>}
        {a.status !== "logged_out" && (
          <Button size="sm" variant="secondary" onClick={() => onAction(() => api.post(`/api/accounts/${a.id}/resync`), "Sinxronlash boshlandi")}>
            <RefreshCw size={14} /> Sinxronlash
          </Button>
        )}
        {a.status !== "logged_out" && (
          <Button
            size="sm"
            variant="secondary"
            onClick={() =>
              window.confirm("Akkauntdan chiqilsinmi? Yozishmalar tarixi saytda qoladi.") &&
              onAction(() => api.post(`/api/accounts/${a.id}/logout`), "Akkauntdan chiqildi")
            }
          >
            Chiqish
          </Button>
        )}
        <Button
          size="sm"
          variant="ghost"
          className="text-red-600"
          onClick={() =>
            window.confirm("Akkaunt va uning BARCHA yozishmalari saytdan o'chirilsinmi? (Zakazlar qoladi)") &&
            onAction(() => api.del(`/api/accounts/${a.id}`), "Akkaunt o'chirildi")
          }
        >
          <Trash2 size={14} />
        </Button>
      </div>
    </div>
  );
}

function AddAccountModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const { toast } = useApp();
  const [step, setStep] = useState<"phone" | "code" | "password">("phone");
  const [phone, setPhone] = useState("+998");
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [loginId, setLoginId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (open) {
      setStep("phone");
      setPhone("+998");
      setCode("");
      setPassword("");
      setError("");
    }
  }, [open]);

  async function run(fn: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await fn();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  function finish() {
    toast("Akkaunt ulandi. Chatlar yuklanmoqda...");
    onDone();
    onClose();
  }

  return (
    <Modal open={open} onClose={onClose} title="Telegram akkaunt qo'shish">
      {step === "phone" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              const r = await api.post<{ login_id: string }>("/api/accounts/login/start", { phone });
              setLoginId(r.login_id);
              setStep("code");
            });
          }}
          className="space-y-3"
        >
          <Field label="Telefon raqam" hint="Telegram kodni shu akkauntning Telegram ilovasiga yuboradi">
            <Input value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" autoFocus />
          </Field>
          {error && <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
          <Button type="submit" className="w-full" disabled={busy || phone.replace(/\D/g, "").length < 9}>
            {busy ? "Yuborilmoqda..." : "Kod yuborish"}
          </Button>
        </form>
      )}
      {step === "code" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              const r = await api.post<{ status: string }>("/api/accounts/login/code", { login_id: loginId, code });
              if (r.status === "password") setStep("password");
              else finish();
            });
          }}
          className="space-y-3"
        >
          <p className="text-sm text-slate-600">
            <b>{phone}</b> raqamli Telegram ilovasiga kelgan kodni kiriting.
          </p>
          <Field label="Kod">
            <Input value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" autoFocus autoComplete="one-time-code" />
          </Field>
          {error && <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
          <Button type="submit" className="w-full" disabled={busy || code.length < 4}>
            {busy ? "Tekshirilmoqda..." : "Tasdiqlash"}
          </Button>
          <button type="button" onClick={() => setStep("phone")} className="w-full text-center text-sm text-slate-500 hover:underline">
            Raqamni o'zgartirish
          </button>
        </form>
      )}
      {step === "password" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            run(async () => {
              await api.post("/api/accounts/login/password", { login_id: loginId, password });
              finish();
            });
          }}
          className="space-y-3"
        >
          <p className="text-sm text-slate-600">Bu akkauntda ikki bosqichli himoya (bulutli parol) yoqilgan.</p>
          <Field label="Bulutli parol">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoFocus />
          </Field>
          {error && <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
          <Button type="submit" className="w-full" disabled={busy || !password}>
            {busy ? "Tekshirilmoqda..." : "Kirish"}
          </Button>
        </form>
      )}
    </Modal>
  );
}

/* ---------------- Xodimlar ---------------- */

function StaffTab() {
  const { users, accounts, reloadUsers, me } = useApp();
  const [editing, setEditing] = useState<UserItem | "new" | null>(null);

  useEffect(() => {
    reloadUsers().catch(() => undefined);
  }, [reloadUsers]);

  return (
    <Card
      title="Xodimlar"
      action={
        <Button onClick={() => setEditing("new")}>
          <Plus size={16} /> Xodim qo'shish
        </Button>
      }
    >
      <div className="mb-4 grid gap-2 rounded-xl bg-slate-50 p-3 text-xs text-slate-600 sm:grid-cols-3">
        <div>
          <b className="text-slate-800">Admin</b> — hammasi: akkauntlar, xodimlar, hisobot.
        </div>
        <div>
          <b className="text-slate-800">Menejer</b> — ruxsat berilgan akkauntlar chatlari, zakazlar, mijozlar.
        </div>
        <div>
          <b className="text-slate-800">Xodim</b> — faqat o'ziga biriktirilgan zakazlar (holat, izoh, fayl).
        </div>
      </div>
      <div className="divide-y divide-slate-100">
        {users.map((u) => (
          <div key={u.id} className={cx("flex flex-wrap items-center gap-3 py-3", !u.is_active && "opacity-50")}>
            <div className="min-w-0 flex-1">
              <div className="font-medium text-slate-900">
                {u.name} {u.id === me.id && <span className="text-xs text-slate-400">(siz)</span>}
              </div>
              <div className="text-sm text-slate-500">
                {u.login} · {ROLE_LABELS[u.role]} {!u.is_active && "· o'chirilgan"} {u.tg_linked && "· 🔔 bot ulangan"}
              </div>
            </div>
            <div className="flex flex-wrap gap-1">
              {u.role === "admin" ? (
                <span className="text-xs text-slate-400">barcha akkauntlar</span>
              ) : (
                (u.account_ids ?? []).map((id) => {
                  const acc = accounts.find((a) => a.id === id);
                  return acc ? (
                    <span key={id} className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                      <span className="h-1.5 w-1.5 rounded-full" style={{ background: acc.color }} />
                      {acc.name}
                    </span>
                  ) : null;
                })
              )}
            </div>
            <Button size="sm" variant="secondary" onClick={() => setEditing(u)}>
              Tahrirlash
            </Button>
          </div>
        ))}
      </div>
      <UserModal user={editing} onClose={() => setEditing(null)} onSaved={() => reloadUsers()} />
    </Card>
  );
}

function UserModal({ user, onClose, onSaved }: { user: UserItem | "new" | null; onClose: () => void; onSaved: () => void }) {
  const { accounts, toast } = useApp();
  const isNew = user === "new";
  const [form, setForm] = useState({ name: "", login: "", password: "", role: "menejer" as Role, is_active: true, account_ids: [] as number[] });

  useEffect(() => {
    if (user === "new") setForm({ name: "", login: "", password: "", role: "menejer", is_active: true, account_ids: [] });
    else if (user)
      setForm({ name: user.name, login: user.login ?? "", password: "", role: user.role, is_active: user.is_active, account_ids: user.account_ids ?? [] });
  }, [user]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    try {
      if (isNew) await api.post("/api/users", form);
      else if (user) {
        const body: Record<string, unknown> = { ...form };
        if (!form.password) delete body.password;
        await api.patch(`/api/users/${user.id}`, body);
      }
      toast("Saqlandi");
      onSaved();
      onClose();
    } catch (err) {
      toast((err as Error).message, "error");
    }
  }

  function toggleAccount(id: number) {
    setForm((f) => ({ ...f, account_ids: f.account_ids.includes(id) ? f.account_ids.filter((x) => x !== id) : [...f.account_ids, id] }));
  }

  return (
    <Modal open={user !== null} onClose={onClose} title={isNew ? "Yangi xodim" : "Xodimni tahrirlash"}>
      <form onSubmit={submit} className="space-y-3">
        <Field label="Ism">
          <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required autoFocus />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Login">
            <Input value={form.login} onChange={(e) => setForm({ ...form, login: e.target.value })} required autoCapitalize="none" />
          </Field>
          <Field label={isNew ? "Parol" : "Yangi parol"} hint={isNew ? "kamida 6 belgi" : "o'zgartirmasangiz bo'sh qoldiring"}>
            <Input value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required={isNew} autoComplete="new-password" />
          </Field>
        </div>
        <Field label="Rol">
          <Select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as Role })}>
            <option value="menejer">Menejer</option>
            <option value="xodim">Xodim</option>
            <option value="admin">Admin</option>
          </Select>
        </Field>
        {form.role !== "admin" && (
          <div>
            <div className="mb-1 text-xs font-medium text-slate-500">Qaysi akkauntlar chatlarini ko'radi</div>
            <div className="flex flex-wrap gap-2">
              {accounts.map((a) => (
                <label
                  key={a.id}
                  className={cx(
                    "flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-1.5 text-sm",
                    form.account_ids.includes(a.id) ? "border-blue-500 bg-blue-50" : "border-slate-200",
                  )}
                >
                  <input type="checkbox" checked={form.account_ids.includes(a.id)} onChange={() => toggleAccount(a.id)} />
                  <span className="h-2 w-2 rounded-full" style={{ background: a.color }} />
                  {a.name}
                </label>
              ))}
              {accounts.length === 0 && <span className="text-sm text-slate-400">Akkaunt yo'q</span>}
            </div>
          </div>
        )}
        {!isNew && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.is_active} onChange={(e) => setForm({ ...form, is_active: e.target.checked })} />
            Faol (o'chirilsa tizimga kira olmaydi)
          </label>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Bekor
          </Button>
          <Button type="submit">Saqlash</Button>
        </div>
      </form>
    </Modal>
  );
}

/* ---------------- Holatlar ---------------- */

type StatusDraft = Omit<Status, "id" | "position"> & { id: number | null; key: string };

function StatusesTab() {
  const { statuses, reloadStatuses, toast } = useApp();
  const [items, setItems] = useState<StatusDraft[]>([]);

  useEffect(() => {
    setItems(statuses.map((s) => ({ ...s, key: String(s.id) })));
  }, [statuses]);

  function update(i: number, patch: Partial<StatusDraft>) {
    setItems((list) => list.map((s, idx) => (idx === i ? { ...s, ...patch } : s)));
  }

  function move(i: number, dir: -1 | 1) {
    setItems((list) => {
      const copy = [...list];
      const j = i + dir;
      if (j < 0 || j >= copy.length) return list;
      [copy[i], copy[j]] = [copy[j], copy[i]];
      return copy;
    });
  }

  async function save() {
    try {
      await api.put("/api/statuses", items.map(({ key: _key, ...s }) => s));
      await reloadStatuses();
      toast("Holatlar saqlandi");
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  return (
    <Card title="Zakaz holatlari (doska ustunlari)" action={<Button onClick={save}>Saqlash</Button>}>
      <div className="space-y-2">
        {items.map((s, i) => (
          <div key={s.key} className="flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 p-2">
            <input type="color" value={s.color} onChange={(e) => update(i, { color: e.target.value })} className="h-9 w-9 rounded-lg border border-slate-200 p-0.5" />
            <Input value={s.name} onChange={(e) => update(i, { name: e.target.value })} className="w-48 flex-1" />
            <label className="flex items-center gap-1.5 text-xs text-slate-600">
              <input type="checkbox" checked={s.is_done} onChange={(e) => update(i, { is_done: e.target.checked, is_cancel: e.target.checked ? false : s.is_cancel })} />
              Yakunlangan
            </label>
            <label className="flex items-center gap-1.5 text-xs text-slate-600">
              <input type="checkbox" checked={s.is_cancel} onChange={(e) => update(i, { is_cancel: e.target.checked, is_done: e.target.checked ? false : s.is_done })} />
              Bekor
            </label>
            <div className="flex">
              <button onClick={() => move(i, -1)} className="rounded p-1.5 text-slate-400 hover:bg-slate-100" title="Yuqoriga">
                <ArrowUp size={15} />
              </button>
              <button onClick={() => move(i, 1)} className="rounded p-1.5 text-slate-400 hover:bg-slate-100" title="Pastga">
                <ArrowDown size={15} />
              </button>
              <button onClick={() => setItems((l) => l.filter((_, idx) => idx !== i))} className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600" title="O'chirish">
                <Trash2 size={15} />
              </button>
            </div>
          </div>
        ))}
      </div>
      <Button
        variant="ghost"
        className="mt-3"
        onClick={() =>
          setItems((l) => [...l, { id: null, key: `new-${Date.now()}`, name: "Yangi holat", color: "#64748b", is_done: false, is_cancel: false }])
        }
      >
        <Plus size={15} /> Holat qo'shish
      </Button>
      <p className="mt-3 text-xs text-slate-500">
        «Yakunlangan» holatdagi zakazlar topshirilgan hisoblanadi (hisobotda), «Bekor» holatdagilar summasi hisobga kirmaydi. Zakazi bor holatni o'chirib
        bo'lmaydi.
      </p>
    </Card>
  );
}

/* ---------------- Eslatmalar ---------------- */

function NotifyTab() {
  const { settings, reloadSettings, toast } = useApp();
  const [values, setValues] = useState<Record<string, string>>({});

  useEffect(() => {
    if (settings) setValues(settings.values);
  }, [settings]);

  const set = (k: string, v: string) => setValues((s) => ({ ...s, [k]: v }));
  const flag = (k: string) => values[k] === "1";

  async function save() {
    try {
      await api.put("/api/settings", values);
      await reloadSettings();
      toast("Saqlandi");
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function backup() {
    try {
      const r = await api.post<{ file: string }>("/api/settings/backup");
      toast(`Zaxira nusxa: ${r.file}`);
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  if (!settings) return null;
  return (
    <>
      <Card title="Eslatma boti" action={<Button onClick={save}>Saqlash</Button>}>
        <div
          className={cx(
            "mb-4 rounded-xl p-3 text-sm",
            settings.bot.enabled ? "bg-emerald-50 text-emerald-800" : "bg-amber-50 text-amber-900",
          )}
        >
          {settings.bot.enabled
            ? `Bot ishlayapti: @${settings.bot.username}. Har bir xodim Profil bo'limidan botni ulaydi.`
            : settings.bot.error ?? "Bot sozlanmagan: @BotFather orqali bot yarating va tokenini .env faylida BOT_TOKEN ga yozing."}
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Kompaniya nomi">
            <Input value={values.company_name ?? ""} onChange={(e) => set("company_name", e.target.value)} />
          </Field>
          <Field label="Kunlik hisobot vaqti">
            <Input type="time" value={values.daily_time ?? "09:00"} onChange={(e) => set("daily_time", e.target.value)} />
          </Field>
          <Field label="Javobsiz chat eslatmasi (daqiqa)">
            <Input type="number" min={5} max={1440} value={values.unanswered_minutes ?? "30"} onChange={(e) => set("unanswered_minutes", e.target.value)} />
          </Field>
        </div>
        <div className="mt-4 space-y-2 text-sm">
          {[
            ["notify_unanswered", "Javob berilmagan chatlar haqida eslatish"],
            ["notify_deadline", "Muddati yaqin va o'tgan zakazlar (har kuni hisobot vaqtida)"],
            ["notify_assign", "Xodimga zakaz biriktirilganda xabar berish"],
            ["notify_daily", "Adminlarga har kungi qisqa hisobot"],
          ].map(([k, label]) => (
            <label key={k} className="flex items-center gap-2">
              <input type="checkbox" checked={flag(k)} onChange={(e) => set(k, e.target.checked ? "1" : "0")} />
              {label}
            </label>
          ))}
        </div>
      </Card>
      <Card title="Zaxira nusxa" action={<Button variant="secondary" onClick={backup}>Hozir nusxa olish</Button>}>
        <p className="text-sm text-slate-600">
          Baza har kuni soat 03:00 da avtomatik nusxalanadi (serverdagi <code>data/backups</code> papkasi, oxirgi 14 kun saqlanadi).
        </p>
      </Card>
    </>
  );
}
