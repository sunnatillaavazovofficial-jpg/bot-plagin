import { ClipboardPlus, ExternalLink, UserPlus, X } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { deadlineState, formatDate, formatSum } from "../lib/format";
import { useLive } from "../lib/live";
import type { ChatDetail, Client } from "../lib/types";
import type { OrderDraft } from "./OrderForm";
import { Button, Input, Select, StatusPill, cx } from "./ui";

export default function ChatSidePanel({
  chatId,
  onClose,
  onCreateOrder,
  refreshKey,
}: {
  chatId: number;
  onClose: () => void;
  onCreateOrder: (draft: OrderDraft) => void;
  refreshKey: number;
}) {
  const { users, statusById, isManager, toast } = useApp();
  const [detail, setDetail] = useState<ChatDetail | null>(null);
  const [linking, setLinking] = useState(false);
  const [query, setQuery] = useState("");
  const [options, setOptions] = useState<Client[]>([]);

  const load = useCallback(() => {
    api.get<ChatDetail>(`/api/chats/${chatId}`).then(setDetail).catch(() => undefined);
  }, [chatId]);

  useEffect(() => {
    setDetail(null);
    setLinking(false);
    load();
  }, [load, refreshKey]);

  useLive((e) => {
    if (e.type === "order.update" || (e.type === "chat.update" && e.chat.id === chatId)) load();
  });

  useEffect(() => {
    if (!linking || query.length < 2) {
      setOptions([]);
      return;
    }
    const t = window.setTimeout(() => api.get<Client[]>("/api/clients", { q: query, limit: 8 }).then(setOptions), 250);
    return () => window.clearTimeout(t);
  }, [query, linking]);

  async function makeClient() {
    try {
      await api.post(`/api/chats/${chatId}/client`);
      toast("Mijoz yaratildi");
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function patch(body: Record<string, unknown>) {
    try {
      await api.patch(`/api/chats/${chatId}`, body);
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  if (!detail) return <div className="p-5 text-sm text-slate-400">Yuklanmoqda...</div>;
  const client = detail.client;

  return (
    <div className="flex h-full flex-col overflow-y-auto">
      <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
        <div className="font-semibold text-slate-900">Ma'lumot</div>
        <button onClick={onClose} className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100">
          <X size={18} />
        </button>
      </div>

      <section className="space-y-3 border-b border-slate-100 p-4">
        <div className="text-xs font-semibold uppercase tracking-wide text-slate-400">Mijoz</div>
        {client ? (
          <div className="rounded-xl bg-slate-50 p-3">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="font-medium text-slate-900">{client.name}</div>
                {client.phone && <div className="text-sm text-slate-500">{client.phone}</div>}
                {client.company && <div className="text-sm text-slate-500">{client.company}</div>}
              </div>
              {isManager && (
                <Link to={`/clients/${client.id}`} className="rounded-lg p-1.5 text-slate-400 hover:bg-white hover:text-blue-600" title="Mijoz kartasi">
                  <ExternalLink size={16} />
                </Link>
              )}
            </div>
            {client.notes && <div className="mt-2 whitespace-pre-wrap text-sm text-slate-600">{client.notes}</div>}
            {isManager && (
              <button onClick={() => patch({ client_id: null })} className="mt-2 text-xs text-slate-400 hover:text-red-600">
                Mijozdan ajratish
              </button>
            )}
          </div>
        ) : isManager ? (
          <div className="space-y-2">
            <Button variant="secondary" className="w-full" onClick={makeClient}>
              <UserPlus size={16} /> Mijoz sifatida saqlash
            </Button>
            {linking ? (
              <div className="relative">
                <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Mavjud mijozni qidirish..." autoFocus />
                {options.length > 0 && (
                  <div className="absolute inset-x-0 top-11 z-10 rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
                    {options.map((c) => (
                      <button
                        key={c.id}
                        className="block w-full px-3 py-2 text-left text-sm hover:bg-slate-50"
                        onClick={() => {
                          patch({ client_id: c.id });
                          setLinking(false);
                        }}
                      >
                        {c.name} <span className="text-slate-400">{c.phone}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ) : (
              <button onClick={() => setLinking(true)} className="w-full text-center text-xs text-blue-600 hover:underline">
                yoki mavjud mijozga bog'lash
              </button>
            )}
          </div>
        ) : (
          <div className="text-sm text-slate-400">Mijozga bog'lanmagan</div>
        )}
      </section>

      <section className="space-y-2 border-b border-slate-100 p-4">
        <div className="text-xs font-semibold uppercase tracking-wide text-slate-400">Mas'ul xodim</div>
        <Select
          value={detail.assigned_user_id ?? ""}
          onChange={(e) => patch({ assigned_user_id: e.target.value ? Number(e.target.value) : null })}
        >
          <option value="">— Biriktirilmagan —</option>
          {users
            .filter((u) => u.is_active)
            .map((u) => (
              <option key={u.id} value={u.id}>
                {u.name}
              </option>
            ))}
        </Select>
      </section>

      <section className="flex-1 space-y-2 p-4">
        <div className="flex items-center justify-between">
          <div className="text-xs font-semibold uppercase tracking-wide text-slate-400">Zakazlar</div>
          {isManager && (
            <Button
              size="sm"
              variant="ghost"
              onClick={() => onCreateOrder({ chat_id: chatId, client_id: detail.client_id, client_name: client?.name ?? detail.title })}
            >
              <ClipboardPlus size={14} /> Yangi
            </Button>
          )}
        </div>
        {detail.orders.length === 0 && <div className="text-sm text-slate-400">Hali zakaz yo'q</div>}
        {detail.orders.map((o) => {
          const st = statusById(o.status_id);
          const dl = deadlineState(o.deadline, !!(st?.is_done || st?.is_cancel));
          return (
            <Link key={o.id} to={`/orders/${o.id}`} className="block rounded-xl border border-slate-200 p-3 hover:border-blue-300 hover:bg-blue-50/40">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="text-xs text-slate-400">{o.number}</div>
                  <div className="truncate text-sm font-medium text-slate-900">{o.title}</div>
                </div>
                <StatusPill status={st} />
              </div>
              <div className="mt-2 flex items-center justify-between text-xs">
                <span className="text-slate-600">{formatSum(o.price)}</span>
                {o.debt > 0 && <span className="text-red-600">qarz {formatSum(o.debt)}</span>}
                {o.deadline && (
                  <span className={cx(dl === "overdue" ? "text-red-600" : dl ? "text-amber-600" : "text-slate-400")}>
                    {formatDate(o.deadline)}
                  </span>
                )}
              </div>
            </Link>
          );
        })}
      </section>
    </div>
  );
}
