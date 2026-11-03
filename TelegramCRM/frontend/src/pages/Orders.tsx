import { CalendarClock, ClipboardList, LayoutGrid, Plus, Rows3, Search } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import OrderDrawer from "../components/OrderDrawer";
import OrderForm from "../components/OrderForm";
import { Avatar, Button, Empty, Input, Select, Spinner, StatusPill, Tabs, cx } from "../components/ui";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { deadlineState, formatDate, formatSum } from "../lib/format";
import { useLive } from "../lib/live";
import type { Order } from "../lib/types";

type View = "board" | "table";
type Scope = "active" | "overdue" | "done" | "cancel" | "all";

function readView(): View {
  try {
    return localStorage.getItem("orders:view") === "table" ? "table" : "board";
  } catch {
    return "board";
  }
}

export default function OrdersPage() {
  const { orderId } = useParams();
  const navigate = useNavigate();
  const { statuses, users, accounts, userName, statusById, isManager, me, toast } = useApp();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [view, setView] = useState<View>(readView);
  const [scope, setScope] = useState<Scope>("active");
  const [q, setQ] = useState("");
  const [assignee, setAssignee] = useState("");
  const [account, setAccount] = useState("");
  const [creating, setCreating] = useState(false);
  const [dragOver, setDragOver] = useState<number | null>(null);
  const dragId = useRef<number | null>(null);
  const timer = useRef<number | undefined>(undefined);

  const load = useCallback(async () => {
    try {
      setOrders(
        await api.get<Order[]>("/api/orders", {
          scope,
          q: q || undefined,
          assignee_id: assignee || undefined,
          account_id: account || undefined,
        }),
      );
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setLoading(false);
    }
  }, [scope, q, assignee, account, toast]);

  useEffect(() => {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(load, q ? 300 : 0);
  }, [load, q]);

  useEffect(() => {
    try {
      localStorage.setItem("orders:view", view);
    } catch {
      /* e'tiborsiz */
    }
  }, [view]);

  useLive((e) => {
    if (e.type === "order.update") {
      window.clearTimeout(timer.current);
      timer.current = window.setTimeout(load, 400);
    }
  });

  const columns = useMemo(() => {
    const byStatus = new Map<number, Order[]>();
    for (const o of orders) {
      const list = byStatus.get(o.status_id) ?? [];
      list.push(o);
      byStatus.set(o.status_id, list);
    }
    return statuses.map((s) => ({ status: s, orders: byStatus.get(s.id) ?? [] }));
  }, [orders, statuses]);

  async function drop(e: DragEvent, statusId: number, beforeId: number | null) {
    e.preventDefault();
    setDragOver(null);
    const id = dragId.current;
    dragId.current = null;
    if (!id) return;
    const moving = orders.find((o) => o.id === id);
    if (!moving) return;
    const column = orders.filter((o) => o.status_id === statusId && o.id !== id);
    const idx = beforeId ? column.findIndex((o) => o.id === beforeId) : column.length;
    column.splice(idx < 0 ? column.length : idx, 0, { ...moving, status_id: statusId });
    const positions = new Map(column.map((o, i) => [o.id, i]));
    setOrders((list) =>
      list
        .map((o) => (o.id === id ? { ...o, status_id: statusId } : o))
        .map((o) => (positions.has(o.id) ? { ...o, position: positions.get(o.id)! } : o))
        .sort((a, b) => a.position - b.position),
    );
    try {
      if (moving.status_id !== statusId) await api.patch(`/api/orders/${id}`, { status_id: statusId });
      await api.post("/api/orders/reorder", { status_id: statusId, order_ids: column.map((o) => o.id) });
    } catch (err) {
      toast((err as Error).message, "error");
      load();
    }
  }

  const totals = useMemo(
    () => ({
      sum: orders.reduce((s, o) => s + o.price, 0),
      debt: orders.reduce((s, o) => s + o.debt, 0),
    }),
    [orders],
  );

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="space-y-3 border-b border-slate-200 bg-white px-4 py-3 sm:px-6">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-xl font-semibold text-slate-900">Zakazlar</h1>
          <span className="text-sm text-slate-500">
            {orders.length} ta · {formatSum(totals.sum)}
            {totals.debt > 0 && <span className="text-red-600"> · qarz {formatSum(totals.debt)}</span>}
          </span>
          <div className="ml-auto flex items-center gap-2">
            <Tabs
              value={view}
              onChange={setView}
              items={[
                { value: "board", label: <LayoutGrid size={16} /> },
                { value: "table", label: <Rows3 size={16} /> },
              ]}
            />
            {isManager && (
              <Button onClick={() => setCreating(true)}>
                <Plus size={16} /> <span className="hidden sm:inline">Yangi zakaz</span>
              </Button>
            )}
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <div className="relative min-w-48 flex-1 sm:max-w-xs">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Raqam, nom yoki mijoz..." className="pl-9" />
          </div>
          <Select value={scope} onChange={(e) => setScope(e.target.value as Scope)} className="w-auto">
            <option value="active">Jarayondagilar</option>
            <option value="overdue">Muddati o'tganlar</option>
            <option value="done">Topshirilganlar</option>
            <option value="cancel">Bekor qilinganlar</option>
            <option value="all">Hammasi</option>
          </Select>
          {me.role !== "xodim" && (
            <Select value={assignee} onChange={(e) => setAssignee(e.target.value)} className="w-auto">
              <option value="">Barcha xodimlar</option>
              {users.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </Select>
          )}
          {accounts.length > 1 && me.role !== "xodim" && (
            <Select value={account} onChange={(e) => setAccount(e.target.value)} className="w-auto">
              <option value="">Barcha akkauntlar</option>
              {accounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </Select>
          )}
        </div>
      </header>

      {loading ? (
        <div className="flex justify-center py-16">
          <Spinner />
        </div>
      ) : orders.length === 0 && view === "table" ? (
        <Empty icon={<ClipboardList size={40} />} title="Zakaz topilmadi" text="Chatdagi xabardan yoki «Yangi zakaz» tugmasi bilan yarating" />
      ) : view === "board" ? (
        <div className="scroll-thin flex min-h-0 flex-1 gap-3 overflow-x-auto p-4 sm:px-6">
          {columns.map(({ status, orders: list }) => (
            <div
              key={status.id}
              className={cx(
                "flex w-72 shrink-0 flex-col rounded-2xl bg-slate-100/80 transition",
                dragOver === status.id && "ring-2 ring-blue-400",
              )}
              onDragOver={(e) => {
                e.preventDefault();
                setDragOver(status.id);
              }}
              onDragLeave={() => setDragOver((v) => (v === status.id ? null : v))}
              onDrop={(e) => drop(e, status.id, null)}
            >
              <div className="flex items-center gap-2 px-3 pb-2 pt-3">
                <span className="h-2.5 w-2.5 rounded-full" style={{ background: status.color }} />
                <span className="text-sm font-semibold text-slate-700">{status.name}</span>
                <span className="text-xs text-slate-400">{list.length}</span>
                <span className="ml-auto text-xs text-slate-400">{list.length ? formatSum(list.reduce((s, o) => s + o.price, 0), false) : ""}</span>
              </div>
              <div className="scroll-thin min-h-24 flex-1 space-y-2 overflow-y-auto px-2 pb-3">
                {list.map((o) => {
                  const dl = deadlineState(o.deadline, status.is_done || status.is_cancel);
                  return (
                    <div
                      key={o.id}
                      draggable
                      onDragStart={() => (dragId.current = o.id)}
                      onDrop={(e) => {
                        e.stopPropagation();
                        drop(e, status.id, o.id);
                      }}
                      onClick={() => navigate(`/orders/${o.id}`)}
                      className="cursor-pointer rounded-xl border border-slate-200 bg-white p-3 shadow-sm transition hover:border-blue-300 hover:shadow"
                    >
                      <div className="flex items-start justify-between gap-2">
                        <span className="text-xs font-medium text-slate-400">{o.number}</span>
                        {o.deadline && (
                          <span
                            className={cx(
                              "inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-[11px] font-medium",
                              dl === "overdue" ? "bg-red-50 text-red-600" : dl ? "bg-amber-50 text-amber-700" : "text-slate-400",
                            )}
                          >
                            <CalendarClock size={12} /> {formatDate(o.deadline)}
                          </span>
                        )}
                      </div>
                      <div className="mt-1 line-clamp-2 text-sm font-medium text-slate-900">{o.title}</div>
                      {o.client_name && <div className="mt-0.5 truncate text-xs text-slate-500">{o.client_name}</div>}
                      <div className="mt-2.5 flex items-center gap-2">
                        {o.price > 0 && <span className="text-xs font-medium text-slate-700">{formatSum(o.price, false)}</span>}
                        {o.debt > 0 && o.paid > 0 && <span className="text-[11px] text-red-500">qarz {formatSum(o.debt, false)}</span>}
                        {o.price > 0 && o.debt === 0 && <span className="text-[11px] text-emerald-600">to'langan</span>}
                        {o.assignee_id && (
                          <span className="ml-auto" title={userName(o.assignee_id)}>
                            <Avatar name={userName(o.assignee_id)} size={22} />
                          </span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="scroll-thin min-h-0 flex-1 overflow-auto p-4 sm:px-6">
          <table className="w-full min-w-[760px] overflow-hidden rounded-xl bg-white text-sm shadow-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th className="px-3 py-2.5">№</th>
                <th className="px-3 py-2.5">Zakaz</th>
                <th className="px-3 py-2.5">Holat</th>
                <th className="px-3 py-2.5 text-right">Narx</th>
                <th className="px-3 py-2.5 text-right">Qarz</th>
                <th className="px-3 py-2.5">Muddat</th>
                <th className="px-3 py-2.5">Mas'ul</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((o) => {
                const st = statusById(o.status_id);
                const dl = deadlineState(o.deadline, !!(st?.is_done || st?.is_cancel));
                return (
                  <tr key={o.id} onClick={() => navigate(`/orders/${o.id}`)} className="cursor-pointer border-t border-slate-100 hover:bg-slate-50">
                    <td className="px-3 py-2.5 text-slate-400">{o.number}</td>
                    <td className="px-3 py-2.5">
                      <div className="font-medium text-slate-900">{o.title}</div>
                      <div className="text-xs text-slate-500">{o.client_name}</div>
                    </td>
                    <td className="px-3 py-2.5">
                      <StatusPill status={st} />
                    </td>
                    <td className="px-3 py-2.5 text-right tabular-nums">{formatSum(o.price, false)}</td>
                    <td className={cx("px-3 py-2.5 text-right tabular-nums", o.debt > 0 && "text-red-600")}>{o.debt ? formatSum(o.debt, false) : "—"}</td>
                    <td className={cx("px-3 py-2.5", dl === "overdue" && "text-red-600", dl && dl !== "overdue" && "text-amber-600")}>
                      {formatDate(o.deadline)}
                    </td>
                    <td className="px-3 py-2.5 text-slate-600">{o.assignee_id ? userName(o.assignee_id) : "—"}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <OrderDrawer orderId={orderId ? Number(orderId) : null} onClose={() => navigate("/orders")} />
      <OrderForm open={creating} draft={{}} onClose={() => setCreating(false)} onCreated={(o) => navigate(`/orders/${o.id}`)} />
    </div>
  );
}
