import { ArrowDownLeft, ArrowRightLeft, ArrowUpRight, Download, Pencil, Plus, Trash2, Wallet } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { SumInput } from "../components/OrderForm";
import PeriodPicker from "../components/PeriodPicker";
import { Button, Empty, Field, Input, Modal, Select, Spinner, Textarea, cx } from "../components/ui";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { formatDate, formatSum, todayISO } from "../lib/format";
import { useLive } from "../lib/live";
import { defaultPeriod } from "../lib/periods";
import type { Category, Debt, FinanceSummary, OrderDebt, Transaction, TxKind } from "../lib/types";

const METHODS: Record<string, string> = { naqd: "Naqd", karta: "Karta", "o'tkazma": "Bank o'tkazmasi", boshqa: "Boshqa" };
const KIND_LABEL: Record<TxKind, string> = { kirim: "Kirim", chiqim: "Chiqim", otkazma: "O'tkazma" };
const SERIES = "#2a78d6";

type Tab = "kassa" | "qarzlar" | "toifalar";

export default function FinancePage() {
  const { tab = "kassa" } = useParams();
  const navigate = useNavigate();
  const { isManager } = useApp();
  const [categories, setCategories] = useState<Category[]>([]);

  const loadCategories = useCallback(() => {
    api.get<Category[]>("/api/finance/categories").then(setCategories).catch(() => undefined);
  }, []);
  useEffect(loadCategories, [loadCategories]);

  if (!isManager) return <Empty title="Kassa faqat admin va menejerlar uchun" />;
  const current = (["kassa", "qarzlar", "toifalar"].includes(tab) ? tab : "kassa") as Tab;

  return (
    <div className="scroll-thin h-full overflow-y-auto">
      <div className="mx-auto max-w-6xl space-y-5 p-4 sm:p-6">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="mr-auto flex items-center gap-2 text-xl font-semibold text-slate-900">
            <Wallet size={22} /> Kassa va hisob-kitob
          </h1>
          <div className="flex gap-1 rounded-lg bg-slate-100 p-1">
            {(
              [
                ["kassa", "Kirim-chiqim"],
                ["qarzlar", "Qarz daftari"],
                ["toifalar", "Toifalar"],
              ] as [Tab, string][]
            ).map(([key, label]) => (
              <button
                key={key}
                onClick={() => navigate(`/finance/${key}`)}
                className={cx(
                  "rounded-md px-3 py-1.5 text-sm font-medium",
                  current === key ? "bg-white text-slate-900 shadow-sm" : "text-slate-500 hover:text-slate-700",
                )}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
        {current === "kassa" && <CashTab categories={categories} />}
        {current === "qarzlar" && <DebtsTab />}
        {current === "toifalar" && <CategoriesTab categories={categories} reload={loadCategories} />}
      </div>
    </div>
  );
}

/* ================= Kirim-chiqim ================= */

function CashTab({ categories }: { categories: Category[] }) {
  const { toast } = useApp();
  const [preset, setPreset] = useState("month");
  const [range, setRange] = useState<[string, string]>(defaultPeriod);
  const [summary, setSummary] = useState<FinanceSummary | null>(null);
  const [items, setItems] = useState<Transaction[]>([]);
  const [total, setTotal] = useState(0);
  const [filters, setFilters] = useState({ kind: "", category_id: "", method: "", q: "" });
  const [editing, setEditing] = useState<Transaction | null>(null);
  const [loading, setLoading] = useState(true);
  const countRef = useRef(0);
  countRef.current = items.length;

  const load = useCallback(
    async (append = false) => {
      try {
        const [s, list] = await Promise.all([
          api.get<FinanceSummary>("/api/finance/summary", { date_from: range[0], date_to: range[1] }),
          api.get<{ total: number; items: Transaction[] }>("/api/finance/transactions", {
            date_from: range[0],
            date_to: range[1],
            kind: filters.kind || undefined,
            category_id: filters.category_id || undefined,
            method: filters.method || undefined,
            q: filters.q || undefined,
            limit: 100,
            offset: append ? countRef.current : 0,
          }),
        ]);
        setSummary(s);
        setItems((prev) => (append ? [...prev, ...list.items] : list.items));
        setTotal(list.total);
      } catch (e) {
        toast((e as Error).message, "error");
      } finally {
        setLoading(false);
      }
    },
    [range, filters, toast],
  );

  useEffect(() => {
    const t = window.setTimeout(() => load(), filters.q ? 300 : 0);
    return () => window.clearTimeout(t);
  }, [load, filters.q]);

  useLive((e) => {
    if (e.type === "finance.update") load();
  });

  async function remove(tx: Transaction) {
    if (!window.confirm(`${KIND_LABEL[tx.kind]} ${formatSum(tx.amount)} o'chirilsinmi?`)) return;
    try {
      await api.del(`/api/finance/transactions/${tx.id}`);
      toast("O'chirildi");
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  const b = summary?.balances ?? {};
  return (
    <div className="space-y-5">
      <PeriodPicker
        preset={preset}
        range={range}
        onChange={(p, r) => {
          setPreset(p);
          setRange(r);
        }}
      />
      <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <div className="col-span-2 rounded-2xl bg-slate-900 p-5 text-white shadow-sm lg:col-span-1 lg:row-span-2">
          <div className="text-sm text-slate-300">Kassa qoldig'i (jami)</div>
          <div className={cx("mt-1 text-4xl font-semibold tracking-tight", (b.jami ?? 0) < 0 && "text-red-300")}>
            {formatSum(b.jami ?? 0, false)}
          </div>
          <div className="text-sm text-slate-400">so'm</div>
          <div className="mt-4 space-y-1.5 border-t border-white/10 pt-3 text-sm">
            {Object.entries(METHODS).map(([key, label]) =>
              key === "naqd" || key === "karta" || b[key] ? (
                <div key={key} className="flex justify-between">
                  <span className="text-slate-300">{label}</span>
                  <span className="font-medium tabular-nums">{formatSum(b[key] ?? 0, false)}</span>
                </div>
              ) : null,
            )}
          </div>
        </div>
        <Tile label="Kirim (davr)" value={summary?.totals.kirim ?? 0} tone="in" />
        <Tile label="Chiqim (davr)" value={summary?.totals.chiqim ?? 0} tone="out" />
        <Tile label="Foyda (davr)" value={summary?.totals.foyda ?? 0} tone={(summary?.totals.foyda ?? 0) < 0 ? "out" : "in"} />
        <Tile label="Bizdan qarzdorlar" value={(summary?.debts.bizga ?? 0) + (summary?.debts.zakazlar ?? 0)} link="/finance/qarzlar" />
        <Tile label="Biz qarzdormiz" value={summary?.debts.bizdan ?? 0} link="/finance/qarzlar" />
        <div className="col-span-2 flex items-center rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200 lg:col-span-1">
          <a
            href={`/api/finance/export.xlsx?date_from=${range[0]}&date_to=${range[1]}`}
            className="flex w-full items-center justify-center gap-2 rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-emerald-700"
          >
            <Download size={16} /> Excel'ga yuklab olish
          </a>
        </div>
      </section>

      <TxForm categories={categories} onSaved={() => load()} />

      {summary && (summary.chiqim_by_category.length > 0 || summary.kirim_by_category.length > 0) && (
        <div className="grid gap-5 lg:grid-cols-2">
          <CategoryBars title="Chiqimlar toifalar bo'yicha" rows={summary.chiqim_by_category} />
          <CategoryBars title="Kirimlar toifalar bo'yicha" rows={summary.kirim_by_category} />
        </div>
      )}

      <section className="rounded-2xl bg-white shadow-sm ring-1 ring-slate-200">
        <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 p-4">
          <h2 className="mr-auto font-semibold text-slate-900">Yozuvlar ({total})</h2>
          <Input
            value={filters.q}
            onChange={(e) => setFilters({ ...filters, q: e.target.value })}
            placeholder="Izoh, mijoz yoki #zakaz..."
            className="h-9 w-56"
          />
          <Select value={filters.kind} onChange={(e) => setFilters({ ...filters, kind: e.target.value })} className="h-9 w-auto">
            <option value="">Barcha turlar</option>
            <option value="kirim">Kirim</option>
            <option value="chiqim">Chiqim</option>
            <option value="otkazma">O'tkazma</option>
          </Select>
          <Select value={filters.category_id} onChange={(e) => setFilters({ ...filters, category_id: e.target.value })} className="h-9 w-auto">
            <option value="">Barcha toifalar</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.kind === "kirim" ? "＋" : "−"} {c.name}
              </option>
            ))}
          </Select>
          <Select value={filters.method} onChange={(e) => setFilters({ ...filters, method: e.target.value })} className="h-9 w-auto">
            <option value="">Naqd va karta</option>
            {Object.entries(METHODS).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </Select>
        </div>
        {loading ? (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        ) : items.length === 0 ? (
          <div className="py-10 text-center text-sm text-slate-500">Bu davrda yozuv yo'q</div>
        ) : (
          <TxTable items={items} onEdit={setEditing} onDelete={remove} />
        )}
        {items.length < total && (
          <button onClick={() => load(true)} className="w-full border-t border-slate-100 py-3 text-sm text-blue-600 hover:bg-slate-50">
            Yana yuklash
          </button>
        )}
      </section>

      <Modal open={editing !== null} onClose={() => setEditing(null)} title="Yozuvni tahrirlash" wide>
        {editing && (
          <TxForm
            categories={categories}
            initial={editing}
            onSaved={() => {
              setEditing(null);
              load();
            }}
          />
        )}
      </Modal>
    </div>
  );
}

function Tile({ label, value, tone, link }: { label: string; value: number; tone?: "in" | "out"; link?: string }) {
  const body = (
    <>
      <div className="text-sm text-slate-500">{label}</div>
      <div className={cx("mt-1 text-xl font-semibold sm:text-2xl", tone === "in" && "text-emerald-700", tone === "out" && "text-red-600", !tone && "text-slate-900")}>
        {formatSum(value, false)}
      </div>
      <div className="text-xs text-slate-400">so'm</div>
    </>
  );
  return link ? (
    <Link to={link} className="rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200 hover:ring-blue-300">
      {body}
    </Link>
  ) : (
    <div className="rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200">{body}</div>
  );
}

function TxTable({ items, onEdit, onDelete }: { items: Transaction[]; onEdit: (t: Transaction) => void; onDelete: (t: Transaction) => void }) {
  const { userName, me } = useApp();
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[720px] text-sm">
        <thead className="text-left text-xs text-slate-500">
          <tr className="border-b border-slate-100">
            <th className="px-4 py-2 font-medium">Sana</th>
            <th className="px-2 py-2 font-medium">Toifa</th>
            <th className="px-2 py-2 font-medium">Izoh</th>
            <th className="px-2 py-2 font-medium">Usul</th>
            <th className="px-2 py-2 text-right font-medium">Summa</th>
            <th className="px-2 py-2 font-medium">Kim</th>
            <th className="w-16 px-2 py-2" />
          </tr>
        </thead>
        <tbody>
          {items.map((t) => {
            const canEdit = me.role === "admin" || t.created_by_id === me.id;
            return (
              <tr key={t.id} className="group border-b border-slate-50 hover:bg-slate-50/60">
                <td className="whitespace-nowrap px-4 py-2 text-slate-600">{formatDate(t.date)}</td>
                <td className="px-2 py-2">
                  {t.kind === "otkazma" ? (
                    <span className="inline-flex items-center gap-1 text-slate-600">
                      <ArrowRightLeft size={14} /> O'tkazma
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 text-slate-800">
                      <span className="h-2 w-2 rounded-full" style={{ background: t.category_color ?? "#94a3b8" }} />
                      {t.category_name ?? "Toifasiz"}
                    </span>
                  )}
                </td>
                <td className="max-w-xs px-2 py-2 text-slate-700">
                  <div className="truncate">{t.note}</div>
                  <div className="flex gap-2 text-xs">
                    {t.order_id && (
                      <Link to={`/orders/${t.order_id}`} className="text-blue-600 hover:underline">
                        {t.order_number}
                      </Link>
                    )}
                    {t.client_name && <span className="text-slate-500">{t.client_name}</span>}
                    {t.source === "bot" && <span className="text-slate-400">bot orqali</span>}
                  </div>
                </td>
                <td className="whitespace-nowrap px-2 py-2 text-slate-600">
                  {METHODS[t.method] ?? t.method}
                  {t.kind === "otkazma" && t.to_method && <> → {METHODS[t.to_method] ?? t.to_method}</>}
                </td>
                <td
                  className={cx(
                    "whitespace-nowrap px-2 py-2 text-right font-semibold tabular-nums",
                    t.kind === "kirim" && "text-emerald-700",
                    t.kind === "chiqim" && "text-red-600",
                    t.kind === "otkazma" && "text-slate-600",
                  )}
                >
                  {t.kind === "kirim" ? "+" : t.kind === "chiqim" ? "−" : ""}
                  {formatSum(t.amount, false)}
                </td>
                <td className="whitespace-nowrap px-2 py-2 text-xs text-slate-500">{userName(t.created_by_id)}</td>
                <td className="px-2 py-2">
                  {canEdit && (
                    <div className="flex justify-end gap-1 opacity-60 group-hover:opacity-100">
                      <button onClick={() => onEdit(t)} className="rounded p-1 text-slate-400 hover:text-blue-600" title="Tahrirlash">
                        <Pencil size={14} />
                      </button>
                      <button onClick={() => onDelete(t)} className="rounded p-1 text-slate-400 hover:text-red-600" title="O'chirish">
                        <Trash2 size={14} />
                      </button>
                    </div>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function TxForm({ categories, initial, onSaved }: { categories: Category[]; initial?: Transaction; onSaved: () => void }) {
  const { toast } = useApp();
  const [kind, setKind] = useState<TxKind>(initial?.kind ?? "chiqim");
  const [amount, setAmount] = useState(initial?.amount ?? 0);
  const [method, setMethod] = useState(initial?.method ?? "naqd");
  const [toMethod, setToMethod] = useState(initial?.to_method ?? "karta");
  const [categoryId, setCategoryId] = useState<string>(initial?.category_id ? String(initial.category_id) : "");
  const [day, setDay] = useState(initial?.date ?? todayISO());
  const [note, setNote] = useState(initial?.note ?? "");
  const [orderNo, setOrderNo] = useState(initial?.order_id ? String(initial.order_id) : "");
  const [busy, setBusy] = useState(false);
  const cats = useMemo(() => categories.filter((c) => c.kind === kind && c.system_key !== "refund"), [categories, kind]);

  useEffect(() => {
    if (!initial && !cats.some((c) => String(c.id) === categoryId)) setCategoryId("");
  }, [kind, cats, categoryId, initial]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    const body = {
      kind,
      amount,
      method,
      to_method: kind === "otkazma" ? toMethod : null,
      category_id: kind === "otkazma" || !categoryId ? null : Number(categoryId),
      date: day,
      note,
      order_id: kind !== "otkazma" && orderNo ? Number(orderNo.replace(/\D/g, "")) : null,
      client_id: initial?.client_id ?? null,
      debt_id: initial?.debt_id ?? null,
    };
    try {
      if (initial) await api.patch(`/api/finance/transactions/${initial.id}`, body);
      else await api.post("/api/finance/transactions", body);
      toast(initial ? "Saqlandi" : `${KIND_LABEL[kind]} yozildi: ${formatSum(amount)}`);
      if (!initial) {
        setAmount(0);
        setNote("");
        setOrderNo("");
      }
      onSaved();
    } catch (err) {
      toast((err as Error).message, "error");
    } finally {
      setBusy(false);
    }
  }

  const kindBtn = (k: TxKind, icon: ReactNode, active: string) => (
    <button
      type="button"
      onClick={() => setKind(k)}
      className={cx(
        "flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-sm font-semibold transition",
        kind === k ? active : "bg-slate-100 text-slate-500 hover:bg-slate-200",
      )}
    >
      {icon} {KIND_LABEL[k]}
    </button>
  );

  return (
    <form onSubmit={submit} className={cx(!initial && "rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200")}>
      {!initial && <h2 className="mb-3 font-semibold text-slate-900">Yangi yozuv</h2>}
      <div className="mb-3 flex gap-2">
        {kindBtn("kirim", <ArrowDownLeft size={16} />, "bg-emerald-600 text-white")}
        {kindBtn("chiqim", <ArrowUpRight size={16} />, "bg-red-600 text-white")}
        {kindBtn("otkazma", <ArrowRightLeft size={16} />, "bg-slate-800 text-white")}
      </div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
        <div className="lg:col-span-2">
          <Field label="Summa (so'm) *">
            <SumInput value={amount} onChange={setAmount} />
          </Field>
        </div>
        {kind === "otkazma" ? (
          <>
            <Field label="Qayerdan">
              <Select value={method} onChange={(e) => setMethod(e.target.value)}>
                {Object.entries(METHODS).map(([k, v]) => (
                  <option key={k} value={k}>
                    {v}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Qayerga">
              <Select value={toMethod} onChange={(e) => setToMethod(e.target.value)}>
                {Object.entries(METHODS).map(([k, v]) => (
                  <option key={k} value={k}>
                    {v}
                  </option>
                ))}
              </Select>
            </Field>
          </>
        ) : (
          <>
            <Field label="Toifa">
              <Select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
                <option value="">— Boshqa —</option>
                {cats.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Usul">
              <Select value={method} onChange={(e) => setMethod(e.target.value)}>
                {Object.entries(METHODS).map(([k, v]) => (
                  <option key={k} value={k}>
                    {v}
                  </option>
                ))}
              </Select>
            </Field>
          </>
        )}
        <Field label="Sana">
          <Input type="date" value={day} onChange={(e) => setDay(e.target.value)} />
        </Field>
        {kind !== "otkazma" ? (
          <Field label="Zakaz № (ixtiyoriy)">
            <Input value={orderNo} onChange={(e) => setOrderNo(e.target.value)} placeholder="#12" inputMode="numeric" />
          </Field>
        ) : (
          <div />
        )}
        <div className="sm:col-span-2 lg:col-span-5">
          <Field label="Izoh">
            <Textarea rows={1} value={note} onChange={(e) => setNote(e.target.value)} placeholder="Masalan: akril 2 list, Sherzoddan" />
          </Field>
        </div>
        <div className="flex items-end">
          <Button type="submit" className="w-full" disabled={busy || amount <= 0}>
            <Plus size={16} /> {initial ? "Saqlash" : "Yozish"}
          </Button>
        </div>
      </div>
    </form>
  );
}

function CategoryBars({ title, rows }: { title: string; rows: FinanceSummary["chiqim_by_category"] }) {
  const max = Math.max(1, ...rows.map((r) => r.amount));
  const total = rows.reduce((s, r) => s + r.amount, 0);
  return (
    <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
      <div className="mb-3 flex items-baseline justify-between">
        <h2 className="text-sm font-semibold text-slate-900">{title}</h2>
        <span className="text-sm tabular-nums text-slate-500">{formatSum(total)}</span>
      </div>
      {rows.length === 0 ? (
        <div className="py-4 text-center text-sm text-slate-400">Yozuv yo'q</div>
      ) : (
        <div className="space-y-2">
          {rows.map((r) => (
            <div
              key={r.category_id ?? "none"}
              className="grid grid-cols-[8rem_1fr_auto] items-center gap-3 text-sm"
              title={`${r.name}: ${formatSum(r.amount)}`}
            >
              <span className="flex min-w-0 items-center gap-2 text-slate-700">
                <span className="h-2 w-2 shrink-0 rounded-full" style={{ background: r.color }} />
                <span className="truncate">{r.name}</span>
              </span>
              <div className="h-4">
                <div className="h-full rounded-r-[4px]" style={{ width: `${(r.amount / max) * 100}%`, background: SERIES, minWidth: 4 }} />
              </div>
              <span className="w-28 text-right tabular-nums text-slate-900">{formatSum(r.amount, false)}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

/* ================= Qarz daftari ================= */

function DebtsTab() {
  const { toast, isAdmin } = useApp();
  const [status, setStatus] = useState<"open" | "closed" | "all">("open");
  const [debts, setDebts] = useState<Debt[]>([]);
  const [orders, setOrders] = useState<OrderDebt[]>([]);
  const [creating, setCreating] = useState(false);
  const [paying, setPaying] = useState<Debt | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      const r = await api.get<{ debts: Debt[]; orders: OrderDebt[] }>("/api/finance/debts", { status });
      setDebts(r.debts);
      setOrders(r.orders);
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setLoading(false);
    }
  }, [status, toast]);

  useEffect(() => {
    load();
  }, [load]);
  useLive((e) => {
    if (e.type === "finance.update" || e.type === "order.update") load();
  });

  async function remove(d: Debt) {
    if (!window.confirm(`«${d.person}» qarz yozuvi o'chirilsinmi? (Kassadagi yozuvlar qoladi)`)) return;
    await api.del(`/api/finance/debts/${d.id}`);
    load();
  }

  const toUs = debts.filter((d) => d.direction === "bizga");
  const fromUs = debts.filter((d) => d.direction === "bizdan");
  const sum = (list: { remaining: number }[]) => list.reduce((s, d) => s + d.remaining, 0);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center gap-2">
        <Select value={status} onChange={(e) => setStatus(e.target.value as "open" | "closed" | "all")} className="w-auto">
          <option value="open">Ochiq qarzlar</option>
          <option value="closed">Yopilganlar</option>
          <option value="all">Hammasi</option>
        </Select>
        <Button className="ml-auto" onClick={() => setCreating(true)}>
          <Plus size={16} /> Qarz yozish
        </Button>
      </div>
      {loading ? (
        <div className="flex justify-center py-10">
          <Spinner />
        </div>
      ) : (
        <div className="grid gap-5 lg:grid-cols-2">
          <DebtList
            title="Bizdan qarzdorlar"
            hint="Kim sizga qarz (pul qaytarishi kerak)"
            total={sum(toUs)}
            debts={toUs}
            onPay={setPaying}
            onDelete={isAdmin ? remove : undefined}
          />
          <DebtList
            title="Biz qarzdormiz"
            hint="Siz kimga qarzsiz (ta'minotchi, bank...)"
            total={sum(fromUs)}
            debts={fromUs}
            onPay={setPaying}
            onDelete={isAdmin ? remove : undefined}
          />
        </div>
      )}
      {status !== "closed" && (
        <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
          <div className="mb-3 flex items-baseline justify-between">
            <div>
              <h2 className="font-semibold text-slate-900">Zakazlar bo'yicha qarzlar</h2>
              <p className="text-xs text-slate-500">Narxi to'liq to'lanmagan zakazlar (avtomatik)</p>
            </div>
            <span className="font-semibold tabular-nums text-red-600">{formatSum(sum(orders))}</span>
          </div>
          {orders.length === 0 ? (
            <div className="py-4 text-center text-sm text-slate-400">Qarzdor zakaz yo'q</div>
          ) : (
            <div className="divide-y divide-slate-100">
              {orders.map((o) => (
                <Link key={o.order_id} to={`/orders/${o.order_id}`} className="flex items-center gap-3 py-2 text-sm hover:bg-slate-50">
                  <span className="text-xs text-slate-400">{o.number}</span>
                  <span className="min-w-0 flex-1 truncate">
                    <span className="font-medium text-slate-900">{o.client_name ?? "—"}</span>
                    <span className="text-slate-500"> · {o.title}</span>
                  </span>
                  <span className="text-xs text-slate-400">
                    {formatSum(o.paid, false)} / {formatSum(o.price, false)}
                  </span>
                  <span className="w-28 text-right font-medium tabular-nums text-red-600">{formatSum(o.remaining, false)}</span>
                </Link>
              ))}
            </div>
          )}
        </section>
      )}
      <DebtModal open={creating} onClose={() => setCreating(false)} onSaved={load} />
      <PayDebtModal debt={paying} onClose={() => setPaying(null)} onSaved={load} />
    </div>
  );
}

function DebtList({
  title,
  hint,
  total,
  debts,
  onPay,
  onDelete,
}: {
  title: string;
  hint: string;
  total: number;
  debts: Debt[];
  onPay: (d: Debt) => void;
  onDelete?: (d: Debt) => void;
}) {
  const today = todayISO();
  return (
    <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
      <div className="mb-3 flex items-baseline justify-between">
        <div>
          <h2 className="font-semibold text-slate-900">{title}</h2>
          <p className="text-xs text-slate-500">{hint}</p>
        </div>
        <span className="text-lg font-semibold tabular-nums text-slate-900">{formatSum(total)}</span>
      </div>
      {debts.length === 0 ? (
        <div className="py-4 text-center text-sm text-slate-400">Yozuv yo'q</div>
      ) : (
        <div className="space-y-2">
          {debts.map((d) => {
            const overdue = !d.closed_at && d.due_date && d.due_date < today;
            return (
              <div key={d.id} className="rounded-xl border border-slate-200 p-3">
                <div className="flex items-start gap-2">
                  <div className="min-w-0 flex-1">
                    <div className="font-medium text-slate-900">{d.person}</div>
                    <div className="text-xs text-slate-500">
                      {[d.phone, d.note].filter(Boolean).join(" · ")}
                      {d.due_date && <span className={cx("ml-1", overdue && "font-medium text-red-600")}>· muddat {formatDate(d.due_date)}</span>}
                    </div>
                  </div>
                  <div className="text-right">
                    <div className="font-semibold tabular-nums">{formatSum(d.remaining, false)}</div>
                    <div className="text-xs text-slate-400">{formatSum(d.amount, false)} dan</div>
                  </div>
                </div>
                <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-100">
                  <div className="h-full rounded-full bg-emerald-500" style={{ width: `${Math.min(100, (d.paid / d.amount) * 100)}%` }} />
                </div>
                <div className="mt-2 flex items-center gap-2">
                  {d.closed_at ? (
                    <span className="text-xs text-emerald-700">✓ Yopilgan {formatDate(d.closed_at)}</span>
                  ) : (
                    <Button size="sm" variant="secondary" onClick={() => onPay(d)}>
                      {d.direction === "bizga" ? "Qaytardi" : "To'ladik"}
                    </Button>
                  )}
                  {onDelete && (
                    <button onClick={() => onDelete(d)} className="ml-auto rounded p-1 text-slate-300 hover:text-red-600" title="O'chirish">
                      <Trash2 size={14} />
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}

function DebtModal({ open, onClose, onSaved }: { open: boolean; onClose: () => void; onSaved: () => void }) {
  const { toast } = useApp();
  const [form, setForm] = useState({ direction: "bizga", person: "", phone: "", amount: 0, due_date: "", note: "", with_cash: false, method: "naqd" });
  useEffect(() => {
    if (open) setForm({ direction: "bizga", person: "", phone: "", amount: 0, due_date: "", note: "", with_cash: false, method: "naqd" });
  }, [open]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    try {
      await api.post("/api/finance/debts", { ...form, due_date: form.due_date || null });
      toast("Qarz yozildi");
      onSaved();
      onClose();
    } catch (err) {
      toast((err as Error).message, "error");
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Qarz yozish">
      <form onSubmit={submit} className="space-y-3">
        <div className="flex gap-2">
          {(
            [
              ["bizga", "Menga qarz (u qaytaradi)"],
              ["bizdan", "Men qarzman (men to'layman)"],
            ] as const
          ).map(([k, label]) => (
            <button
              key={k}
              type="button"
              onClick={() => setForm({ ...form, direction: k })}
              className={cx(
                "flex-1 rounded-lg px-3 py-2 text-sm font-medium",
                form.direction === k ? "bg-slate-900 text-white" : "bg-slate-100 text-slate-600",
              )}
            >
              {label}
            </button>
          ))}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Kim *">
            <Input value={form.person} onChange={(e) => setForm({ ...form, person: e.target.value })} required autoFocus />
          </Field>
          <Field label="Telefon">
            <Input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} inputMode="tel" />
          </Field>
          <Field label="Summa *">
            <SumInput value={form.amount} onChange={(v) => setForm({ ...form, amount: v })} />
          </Field>
          <Field label="Qaytarish muddati">
            <Input type="date" value={form.due_date} onChange={(e) => setForm({ ...form, due_date: e.target.value })} />
          </Field>
        </div>
        <Field label="Izoh">
          <Input value={form.note} onChange={(e) => setForm({ ...form, note: e.target.value })} placeholder="Masalan: material uchun nasiya" />
        </Field>
        <label className="flex items-center gap-2 rounded-lg bg-slate-50 p-2.5 text-sm">
          <input type="checkbox" checked={form.with_cash} onChange={(e) => setForm({ ...form, with_cash: e.target.checked })} />
          {form.direction === "bizga" ? "Pulni hozir kassadan berdim (chiqim yozilsin)" : "Pulni hozir oldim (kassaga kirim yozilsin)"}
          {form.with_cash && (
            <Select value={form.method} onChange={(e) => setForm({ ...form, method: e.target.value })} className="ml-auto h-8 w-28 text-xs">
              {Object.entries(METHODS).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </Select>
          )}
        </label>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Bekor
          </Button>
          <Button type="submit" disabled={!form.person.trim() || form.amount <= 0}>
            Saqlash
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function PayDebtModal({ debt, onClose, onSaved }: { debt: Debt | null; onClose: () => void; onSaved: () => void }) {
  const { toast } = useApp();
  const [amount, setAmount] = useState(0);
  const [method, setMethod] = useState("naqd");
  useEffect(() => {
    if (debt) {
      setAmount(debt.remaining);
      setMethod("naqd");
    }
  }, [debt]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!debt) return;
    try {
      const r = await api.post<Debt>(`/api/finance/debts/${debt.id}/pay`, { amount, method });
      toast(r.closed_at ? "Qarz to'liq yopildi" : `Qoldi: ${formatSum(r.remaining)}`);
      onSaved();
      onClose();
    } catch (err) {
      toast((err as Error).message, "error");
    }
  }

  return (
    <Modal open={debt !== null} onClose={onClose} title={debt?.direction === "bizga" ? `${debt?.person} qarzini qaytardi` : `${debt?.person} ga to'lov`}>
      <form onSubmit={submit} className="space-y-3">
        <p className="text-sm text-slate-600">
          Qoldiq: <b>{formatSum(debt?.remaining ?? 0)}</b>. Summa kassaga {debt?.direction === "bizga" ? "kirim" : "chiqim"} bo'lib yoziladi.
        </p>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Summa">
            <SumInput value={amount} onChange={setAmount} />
          </Field>
          <Field label="Usul">
            <Select value={method} onChange={(e) => setMethod(e.target.value)}>
              {Object.entries(METHODS).map(([k, v]) => (
                <option key={k} value={k}>
                  {v}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Bekor
          </Button>
          <Button type="submit" disabled={amount <= 0}>
            Yozish
          </Button>
        </div>
      </form>
    </Modal>
  );
}

/* ================= Toifalar ================= */

function CategoriesTab({ categories, reload }: { categories: Category[]; reload: () => void }) {
  const { toast } = useApp();
  const [name, setName] = useState("");
  const [kind, setKind] = useState<"kirim" | "chiqim">("chiqim");

  async function add() {
    try {
      await api.post("/api/finance/categories", { name, kind, color: kind === "kirim" ? "#16a34a" : "#ea580c" });
      setName("");
      reload();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function save(c: Category, patch: Partial<Category>) {
    try {
      await api.patch(`/api/finance/categories/${c.id}`, { name: patch.name ?? c.name, color: patch.color ?? c.color, kind: c.kind });
      reload();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function remove(c: Category) {
    if (!window.confirm(`«${c.name}» toifasi o'chirilsinmi? Uning ${c.used ?? 0} ta yozuvi «Toifasiz» bo'lib qoladi.`)) return;
    try {
      await api.del(`/api/finance/categories/${c.id}`);
      reload();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  return (
    <div className="space-y-5">
      <section className="flex flex-wrap items-end gap-2 rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
        <Field label="Yangi toifa">
          <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Masalan: Bo'yoq" className="w-56" />
        </Field>
        <Select value={kind} onChange={(e) => setKind(e.target.value as "kirim" | "chiqim")} className="w-32">
          <option value="chiqim">Chiqim</option>
          <option value="kirim">Kirim</option>
        </Select>
        <Button onClick={add} disabled={!name.trim()}>
          <Plus size={16} /> Qo'shish
        </Button>
      </section>
      <div className="grid gap-5 lg:grid-cols-2">
        {(["kirim", "chiqim"] as const).map((k) => (
          <section key={k} className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
            <h2 className="mb-3 font-semibold text-slate-900">{k === "kirim" ? "Kirim toifalari" : "Chiqim toifalari"}</h2>
            <div className="space-y-1.5">
              {categories
                .filter((c) => c.kind === k)
                .map((c) => (
                  <div key={c.id} className="flex items-center gap-2 rounded-lg border border-slate-200 px-2 py-1.5">
                    <input type="color" value={c.color} onChange={(e) => save(c, { color: e.target.value })} className="h-7 w-7 rounded border-0 p-0" />
                    <input
                      defaultValue={c.name}
                      onBlur={(e) => e.target.value.trim() && e.target.value !== c.name && save(c, { name: e.target.value })}
                      className="min-w-0 flex-1 rounded px-1 text-sm outline-none focus:bg-slate-50"
                    />
                    <span className="text-xs text-slate-400">{c.used ?? 0} ta</span>
                    {c.system_key ? (
                      <span className="text-[11px] text-slate-400" title="Tizim toifasi — o'chirilmaydi">
                        tizim
                      </span>
                    ) : (
                      <button onClick={() => remove(c)} className="rounded p-1 text-slate-300 hover:text-red-600" title="O'chirish">
                        <Trash2 size={14} />
                      </button>
                    )}
                  </div>
                ))}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
