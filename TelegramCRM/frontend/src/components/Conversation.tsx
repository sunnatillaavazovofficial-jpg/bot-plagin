import { ArrowLeft, Bell, BellOff, ClipboardPlus, EyeOff, Folder as FolderIcon, Info, Lock, MoreVertical, Paperclip, Send, X } from "lucide-react";
import { useCallback, useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent } from "react";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { dayLabel, fileSize } from "../lib/format";
import { useLive } from "../lib/live";
import type { BotButton, Chat, Message } from "../lib/types";
import { KIND_SINGULAR, KindIcon } from "./chatKinds";
import MessageBubble from "./MessageBubble";
import type { OrderDraft } from "./OrderForm";
import { AccountTag, Avatar, Button, Spinner, cx } from "./ui";

export default function Conversation({
  chat,
  onBack,
  onToggleInfo,
  onCreateOrder,
}: {
  chat: Chat;
  onBack: () => void;
  onToggleInfo: () => void;
  onCreateOrder: (draft: OrderDraft) => void;
}) {
  const { accountById, userName, toast, folders, isManager } = useApp();
  const [menuOpen, setMenuOpen] = useState(false);
  const [messages, setMessages] = useState<Message[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [text, setText] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [asDocument, setAsDocument] = useState(false);
  const [replyTo, setReplyTo] = useState<Message | null>(null);
  const [sending, setSending] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const textRef = useRef<HTMLTextAreaElement>(null);
  const stickBottom = useRef(true);
  const prevHeight = useRef<number | null>(null);
  const account = accountById(chat.account_id);

  const markRead = useCallback(() => {
    if (document.visibilityState === "visible") api.post(`/api/chats/${chat.id}/read`).catch(() => undefined);
  }, [chat.id]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setMessages([]);
    setReplyTo(null);
    setFile(null);
    setText(localStorage.getItem(`draft:${chat.id}`) ?? "");
    stickBottom.current = true;
    api
      .get<{ messages: Message[]; has_more: boolean }>(`/api/chats/${chat.id}/messages`, { limit: 50 })
      .then((r) => {
        if (cancelled) return;
        setMessages(r.messages);
        setHasMore(r.has_more);
      })
      .catch((e) => toast(e.message, "error"))
      .finally(() => !cancelled && setLoading(false));
    markRead();
    return () => {
      cancelled = true;
    };
  }, [chat.id, markRead, toast]);

  useEffect(() => {
    try {
      if (text) localStorage.setItem(`draft:${chat.id}`, text);
      else localStorage.removeItem(`draft:${chat.id}`);
    } catch {
      /* saqlab bo'lmadi */
    }
  }, [text, chat.id]);

  useLayoutEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    if (prevHeight.current !== null) {
      el.scrollTop = el.scrollHeight - prevHeight.current;
      prevHeight.current = null;
    } else if (stickBottom.current) {
      el.scrollTop = el.scrollHeight;
    }
  }, [messages]);

  useLive((e) => {
    if ((e.type === "message.new" || e.type === "message.edit") && e.message.chat_id === chat.id) {
      setMessages((list) => {
        const idx = list.findIndex((m) => m.id === e.message.id);
        if (idx >= 0) {
          const copy = [...list];
          copy[idx] = e.message;
          return copy;
        }
        if (e.type === "message.edit") return list;
        return [...list, e.message].sort((a, b) => a.tg_id - b.tg_id);
      });
      if (e.type === "message.new" && !e.message.out) markRead();
    }
  });

  async function loadOlder() {
    if (!messages.length || loadingOlder) return;
    setLoadingOlder(true);
    try {
      const r = await api.get<{ messages: Message[]; has_more: boolean }>(`/api/chats/${chat.id}/messages`, {
        before_id: messages[0].tg_id,
        limit: 50,
      });
      prevHeight.current = scrollRef.current?.scrollHeight ?? null;
      setMessages((list) => {
        const ids = new Set(list.map((m) => m.id));
        return [...r.messages.filter((m) => !ids.has(m.id)), ...list];
      });
      setHasMore(r.has_more && r.messages.length > 0);
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setLoadingOlder(false);
    }
  }

  function onScroll() {
    const el = scrollRef.current;
    if (!el) return;
    stickBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 120;
    if (el.scrollTop < 80 && hasMore && !loadingOlder) loadOlder();
  }

  async function patchChat(body: Record<string, unknown>, ok?: string) {
    setMenuOpen(false);
    try {
      await api.patch(`/api/chats/${chat.id}`, body);
      if (ok) toast(ok);
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function pressButton(m: Message, row: number, col: number, b: BotButton) {
    if (b.kind === "reply") {
      await sendText(b.text);
      return;
    }
    try {
      const r = await api.post<{ answer: string | null }>(`/api/messages/${m.id}/click`, { row, col });
      if (r.answer) toast(r.answer);
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function sendText(value: string) {
    const form = new FormData();
    form.set("text", value);
    try {
      const msg = await api.post<Message>(`/api/chats/${chat.id}/messages`, form);
      stickBottom.current = true;
      setMessages((list) => (list.some((m) => m.id === msg.id) ? list : [...list, msg]));
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function send() {
    if (sending || (!text.trim() && !file)) return;
    setSending(true);
    const form = new FormData();
    form.set("text", text.trim());
    if (replyTo) form.set("reply_to", String(replyTo.tg_id));
    if (file) {
      form.set("file", file);
      form.set("as_document", asDocument ? "true" : "false");
    }
    try {
      const msg = await api.post<Message>(`/api/chats/${chat.id}/messages`, form);
      stickBottom.current = true;
      setMessages((list) => (list.some((m) => m.id === msg.id) ? list.map((m) => (m.id === msg.id ? msg : m)) : [...list, msg]));
      setText("");
      setFile(null);
      setReplyTo(null);
      textRef.current?.focus();
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setSending(false);
    }
  }

  function onKey(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing && window.innerWidth >= 768) {
      e.preventDefault();
      send();
    }
  }

  function orderFrom(m: Message) {
    const texts = messages
      .filter((x) => !x.out && x.tg_id <= m.tg_id)
      .slice(-5)
      .map((x) => x.text)
      .filter(Boolean);
    onCreateOrder({
      title: (m.text || texts[texts.length - 1] || "").split("\n")[0].slice(0, 80),
      description: texts.join("\n"),
      chat_id: chat.id,
      client_id: chat.client_id,
      client_name: chat.title,
      message_ids: m.media_type ? [m.id] : [],
    });
  }

  const byTg = new Map(messages.map((m) => [m.tg_id, m]));
  let lastDay = "";

  return (
    <div className="flex h-full min-h-0 flex-col">
      <header className="flex items-center gap-3 border-b border-slate-200 bg-white px-3 py-2.5 sm:px-4">
        <button onClick={onBack} className="rounded-lg p-1.5 text-slate-500 hover:bg-slate-100 md:hidden" aria-label="Orqaga">
          <ArrowLeft size={20} />
        </button>
        <Avatar name={chat.title} size={38} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-1.5 font-semibold text-slate-900">
            {chat.kind !== "private" && <KindIcon kind={chat.kind} size={15} className="shrink-0 text-slate-400" />}
            <span className="truncate">{chat.title}</span>
            {chat.muted && <BellOff size={14} className="shrink-0 text-slate-300" />}
          </div>
          <div className="flex items-center gap-2 truncate text-xs text-slate-500">
            {account && <AccountTag name={account.name} color={account.color} />}
            {chat.kind !== "private" && <span>{KIND_SINGULAR[chat.kind]}</span>}
            {chat.phone && <span>{chat.phone}</span>}
            {chat.username && <span>@{chat.username}</span>}
          </div>
        </div>
        {chat.kind !== "channel" && (
          <Button
            variant="secondary"
            size="sm"
            onClick={() =>
              onCreateOrder({ chat_id: chat.id, client_id: chat.client_id, client_name: chat.title, title: "", description: "" })
            }
          >
            <ClipboardPlus size={15} /> <span className="hidden sm:inline">Zakaz</span>
          </Button>
        )}
        <button onClick={onToggleInfo} className="rounded-lg p-2 text-slate-500 hover:bg-slate-100" title="Mijoz ma'lumotlari">
          <Info size={19} />
        </button>
        {isManager && (
          <div className="relative">
            <button onClick={() => setMenuOpen((v) => !v)} className="rounded-lg p-2 text-slate-500 hover:bg-slate-100" title="Chat sozlamalari">
              <MoreVertical size={19} />
            </button>
            {menuOpen && (
              <>
                <div className="fixed inset-0 z-20" onClick={() => setMenuOpen(false)} />
                <div className="absolute right-0 top-11 z-30 w-60 rounded-xl border border-slate-200 bg-white py-1.5 text-sm shadow-lg">
                  <div className="px-3 pb-1 pt-1 text-xs font-medium text-slate-400">Papkaga joylash</div>
                  {folders.map((f) => (
                    <button
                      key={f.id}
                      onClick={() => patchChat({ folder_id: f.id }, `«${f.name}» papkasiga joylandi`)}
                      className="flex w-full items-center gap-2 px-3 py-1.5 text-left hover:bg-slate-50"
                    >
                      <FolderIcon size={15} style={{ color: f.color }} />
                      <span className="flex-1 truncate">{f.name}</span>
                      {chat.folder_id === f.id && <span className="text-blue-600">✓</span>}
                    </button>
                  ))}
                  {chat.folder_id !== null && (
                    <button onClick={() => patchChat({ folder_id: null })} className="w-full px-3 py-1.5 text-left text-slate-500 hover:bg-slate-50">
                      Papkadan chiqarish
                    </button>
                  )}
                  {folders.length === 0 && <div className="px-3 py-1.5 text-xs text-slate-400">Papka yo'q — Sozlamalar → Chatlarni tanlash</div>}
                  <div className="my-1 border-t border-slate-100" />
                  <button
                    onClick={() => patchChat({ muted: !chat.muted }, chat.muted ? "Ovoz yoqildi" : "Ovozsiz qilindi")}
                    className="flex w-full items-center gap-2 px-3 py-1.5 text-left hover:bg-slate-50"
                  >
                    {chat.muted ? <Bell size={15} /> : <BellOff size={15} />}
                    {chat.muted ? "Ovozni yoqish" : "Ovozsiz qilish (hisoblanmaydi)"}
                  </button>
                  <button
                    onClick={async () => {
                      await patchChat({ visible: false }, "Chat yashirildi. Qaytarish: Chatlarni tanlash");
                      onBack();
                    }}
                    className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-red-600 hover:bg-red-50"
                  >
                    <EyeOff size={15} /> Saytdan yashirish
                  </button>
                </div>
              </>
            )}
          </div>
        )}
      </header>

      <div ref={scrollRef} onScroll={onScroll} className="chat-bg scroll-thin min-h-0 flex-1 overflow-y-auto px-3 py-4 sm:px-6">
        {loading ? (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        ) : (
          <div className="mx-auto max-w-3xl space-y-1.5">
            {hasMore && (
              <div className="flex justify-center pb-2">
                <Button variant="secondary" size="sm" onClick={loadOlder} disabled={loadingOlder}>
                  {loadingOlder ? "Yuklanmoqda..." : "Oldingi xabarlar"}
                </Button>
              </div>
            )}
            {messages.map((m) => {
              const day = new Date(m.date).toDateString();
              const showDay = day !== lastDay;
              lastDay = day;
              const reply = m.reply_to_tg_id ? byTg.get(m.reply_to_tg_id) : null;
              return (
                <div key={m.id}>
                  {showDay && (
                    <div className="my-3 flex justify-center">
                      <span className="rounded-full bg-slate-900/40 px-3 py-0.5 text-xs font-medium text-white">{dayLabel(m.date)}</span>
                    </div>
                  )}
                  <MessageBubble
                    m={m}
                    read={m.out && m.tg_id <= chat.read_outbox_max_id}
                    senderLabel={m.out && m.sent_by_user_id ? userName(m.sent_by_user_id) : null}
                    replyText={reply ? reply.text || (reply.media_type ? "Media" : "") : null}
                    onOrder={chat.kind === "channel" ? undefined : () => orderFrom(m)}
                    onButton={(row, col, b) => pressButton(m, row, col, b)}
                    onReply={() => {
                      setReplyTo(m);
                      textRef.current?.focus();
                    }}
                  />
                </div>
              );
            })}
            {!messages.length && <div className="py-10 text-center text-sm text-slate-500">Xabarlar yo'q</div>}
          </div>
        )}
      </div>

      {!chat.can_send ? (
        <div className="flex items-center justify-center gap-2 border-t border-slate-200 bg-white px-4 py-4 text-sm text-slate-500">
          <Lock size={15} /> Bu kanalga yozish huquqi yo'q — faqat o'qish mumkin
        </div>
      ) : (
      <div className="border-t border-slate-200 bg-white px-3 py-2.5 sm:px-4">
        {(replyTo || file) && (
          <div className="mx-auto mb-2 max-w-3xl space-y-1.5">
            {replyTo && (
              <div className="flex items-center gap-2 rounded-lg bg-blue-50 px-3 py-1.5 text-sm">
                <span className="text-blue-600">Javob:</span>
                <span className="min-w-0 flex-1 truncate text-slate-600">{replyTo.text || "Media"}</span>
                <button onClick={() => setReplyTo(null)} className="text-slate-400 hover:text-slate-600">
                  <X size={16} />
                </button>
              </div>
            )}
            {file && (
              <div className="flex flex-wrap items-center gap-2 rounded-lg bg-slate-100 px-3 py-1.5 text-sm">
                <Paperclip size={15} className="text-slate-500" />
                <span className="min-w-0 flex-1 truncate">{file.name}</span>
                <span className="text-xs text-slate-500">{fileSize(file.size)}</span>
                {file.type.startsWith("image/") && (
                  <label className="flex items-center gap-1 text-xs text-slate-600">
                    <input type="checkbox" checked={asDocument} onChange={(e) => setAsDocument(e.target.checked)} />
                    sifatini saqlash (fayl qilib)
                  </label>
                )}
                <button onClick={() => setFile(null)} className="text-slate-400 hover:text-slate-600">
                  <X size={16} />
                </button>
              </div>
            )}
          </div>
        )}
        <div className="mx-auto flex max-w-3xl items-end gap-2">
          <input
            ref={fileRef}
            type="file"
            className="hidden"
            onChange={(e) => {
              setFile(e.target.files?.[0] ?? null);
              setAsDocument(false);
              e.target.value = "";
            }}
          />
          <button
            onClick={() => fileRef.current?.click()}
            className="rounded-lg p-2.5 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
            title="Fayl biriktirish"
          >
            <Paperclip size={20} />
          </button>
          <textarea
            ref={textRef}
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={onKey}
            rows={Math.min(6, Math.max(1, text.split("\n").length))}
            placeholder={account ? `${account.name} akkauntidan yozish...` : "Xabar yozing..."}
            className="max-h-40 min-h-[42px] flex-1 resize-none rounded-xl border border-slate-200 bg-slate-50 px-3.5 py-2.5 text-[14.5px] outline-none focus:border-blue-500 focus:bg-white"
          />
          <button
            onClick={send}
            disabled={sending || (!text.trim() && !file)}
            className={cx(
              "flex h-[42px] w-[42px] shrink-0 items-center justify-center rounded-xl text-white transition",
              sending || (!text.trim() && !file) ? "bg-slate-300" : "bg-blue-600 hover:bg-blue-700",
            )}
            title="Yuborish (Enter)"
          >
            {sending ? <Spinner className="h-4 w-4 border-white/40 border-t-white" /> : <Send size={18} />}
          </button>
        </div>
      </div>
      )}
    </div>
  );
}
