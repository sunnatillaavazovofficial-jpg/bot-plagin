import { MessageCircle, Search } from "lucide-react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import ChatSidePanel from "../components/ChatSidePanel";
import Conversation from "../components/Conversation";
import OrderForm, { type OrderDraft } from "../components/OrderForm";
import { Avatar, Empty, Input, Spinner, cx } from "../components/ui";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { chatTime, minutesAgo, waitLabel } from "../lib/format";
import { useLive } from "../lib/live";
import type { Chat } from "../lib/types";

const FILTERS = [
  { value: "all", label: "Hammasi" },
  { value: "unread", label: "O'qilmagan" },
  { value: "unanswered", label: "Javobsiz" },
  { value: "mine", label: "Meniki" },
] as const;

const PAGE = 60;

export default function InboxPage() {
  const { chatId } = useParams();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const { accounts, accountById, settings } = useApp();
  const filter = params.get("filter") ?? "all";
  const accountFilter = params.get("account") ?? "";
  const [q, setQ] = useState("");
  const [debouncedQ, setDebouncedQ] = useState("");
  const [chats, setChats] = useState<Chat[]>([]);
  const [loading, setLoading] = useState(true);
  const [hasMore, setHasMore] = useState(false);
  const [showInfo, setShowInfo] = useState(() => window.innerWidth >= 1280);
  const [draft, setDraft] = useState<OrderDraft | null>(null);
  const [panelKey, setPanelKey] = useState(0);
  const selectedId = chatId ? Number(chatId) : null;
  const [selected, setSelected] = useState<Chat | null>(null);
  const reloadTimer = useRef<number | undefined>(undefined);
  const waitMinutes = Number(settings?.values.unanswered_minutes ?? 30);

  useEffect(() => {
    const t = window.setTimeout(() => setDebouncedQ(q), 300);
    return () => window.clearTimeout(t);
  }, [q]);

  const load = useCallback(
    async (append = false, offset = 0) => {
      try {
        const list = await api.get<Chat[]>("/api/chats", {
          filter,
          account_id: accountFilter || undefined,
          q: debouncedQ || undefined,
          limit: PAGE,
          offset,
        });
        setChats((prev) => (append ? [...prev, ...list.filter((c) => !prev.some((p) => p.id === c.id))] : list));
        setHasMore(list.length === PAGE);
      } finally {
        setLoading(false);
      }
    },
    [filter, accountFilter, debouncedQ],
  );

  useEffect(() => {
    setLoading(true);
    load();
  }, [load]);

  useEffect(() => {
    if (!selectedId) {
      setSelected(null);
      return;
    }
    const inList = chats.find((c) => c.id === selectedId);
    if (inList) setSelected(inList);
    else api.get<Chat>(`/api/chats/${selectedId}`).then(setSelected).catch(() => navigate("/chats"));
  }, [selectedId, chats, navigate]);

  const scheduleReload = useCallback(() => {
    window.clearTimeout(reloadTimer.current);
    reloadTimer.current = window.setTimeout(() => load(), 500);
  }, [load]);

  useLive((e) => {
    if (e.type === "message.new" || e.type === "message.edit" || e.type === "chat.update") {
      const chat = e.chat;
      setChats((list) => {
        const idx = list.findIndex((c) => c.id === chat.id);
        if (idx < 0) {
          if (e.type === "message.new") scheduleReload();
          return list;
        }
        const copy = [...list];
        copy[idx] = chat;
        return copy.sort((a, b) => (b.last_message_at ?? "").localeCompare(a.last_message_at ?? ""));
      });
      if (chat.id === selectedId) setSelected(chat);
    } else if (e.type === "chats.reload" || e.type === "account.deleted") {
      scheduleReload();
    }
  });

  function setFilterParam(key: string, value: string) {
    const next = new URLSearchParams(params);
    if (value && value !== "all") next.set(key, value);
    else next.delete(key);
    setParams(next, { replace: true });
  }

  const visibleAccounts = useMemo(() => accounts.filter((a) => a.status !== "logged_out"), [accounts]);

  return (
    <div className="flex h-full min-h-0">
      {/* Chatlar ro'yxati */}
      <section
        className={cx(
          "flex min-h-0 w-full shrink-0 flex-col border-r border-slate-200 bg-white md:w-80 lg:w-96",
          selectedId && "max-md:hidden",
        )}
      >
        <div className="space-y-2.5 border-b border-slate-100 p-3">
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
            <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Ism, telefon yoki matn..." className="pl-9" />
          </div>
          <div className="flex gap-1 overflow-x-auto">
            {FILTERS.map((f) => (
              <button
                key={f.value}
                onClick={() => setFilterParam("filter", f.value)}
                className={cx(
                  "whitespace-nowrap rounded-full px-3 py-1 text-xs font-medium transition",
                  filter === f.value ? "bg-slate-900 text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200",
                )}
              >
                {f.label}
              </button>
            ))}
          </div>
          {visibleAccounts.length > 1 && (
            <div className="scroll-thin flex gap-1.5 overflow-x-auto pb-0.5">
              <button
                onClick={() => setFilterParam("account", "")}
                className={cx(
                  "whitespace-nowrap rounded-lg border px-2.5 py-1 text-xs font-medium",
                  !accountFilter ? "border-slate-900 text-slate-900" : "border-slate-200 text-slate-500",
                )}
              >
                Barcha akkauntlar
              </button>
              {visibleAccounts.map((a) => (
                <button
                  key={a.id}
                  onClick={() => setFilterParam("account", String(a.id))}
                  className={cx(
                    "flex items-center gap-1.5 whitespace-nowrap rounded-lg border px-2.5 py-1 text-xs font-medium",
                    accountFilter === String(a.id) ? "text-slate-900" : "border-slate-200 text-slate-500",
                  )}
                  style={accountFilter === String(a.id) ? { borderColor: a.color } : undefined}
                >
                  <span className="h-2 w-2 rounded-full" style={{ background: a.color }} />
                  {a.name}
                  {!!a.unread && <span className="text-slate-400">{a.unread}</span>}
                  {a.status === "error" && <span className="text-red-500">!</span>}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="scroll-thin min-h-0 flex-1 overflow-y-auto">
          {loading ? (
            <div className="flex justify-center py-10">
              <Spinner />
            </div>
          ) : chats.length === 0 ? (
            <Empty
              icon={<MessageCircle size={40} />}
              title={accounts.length ? "Chat topilmadi" : "Akkaunt ulanmagan"}
              text={accounts.length ? "Filtrni o'zgartirib ko'ring" : "Sozlamalar → Akkauntlar bo'limida Telegram akkauntingizni ulang"}
            />
          ) : (
            <>
              {chats.map((c) => {
                const acc = accountById(c.account_id);
                const waiting = !c.last_message_out && c.last_incoming_at && minutesAgo(c.last_incoming_at) >= waitMinutes;
                return (
                  <button
                    key={c.id}
                    onClick={() => navigate(`/chats/${c.id}${params.toString() ? `?${params}` : ""}`)}
                    className={cx(
                      "flex w-full items-center gap-3 border-b border-slate-50 px-3 py-2.5 text-left transition",
                      c.id === selectedId ? "bg-blue-50" : "hover:bg-slate-50",
                    )}
                  >
                    <div className="relative">
                      <Avatar name={c.title} size={46} />
                      {acc && (
                        <span
                          className="absolute -bottom-0.5 -right-0.5 h-4 w-4 rounded-full border-2 border-white"
                          style={{ background: acc.color }}
                          title={acc.name}
                        />
                      )}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-baseline justify-between gap-2">
                        <span className={cx("truncate text-[15px]", c.unread_count ? "font-semibold text-slate-900" : "font-medium text-slate-800")}>
                          {c.title}
                        </span>
                        <span className={cx("shrink-0 text-xs", c.unread_count ? "text-blue-600" : "text-slate-400")}>
                          {chatTime(c.last_message_at)}
                        </span>
                      </div>
                      <div className="mt-0.5 flex items-center gap-2">
                        <span className="min-w-0 flex-1 truncate text-sm text-slate-500">
                          {c.last_message_out && <span className="text-slate-400">Siz: </span>}
                          {c.last_message_text}
                        </span>
                        {waiting && (
                          <span className="shrink-0 rounded bg-red-50 px-1.5 py-0.5 text-[10px] font-semibold text-red-600">
                            {waitLabel(c.last_incoming_at)}
                          </span>
                        )}
                        {c.unread_count > 0 && (
                          <span className="flex h-5 min-w-5 shrink-0 items-center justify-center rounded-full bg-blue-600 px-1.5 text-[11px] font-semibold text-white">
                            {c.unread_count}
                          </span>
                        )}
                      </div>
                      {acc && visibleAccounts.length > 1 && (
                        <div className="mt-0.5 text-[11px] font-medium" style={{ color: acc.color }}>
                          {acc.name}
                        </div>
                      )}
                    </div>
                  </button>
                );
              })}
              {hasMore && (
                <button onClick={() => load(true, chats.length)} className="w-full py-3 text-sm text-blue-600 hover:bg-slate-50">
                  Yana yuklash
                </button>
              )}
            </>
          )}
        </div>
      </section>

      {/* Yozishma */}
      <section className={cx("min-h-0 min-w-0 flex-1", !selectedId && "max-md:hidden")}>
        {selected ? (
          <Conversation
            chat={selected}
            onBack={() => navigate(`/chats${params.toString() ? `?${params}` : ""}`)}
            onToggleInfo={() => setShowInfo((v) => !v)}
            onCreateOrder={setDraft}
          />
        ) : (
          <div className="chat-bg flex h-full items-center justify-center">
            <div className="rounded-full bg-slate-900/30 px-4 py-1.5 text-sm text-white">Chatni tanlang</div>
          </div>
        )}
      </section>

      {/* Mijoz paneli */}
      {selected && showInfo && (
        <aside className="fixed inset-0 z-30 bg-white md:static md:z-auto md:w-80 md:shrink-0 md:border-l md:border-slate-200">
          <ChatSidePanel chatId={selected.id} onClose={() => setShowInfo(false)} onCreateOrder={setDraft} refreshKey={panelKey} />
        </aside>
      )}

      <OrderForm
        open={draft !== null}
        draft={draft ?? {}}
        onClose={() => setDraft(null)}
        onCreated={() => setPanelKey((k) => k + 1)}
      />
    </div>
  );
}
