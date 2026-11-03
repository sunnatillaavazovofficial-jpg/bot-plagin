import { ExternalLink, Merge, Plus, Search, Trash2, Users, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import OrderForm from "../components/OrderForm";
import { AccountTag, Avatar, Button, Drawer, Empty, Field, Input, Modal, Spinner, StatusPill, Textarea, cx } from "../components/ui";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { chatTime, formatDate, formatSum } from "../lib/format";
import type { Client, Order } from "../lib/types";

export default function ClientsPage() {
  const { clientId } = useParams();
  const navigate = useNavigate();
  const { isManager, toast } = useApp();
  const [clients, setClients] = useState<Client[]>([]);
  const [loading, setLoading] = useState(true);
  const [q, setQ] = useState("");
  const [onlyDebt, setOnlyDebt] = useState(false);
  const [creating, setCreating] = useState(false);
  const timer = useRef<number | undefined>(undefined);

  const load = useCallback(async () => {
    try {
      setClients(await api.get<Client[]>("/api/clients", { q: q || undefined, debt: onlyDebt || undefined }));
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setLoading(false);
    }
  }, [q, onlyDebt, toast]);

  useEffect(() => {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(load, q ? 300 : 0);
  }, [load, q]);

  if (!isManager) return <Empty title="Bu bo'lim uchun ruxsat yo'q" />;

  const totalDebt = clients.reduce((s, c) => s + (c.debt ?? 0), 0);

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="space-y-3 border-b border-slate-200 bg-white px-4 py-3 sm:px-6">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold text-slate-900">Mijozlar</h1>
          <span className="text-sm text-slate-500">
            {clients.length} ta{totalDebt > 0 && <span className="text-red-600"> · qarz {formatSum(totalDebt)}</span>}
          </span>
          <Button className="ml-auto" onClick={() => setCreating(true)}>
            <Plus size={16} /> <span className="hidden sm:inline">Yangi mijoz</span>
          </Button>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-48 flex-1 sm:max-w-sm">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Ism, telefon, kompaniya..." className="pl-9" />
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" checked={onlyDebt} onChange={(e) => setOnlyDebt(e.target.checked)} /> Faqat qarzdorlar
          </label>
        </div>
      </header>
      <div className="scroll-thin min-h-0 flex-1 overflow-auto p-4 sm:px-6">
        {loading ? (
          <div className="flex justify-center py-16">
            <Spinner />
          </div>
        ) : clients.length === 0 ? (
          <Empty icon={<Users size={40} />} title="Mijoz topilmadi" text="Chatda «Mijoz sifatida saqlash» yoki zakaz yaratilganda mijoz avtomatik qo'shiladi" />
        ) : (
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
            {clients.map((c) => (
              <button
                key={c.id}
                onClick={() => navigate(`/clients/${c.id}`)}
                className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-3 text-left shadow-sm transition hover:border-blue-300"
              >
                <Avatar name={c.name} size={42} />
                <div className="min-w-0 flex-1">
                  <div className="truncate font-medium text-slate-900">{c.name}</div>
                  <div className="truncate text-sm text-slate-500">{[c.phone, c.company].filter(Boolean).join(" · ") || "—"}</div>
                </div>
                <div className="shrink-0 text-right text-xs">
                  <div className="text-slate-500">{typeof c.orders === "number" ? c.orders : 0} ta zakaz</div>
                  {!!c.total && <div className="font-medium text-slate-700">{formatSum(c.total, false)}</div>}
                  {!!c.debt && <div className="font-medium text-red-600">qarz {formatSum(c.debt, false)}</div>}
                </div>
              </button>
            ))}
          </div>
        )}
      </div>
      <ClientDrawer clientId={clientId ? Number(clientId) : null} onClose={() => navigate("/clients")} onChanged={load} />
      <NewClientModal open={creating} onClose={() => setCreating(false)} onCreated={(c) => navigate(`/clients/${c.id}`)} />
    </div>
  );
}

function NewClientModal({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: (c: Client) => void }) {
  const { toast } = useApp();
  const [form, setForm] = useState({ name: "", phone: "", company: "", notes: "" });
  useEffect(() => {
    if (open) setForm({ name: "", phone: "", company: "", notes: "" });
  }, [open]);
  async function submit(e: FormEvent) {
    e.preventDefault();
    try {
      onCreated(await api.post<Client>("/api/clients", form));
      onClose();
    } catch (err) {
      toast((err as Error).message, "error");
    }
  }
  return (
    <Modal open={open} onClose={onClose} title="Yangi mijoz">
      <form onSubmit={submit} className="space-y-3">
        <Field label="Ism *">
          <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoFocus required />
        </Field>
        <Field label="Telefon">
          <Input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} placeholder="+998" inputMode="tel" />
        </Field>
        <Field label="Kompaniya">
          <Input value={form.company} onChange={(e) => setForm({ ...form, company: e.target.value })} />
        </Field>
        <Field label="Izoh">
          <Textarea rows={2} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
        </Field>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Bekor
          </Button>
          <Button type="submit">Saqlash</Button>
        </div>
      </form>
    </Modal>
  );
}

function ClientDrawer({ clientId, onClose, onChanged }: { clientId: number | null; onClose: () => void; onChanged: () => void }) {
  const { accountById, statusById, isAdmin, toast } = useApp();
  const navigate = useNavigate();
  const [client, setClient] = useState<Client | null>(null);
  const [form, setForm] = useState({ name: "", phone: "", company: "", notes: "" });
  const [merging, setMerging] = useState(false);
  const [mergeQ, setMergeQ] = useState("");
  const [mergeOptions, setMergeOptions] = useState<Client[]>([]);
  const [ordering, setOrdering] = useState(false);

  const load = useCallback(async () => {
    if (!clientId) return;
    try {
      const c = await api.get<Client>(`/api/clients/${clientId}`);
      setClient(c);
      setForm({ name: c.name, phone: c.phone ?? "", company: c.company ?? "", notes: c.notes ?? "" });
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }, [clientId, toast]);

  useEffect(() => {
    setClient(null);
    setMerging(false);
    load();
  }, [load]);

  useEffect(() => {
    if (!merging || mergeQ.length < 2) return setMergeOptions([]);
    const t = window.setTimeout(
      () => api.get<Client[]>("/api/clients", { q: mergeQ, limit: 8 }).then((l) => setMergeOptions(l.filter((c) => c.id !== clientId))),
      250,
    );
    return () => window.clearTimeout(t);
  }, [mergeQ, merging, clientId]);

  async function save() {
    try {
      await api.patch(`/api/clients/${clientId}`, form);
      toast("Saqlandi");
      onChanged();
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function merge(other: Client) {
    if (!window.confirm(`«${other.name}» shu mijozga qo'shilsinmi? Uning chat va zakazlari shu yerga o'tadi.`)) return;
    try {
      await api.post(`/api/clients/${clientId}/merge`, { other_id: other.id });
      setMerging(false);
      toast("Birlashtirildi");
      onChanged();
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function remove() {
    if (!window.confirm("Mijoz o'chirilsinmi? (Zakazlari o'chmaydi)")) return;
    await api.del(`/api/clients/${clientId}`);
    onChanged();
    onClose();
  }

  const orders = (Array.isArray(client?.orders) ? client?.orders : []) as Order[];
  const dirty =
    client &&
    (form.name !== client.name || form.phone !== (client.phone ?? "") || form.company !== (client.company ?? "") || form.notes !== (client.notes ?? ""));

  return (
    <Drawer open={clientId !== null} onClose={onClose}>
      {!client ? (
        <div className="flex justify-center p-10">
          <Spinner />
        </div>
      ) : (
        <div>
          <div className="sticky top-0 z-10 flex items-center gap-3 border-b border-slate-100 bg-white px-5 py-4">
            <Avatar name={client.name} size={44} />
            <div className="min-w-0 flex-1">
              <div className="truncate text-lg font-semibold text-slate-900">{client.name}</div>
              <div className="text-xs text-slate-400">Mijoz {formatDate(client.created_at, true)} dan beri</div>
            </div>
            <button onClick={onClose} className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100">
              <X size={20} />
            </button>
          </div>
          <div className="space-y-6 p-5">
            <div className="grid grid-cols-3 gap-2 text-center">
              <Stat label="Jami" value={formatSum(client.total ?? 0, false)} />
              <Stat label="To'langan" value={formatSum(client.paid ?? 0, false)} tone="ok" />
              <Stat label="Qarz" value={formatSum(client.debt ?? 0, false)} tone={client.debt ? "bad" : undefined} />
            </div>

            <section className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <Field label="Ism">
                  <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
                </Field>
                <Field label="Telefon">
                  <Input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} inputMode="tel" />
                </Field>
              </div>
              <Field label="Kompaniya">
                <Input value={form.company} onChange={(e) => setForm({ ...form, company: e.target.value })} />
              </Field>
              <Field label="Izoh">
                <Textarea rows={3} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} />
              </Field>
              {dirty && <Button onClick={save}>Saqlash</Button>}
            </section>

            <section>
              <h3 className="mb-2 text-sm font-semibold text-slate-900">Chatlar</h3>
              {(client.chats ?? []).length === 0 && <div className="text-sm text-slate-400">Chat bog'lanmagan</div>}
              <div className="space-y-1.5">
                {(client.chats ?? []).map((ch) => {
                  const acc = accountById(ch.account_id);
                  return (
                    <Link key={ch.id} to={`/chats/${ch.id}`} className="flex items-center gap-3 rounded-lg border border-slate-200 px-3 py-2 hover:bg-slate-50">
                      <div className="min-w-0 flex-1">
                        <div className="truncate text-sm font-medium">{ch.title}</div>
                        {acc && <AccountTag name={acc.name} color={acc.color} />}
                      </div>
                      <span className="text-xs text-slate-400">{chatTime(ch.last_message_at)}</span>
                      <ExternalLink size={14} className="text-slate-400" />
                    </Link>
                  );
                })}
              </div>
            </section>

            <section>
              <div className="mb-2 flex items-center justify-between">
                <h3 className="text-sm font-semibold text-slate-900">Zakazlar ({orders.length})</h3>
                <Button size="sm" variant="ghost" onClick={() => setOrdering(true)}>
                  <Plus size={14} /> Yangi
                </Button>
              </div>
              <div className="space-y-1.5">
                {orders.map((o) => (
                  <Link key={o.id} to={`/orders/${o.id}`} className="flex items-center gap-3 rounded-lg border border-slate-200 px-3 py-2 hover:bg-slate-50">
                    <span className="text-xs text-slate-400">{o.number}</span>
                    <span className="min-w-0 flex-1 truncate text-sm font-medium">{o.title}</span>
                    <StatusPill status={statusById(o.status_id)} />
                    <span className={cx("text-xs tabular-nums", o.debt ? "text-red-600" : "text-slate-500")}>{formatSum(o.price, false)}</span>
                  </Link>
                ))}
              </div>
            </section>

            <section className="flex flex-wrap gap-2 border-t border-slate-100 pt-4">
              <Button size="sm" variant="secondary" onClick={() => setMerging((v) => !v)}>
                <Merge size={14} /> Boshqa mijoz bilan birlashtirish
              </Button>
              {isAdmin && (
                <Button size="sm" variant="ghost" className="text-red-600" onClick={remove}>
                  <Trash2 size={14} /> O'chirish
                </Button>
              )}
              {merging && (
                <div className="relative w-full">
                  <Input value={mergeQ} onChange={(e) => setMergeQ(e.target.value)} placeholder="Qo'shiladigan mijozni qidiring..." autoFocus />
                  {mergeOptions.length > 0 && (
                    <div className="absolute inset-x-0 top-11 z-10 rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
                      {mergeOptions.map((c) => (
                        <button key={c.id} onClick={() => merge(c)} className="block w-full px-3 py-2 text-left text-sm hover:bg-slate-50">
                          {c.name} <span className="text-slate-400">{c.phone}</span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </section>
          </div>
          <OrderForm
            open={ordering}
            draft={{ client_id: client.id, client_name: client.name }}
            onClose={() => setOrdering(false)}
            onCreated={(o) => navigate(`/orders/${o.id}`)}
          />
        </div>
      )}
    </Drawer>
  );
}

function Stat({ label, value, tone }: { label: string; value: string; tone?: "ok" | "bad" }) {
  return (
    <div className="rounded-xl bg-slate-50 px-2 py-2.5">
      <div className="text-[11px] uppercase tracking-wide text-slate-400">{label}</div>
      <div className={cx("mt-0.5 text-sm font-semibold tabular-nums", tone === "ok" && "text-emerald-600", tone === "bad" && "text-red-600")}>
        {value}
      </div>
    </div>
  );
}
