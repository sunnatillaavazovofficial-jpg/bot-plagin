import { useCallback, useEffect, useMemo, useState } from "react";
import { Empty, Input, Spinner, cx } from "../components/ui";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { compactSum, formatDate, formatSum, PAYMENT_LABELS, todayISO } from "../lib/format";

interface Report {
  date_from: string;
  date_to: string;
  kpi: {
    revenue: number;
    orders_created: number;
    orders_done: number;
    order_sum: number;
    avg_check: number;
    debt: number;
    overdue: number;
    new_clients: number;
    new_chats: number;
    incoming: number;
  };
  daily: { date: string; revenue: number; orders: number }[];
  by_status: { status_id: number; name: string; color: string; count: number; sum: number; is_done: boolean; is_cancel: boolean }[];
  by_staff: { user_id: number | null; name: string; orders: number; done: number; done_sum: number; revenue: number }[];
  by_account: {
    account_id: number | null;
    name: string;
    color: string;
    incoming: number;
    chats: number;
    orders: number;
    order_sum: number;
    revenue: number;
  }[];
  by_method: { method: string; amount: number }[];
  top_clients: { client_id: number; name: string; revenue: number }[];
}

/* Grafik ranglari — tasdiqlangan palitra (dataviz), yorug' fon uchun */
const VIZ = {
  series: "#2a78d6",
  seriesHover: "#1c5cab",
  methods: ["#2a78d6", "#eb6834", "#1baf7a", "#eda100"],
  grid: "#e1e0d9",
  axis: "#c3c2b7",
  muted: "#898781",
};

function monthStart(offset = 0): string {
  const d = new Date();
  d.setDate(1);
  d.setMonth(d.getMonth() + offset);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function monthEnd(offset: number): string {
  const d = new Date();
  d.setDate(1);
  d.setMonth(d.getMonth() + offset + 1);
  d.setDate(0);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

const PRESETS = [
  { key: "month", label: "Bu oy", range: () => [monthStart(0), todayISO()] },
  { key: "prev", label: "O'tgan oy", range: () => [monthStart(-1), monthEnd(-1)] },
  { key: "7", label: "7 kun", range: () => [todayISO(-6), todayISO()] },
  { key: "30", label: "30 kun", range: () => [todayISO(-29), todayISO()] },
  { key: "90", label: "90 kun", range: () => [todayISO(-89), todayISO()] },
  { key: "year", label: "Bu yil", range: () => [`${new Date().getFullYear()}-01-01`, todayISO()] },
] as const;

export default function ReportsPage() {
  const { isAdmin, toast } = useApp();
  const [preset, setPreset] = useState<string>("month");
  const [range, setRange] = useState<[string, string]>([monthStart(0), todayISO()]);
  const [data, setData] = useState<Report | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await api.get<Report>("/api/reports/summary", { date_from: range[0], date_to: range[1] }));
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setLoading(false);
    }
  }, [range, toast]);

  useEffect(() => {
    if (isAdmin) load();
  }, [load, isAdmin]);

  if (!isAdmin) return <Empty title="Hisobot faqat admin uchun" />;

  return (
    <div className="scroll-thin h-full overflow-y-auto">
      <div className="mx-auto max-w-6xl space-y-5 p-4 sm:p-6">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="mr-auto text-xl font-semibold text-slate-900">Hisobot</h1>
        </div>

        {/* Filtrlar — bitta qatorda, grafiklardan yuqorida */}
        <div className="flex flex-wrap items-center gap-2">
          {PRESETS.map((p) => (
            <button
              key={p.key}
              onClick={() => {
                setPreset(p.key);
                setRange(p.range() as [string, string]);
              }}
              className={cx(
                "rounded-full px-3 py-1.5 text-sm font-medium transition",
                preset === p.key ? "bg-slate-900 text-white" : "bg-white text-slate-600 ring-1 ring-slate-200 hover:bg-slate-50",
              )}
            >
              {p.label}
            </button>
          ))}
          <div className="flex items-center gap-1.5">
            <Input
              type="date"
              value={range[0]}
              max={range[1]}
              onChange={(e) => {
                setPreset("custom");
                setRange([e.target.value, range[1]]);
              }}
              className="h-9 w-auto"
            />
            <span className="text-slate-400">—</span>
            <Input
              type="date"
              value={range[1]}
              min={range[0]}
              onChange={(e) => {
                setPreset("custom");
                setRange([range[0], e.target.value]);
              }}
              className="h-9 w-auto"
            />
          </div>
        </div>

        {!data ? (
          <div className="flex justify-center py-16">
            <Spinner />
          </div>
        ) : (
          <div className={cx("space-y-5 transition-opacity", loading && "opacity-60")}>
            <section className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200 sm:col-span-2 lg:row-span-2">
                <div className="text-sm text-slate-500">Tushum (to'lovlar)</div>
                <div className="mt-1 text-5xl font-semibold tracking-tight text-slate-900">{compactSum(data.kpi.revenue)}</div>
                <div className="mt-1 text-sm text-slate-500">{formatSum(data.kpi.revenue)}</div>
                <div className="mt-4 grid grid-cols-2 gap-3 border-t border-slate-100 pt-4 text-sm">
                  <div>
                    <div className="text-slate-500">Zakazlar summasi</div>
                    <div className="font-semibold text-slate-900">{formatSum(data.kpi.order_sum)}</div>
                  </div>
                  <div>
                    <div className="text-slate-500">O'rtacha chek</div>
                    <div className="font-semibold text-slate-900">{formatSum(data.kpi.avg_check)}</div>
                  </div>
                </div>
              </div>
              <Tile label="Yangi zakazlar" value={data.kpi.orders_created} />
              <Tile label="Topshirilgan" value={data.kpi.orders_done} />
              <Tile label="Umumiy qarzdorlik" value={formatSum(data.kpi.debt)} tone={data.kpi.debt > 0 ? "bad" : undefined} hint="hozirgi holat" />
              <Tile label="Muddati o'tgan" value={data.kpi.overdue} tone={data.kpi.overdue > 0 ? "bad" : undefined} hint="hozirgi holat" />
              <Tile label="Yangi mijozlar" value={data.kpi.new_clients} />
              <Tile label="Yangi chatlar" value={data.kpi.new_chats} />
              <Tile label="Kiruvchi xabarlar" value={data.kpi.incoming} />
            </section>

            <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
              <h2 className="text-sm font-semibold text-slate-900">Tushum dinamikasi</h2>
              <p className="mb-4 text-xs text-slate-500">
                {formatDate(data.date_from, true)} — {formatDate(data.date_to, true)}
                {data.daily.length > 62 ? ", haftalar bo'yicha" : ", kunlar bo'yicha"}
              </p>
              <RevenueChart daily={data.daily} />
            </section>

            <div className="grid gap-5 lg:grid-cols-2">
              <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <h2 className="text-sm font-semibold text-slate-900">Zakazlar holati</h2>
                <p className="mb-4 text-xs text-slate-500">Hozir har bir holatdagi zakazlar soni</p>
                <StatusBars rows={data.by_status} />
              </section>
              <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
                <h2 className="text-sm font-semibold text-slate-900">To'lov turlari</h2>
                <p className="mb-4 text-xs text-slate-500">Davr ichidagi tushum</p>
                <MethodSplit rows={data.by_method} />
                <h2 className="mb-2 mt-6 text-sm font-semibold text-slate-900">Eng ko'p to'lagan mijozlar</h2>
                {data.top_clients.length === 0 ? (
                  <div className="text-sm text-slate-400">Ma'lumot yo'q</div>
                ) : (
                  <ol className="space-y-1.5 text-sm">
                    {data.top_clients.map((c, i) => (
                      <li key={c.client_id} className="flex items-center gap-2">
                        <span className="w-5 text-slate-400">{i + 1}.</span>
                        <span className="min-w-0 flex-1 truncate text-slate-800">{c.name}</span>
                        <span className="font-medium tabular-nums text-slate-900">{formatSum(c.revenue)}</span>
                      </li>
                    ))}
                  </ol>
                )}
              </section>
            </div>

            <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
              <h2 className="mb-3 text-sm font-semibold text-slate-900">Telegram akkauntlar kesimida</h2>
              <div className="overflow-x-auto">
                <table className="w-full min-w-[640px] text-sm">
                  <thead className="text-left text-xs text-slate-500">
                    <tr className="border-b border-slate-100">
                      <th className="py-2 pr-3 font-medium">Akkaunt</th>
                      <th className="py-2 pr-3 text-right font-medium">Kiruvchi xabar</th>
                      <th className="py-2 pr-3 text-right font-medium">Yozgan chatlar</th>
                      <th className="py-2 pr-3 text-right font-medium">Zakazlar</th>
                      <th className="py-2 pr-3 text-right font-medium">Zakaz summasi</th>
                      <th className="py-2 text-right font-medium">Tushum</th>
                    </tr>
                  </thead>
                  <tbody className="tabular-nums">
                    {data.by_account.map((a) => (
                      <tr key={a.account_id ?? "none"} className="border-b border-slate-50">
                        <td className="py-2 pr-3">
                          <span className="inline-flex items-center gap-2 font-medium text-slate-800">
                            <span className="h-2.5 w-2.5 rounded-full" style={{ background: a.color }} />
                            {a.name}
                          </span>
                        </td>
                        <td className="py-2 pr-3 text-right">{a.incoming}</td>
                        <td className="py-2 pr-3 text-right">{a.chats}</td>
                        <td className="py-2 pr-3 text-right">{a.orders}</td>
                        <td className="py-2 pr-3 text-right">{formatSum(a.order_sum, false)}</td>
                        <td className="py-2 text-right font-medium text-slate-900">{formatSum(a.revenue, false)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>

            <section className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200">
              <h2 className="mb-3 text-sm font-semibold text-slate-900">Xodimlar kesimida</h2>
              {data.by_staff.length === 0 ? (
                <div className="text-sm text-slate-400">Davr ichida zakaz yoki to'lov yo'q</div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full min-w-[560px] text-sm">
                    <thead className="text-left text-xs text-slate-500">
                      <tr className="border-b border-slate-100">
                        <th className="py-2 pr-3 font-medium">Xodim</th>
                        <th className="py-2 pr-3 text-right font-medium">Yangi zakaz</th>
                        <th className="py-2 pr-3 text-right font-medium">Topshirgan</th>
                        <th className="py-2 pr-3 text-right font-medium">Topshirilgan summa</th>
                        <th className="py-2 text-right font-medium">Tushum</th>
                      </tr>
                    </thead>
                    <tbody className="tabular-nums">
                      {data.by_staff.map((s) => (
                        <tr key={s.user_id ?? "none"} className="border-b border-slate-50">
                          <td className="py-2 pr-3 font-medium text-slate-800">{s.name}</td>
                          <td className="py-2 pr-3 text-right">{s.orders}</td>
                          <td className="py-2 pr-3 text-right">{s.done}</td>
                          <td className="py-2 pr-3 text-right">{formatSum(s.done_sum, false)}</td>
                          <td className="py-2 text-right font-medium text-slate-900">{formatSum(s.revenue, false)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          </div>
        )}
      </div>
    </div>
  );
}

function Tile({ label, value, tone, hint }: { label: string; value: string | number; tone?: "bad"; hint?: string }) {
  return (
    <div className="rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
      <div className="text-sm text-slate-500">{label}</div>
      <div className={cx("mt-1 text-2xl font-semibold", tone === "bad" ? "text-red-600" : "text-slate-900")}>{value}</div>
      {hint && <div className="text-xs text-slate-400">{hint}</div>}
    </div>
  );
}

/** O'q uchun "toza" qadam: 4 ta teng bo'lak (0, 1 mln, 2 mln, ...) */
function niceMax(value: number): number {
  if (value <= 0) return 4;
  const raw = value / 4;
  const exp = Math.pow(10, Math.floor(Math.log10(raw)));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * exp).find((s) => s >= raw) ?? 10 * exp;
  return step * 4;
}

function RevenueChart({ daily }: { daily: Report["daily"] }) {
  const [hover, setHover] = useState<number | null>(null);
  const points = useMemo(() => {
    if (daily.length <= 62) return daily.map((d) => ({ label: formatDate(d.date), from: d.date, ...d }));
    const weeks: { label: string; from: string; date: string; revenue: number; orders: number }[] = [];
    daily.forEach((d, i) => {
      if (i % 7 === 0) weeks.push({ label: formatDate(d.date), from: d.date, date: d.date, revenue: 0, orders: 0 });
      const w = weeks[weeks.length - 1];
      w.revenue += d.revenue;
      w.orders += d.orders;
      w.date = d.date;
    });
    return weeks;
  }, [daily]);

  const total = points.reduce((s, p) => s + p.revenue, 0);
  if (total === 0) return <div className="py-10 text-center text-sm text-slate-400">Bu davrda to'lov yo'q</div>;

  const W = 900;
  const H = 240;
  const pad = { l: 52, r: 8, t: 12, b: 26 };
  const max = niceMax(Math.max(...points.map((p) => p.revenue)));
  const band = (W - pad.l - pad.r) / points.length;
  const barW = Math.max(2, Math.min(24, band - 2));
  const y = (v: number) => pad.t + (H - pad.t - pad.b) * (1 - v / max);
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => f * max);
  const labelEvery = Math.ceil(points.length / 10);
  const hp = hover !== null ? points[hover] : null;

  return (
    <div className="relative">
      <svg viewBox={`0 0 ${W} ${H}`} className="h-auto w-full" role="img" aria-label="Tushum dinamikasi">
        {ticks.map((t) => (
          <g key={t}>
            <line x1={pad.l} x2={W - pad.r} y1={y(t)} y2={y(t)} stroke={t === 0 ? VIZ.axis : VIZ.grid} strokeWidth={1} />
            <text x={pad.l - 8} y={y(t) + 4} textAnchor="end" fontSize={11} fill={VIZ.muted} style={{ fontVariantNumeric: "tabular-nums" }}>
              {compactSum(t)}
            </text>
          </g>
        ))}
        {points.map((p, i) => {
          const x = pad.l + band * i + (band - barW) / 2;
          const top = y(p.revenue);
          const h = y(0) - top;
          const r = Math.min(4, barW / 2, h);
          return (
            <g key={p.from}>
              {h > 0 && (
                <path
                  d={`M${x},${y(0)} V${top + r} Q${x},${top} ${x + r},${top} H${x + barW - r} Q${x + barW},${top} ${x + barW},${top + r} V${y(0)} Z`}
                  fill={hover === i ? VIZ.seriesHover : VIZ.series}
                />
              )}
              {i % labelEvery === 0 && (
                <text x={pad.l + band * i + band / 2} y={H - 8} textAnchor="middle" fontSize={11} fill={VIZ.muted}>
                  {p.label}
                </text>
              )}
              <rect
                x={pad.l + band * i}
                y={pad.t}
                width={band}
                height={H - pad.t - pad.b}
                fill="transparent"
                tabIndex={0}
                aria-label={`${p.label}: ${formatSum(p.revenue)}`}
                onPointerEnter={() => setHover(i)}
                onPointerLeave={() => setHover(null)}
                onFocus={() => setHover(i)}
                onBlur={() => setHover(null)}
              />
            </g>
          );
        })}
      </svg>
      {hp && hover !== null && (
        <div
          className="pointer-events-none absolute top-0 z-10 -translate-x-1/2 rounded-lg bg-white px-3 py-2 text-xs shadow-lg ring-1 ring-slate-200"
          style={{ left: `${((pad.l + band * hover + band / 2) / W) * 100}%` }}
        >
          <div className="text-sm font-semibold text-slate-900">{formatSum(hp.revenue)}</div>
          <div className="text-slate-500">
            {hp.from === hp.date ? formatDate(hp.date, true) : `${formatDate(hp.from)} — ${formatDate(hp.date)}`}
          </div>
          <div className="text-slate-500">{hp.orders} ta yangi zakaz</div>
        </div>
      )}
    </div>
  );
}

function StatusBars({ rows }: { rows: Report["by_status"] }) {
  const visible = rows.filter((r) => !r.is_cancel);
  const max = Math.max(1, ...visible.map((r) => r.count));
  if (!visible.some((r) => r.count)) return <div className="py-6 text-center text-sm text-slate-400">Zakaz yo'q</div>;
  return (
    <div className="space-y-2.5">
      {visible.map((r) => (
        <div key={r.status_id} className="group grid grid-cols-[8.5rem_1fr_auto] items-center gap-3 text-sm" title={`${r.name}: ${r.count} ta, ${formatSum(r.sum)}`}>
          <span className="flex min-w-0 items-center gap-2 text-slate-700">
            <span className="h-2 w-2 shrink-0 rounded-full" style={{ background: r.color }} />
            <span className="truncate">{r.name}</span>
          </span>
          <div className="h-5">
            {r.count > 0 && (
              <div
                className="h-full rounded-r-[4px] transition group-hover:opacity-80"
                style={{ width: `${(r.count / max) * 100}%`, background: VIZ.series, minWidth: 4 }}
              />
            )}
          </div>
          <span className="w-28 text-right tabular-nums text-slate-600">
            <b className="text-slate-900">{r.count}</b> · {compactSum(r.sum)}
          </span>
        </div>
      ))}
    </div>
  );
}

function MethodSplit({ rows }: { rows: Report["by_method"] }) {
  const total = rows.reduce((s, r) => s + Math.max(r.amount, 0), 0);
  if (total <= 0) return <div className="py-6 text-center text-sm text-slate-400">Bu davrda to'lov yo'q</div>;
  const order = Object.keys(PAYMENT_LABELS);
  const colorOf = (m: string) => VIZ.methods[Math.max(0, order.indexOf(m)) % VIZ.methods.length];
  const sorted = [...rows].filter((r) => r.amount > 0).sort((a, b) => order.indexOf(a.method) - order.indexOf(b.method));
  return (
    <div>
      <div className="flex h-2 gap-[2px] overflow-hidden rounded-full">
        {sorted.map((r) => (
          <div key={r.method} style={{ width: `${(r.amount / total) * 100}%`, background: colorOf(r.method) }} title={`${PAYMENT_LABELS[r.method] ?? r.method}: ${formatSum(r.amount)}`} />
        ))}
      </div>
      <ul className="mt-3 space-y-1.5 text-sm">
        {sorted.map((r) => (
          <li key={r.method} className="flex items-center gap-2">
            <span className="h-2.5 w-2.5 rounded-sm" style={{ background: colorOf(r.method) }} />
            <span className="flex-1 text-slate-700">{PAYMENT_LABELS[r.method] ?? r.method}</span>
            <span className="tabular-nums text-slate-500">{Math.round((r.amount / total) * 100)}%</span>
            <span className="w-32 text-right font-medium tabular-nums text-slate-900">{formatSum(r.amount)}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
