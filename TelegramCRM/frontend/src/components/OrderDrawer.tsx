import { Download, ExternalLink, FileText, MessageCircle, Paperclip, Send, Trash2, X } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { deadlineState, fileSize, formatDate, formatDateTime, formatSum, PAYMENT_LABELS } from "../lib/format";
import { useLive } from "../lib/live";
import type { OrderDetail } from "../lib/types";
import { SumInput } from "./OrderForm";
import { Button, Drawer, Field, Input, Select, Spinner, Textarea, cx } from "./ui";

export default function OrderDrawer({ orderId, onClose }: { orderId: number | null; onClose: () => void }) {
  const { statuses, users, userName, statusById, isAdmin, isManager, accountById, toast } = useApp();
  const [order, setOrder] = useState<OrderDetail | null>(null);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [comment, setComment] = useState("");
  const [payAmount, setPayAmount] = useState(0);
  const [payMethod, setPayMethod] = useState("naqd");
  const [uploading, setUploading] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const closeRef = useRef(onClose);
  closeRef.current = onClose;

  const load = useCallback(async () => {
    if (!orderId) return;
    try {
      const o = await api.get<OrderDetail>(`/api/orders/${orderId}`);
      setOrder(o);
      setTitle(o.title);
      setDescription(o.description);
    } catch (e) {
      toast((e as Error).message, "error");
      closeRef.current();
    }
  }, [orderId, toast]);

  useEffect(() => {
    setOrder(null);
    load();
  }, [load]);

  useLive((e) => {
    if (e.type === "order.update" && e.order_id === orderId) {
      if (e.deleted) closeRef.current();
      else load();
    }
  });

  async function patch(body: Record<string, unknown>) {
    try {
      await api.patch(`/api/orders/${orderId}`, body);
      await load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function addComment() {
    if (!comment.trim()) return;
    try {
      await api.post(`/api/orders/${orderId}/comments`, { text: comment });
      setComment("");
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function addPayment() {
    if (!payAmount) return;
    try {
      await api.post(`/api/orders/${orderId}/payments`, { amount: payAmount, method: payMethod });
      setPayAmount(0);
      toast("To'lov qo'shildi");
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function upload(file: File) {
    setUploading(true);
    const form = new FormData();
    form.set("file", file);
    try {
      await api.post(`/api/orders/${orderId}/files`, form);
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setUploading(false);
    }
  }

  async function sendFile(fileId: number) {
    try {
      await api.post(`/api/order-files/${fileId}/send`, { caption: "" });
      toast("Fayl mijozga yuborildi");
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function remove(url: string, confirmText: string) {
    if (!window.confirm(confirmText)) return;
    try {
      await api.del(url);
      load();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function deleteOrder() {
    if (!window.confirm("Zakaz butunlay o'chirilsinmi?")) return;
    try {
      await api.del(`/api/orders/${orderId}`);
      toast("Zakaz o'chirildi");
      onClose();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  const st = order ? statusById(order.status_id) : undefined;
  const closed = !!(st?.is_done || st?.is_cancel);
  const dl = order ? deadlineState(order.deadline, closed) : null;
  const account = order ? accountById(order.account_id) : undefined;
  const editable = isManager;

  return (
    <Drawer open={orderId !== null} onClose={onClose}>
      {!order ? (
        <div className="flex justify-center p-10">
          <Spinner />
        </div>
      ) : (
        <div className="flex min-h-full flex-col">
          <div className="sticky top-0 z-10 flex items-start gap-3 border-b border-slate-100 bg-white px-5 py-4">
            <div className="min-w-0 flex-1">
              <div className="text-xs font-medium text-slate-400">
                {order.number} · {formatDateTime(order.created_at)}
              </div>
              {editable ? (
                <input
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  onBlur={() => title.trim() && title !== order.title && patch({ title })}
                  className="w-full rounded-md border border-transparent px-1 py-0.5 -ml-1 text-lg font-semibold text-slate-900 outline-none hover:border-slate-200 focus:border-blue-500"
                />
              ) : (
                <div className="text-lg font-semibold text-slate-900">{order.title}</div>
              )}
            </div>
            <button onClick={onClose} className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100">
              <X size={20} />
            </button>
          </div>

          <div className="space-y-6 p-5">
            <div className="grid grid-cols-2 gap-3">
              <Field label="Holat">
                <Select
                  value={order.status_id}
                  onChange={(e) => patch({ status_id: Number(e.target.value) })}
                  style={{ color: st?.color, fontWeight: 600 }}
                >
                  {statuses.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Mas'ul">
                <Select
                  value={order.assignee_id ?? ""}
                  disabled={!editable}
                  onChange={(e) => patch({ assignee_id: e.target.value ? Number(e.target.value) : null })}
                >
                  <option value="">— Tanlanmagan —</option>
                  {users
                    .filter((u) => u.is_active || u.id === order.assignee_id)
                    .map((u) => (
                      <option key={u.id} value={u.id}>
                        {u.name}
                      </option>
                    ))}
                </Select>
              </Field>
              <Field label="Muddat">
                <Input
                  type="date"
                  value={order.deadline ?? ""}
                  disabled={!editable}
                  onChange={(e) => patch({ deadline: e.target.value || null })}
                  className={cx(dl === "overdue" && "border-red-300 text-red-600", (dl === "today" || dl === "soon") && "border-amber-300 text-amber-700")}
                />
              </Field>
              <Field label="Narxi">
                {editable ? (
                  <PriceEditor value={order.price} onSave={(v) => patch({ price: v })} />
                ) : (
                  <div className="flex h-10 items-center text-sm">{formatSum(order.price)}</div>
                )}
              </Field>
            </div>

            <div className="flex flex-wrap gap-2 text-sm">
              {order.client_id && (
                <Link to={`/clients/${order.client_id}`} className="inline-flex items-center gap-1.5 rounded-lg bg-slate-100 px-3 py-1.5 text-slate-700 hover:bg-slate-200">
                  <ExternalLink size={14} /> {order.client_name} {order.client_phone && <span className="text-slate-400">{order.client_phone}</span>}
                </Link>
              )}
              {order.chat_id && (
                <Link to={`/chats/${order.chat_id}`} className="inline-flex items-center gap-1.5 rounded-lg bg-blue-50 px-3 py-1.5 text-blue-700 hover:bg-blue-100">
                  <MessageCircle size={14} /> Chatga o'tish {account && <span style={{ color: account.color }}>· {account.name}</span>}
                </Link>
              )}
            </div>

            <Field label="Tavsif">
              <Textarea
                rows={4}
                value={description}
                disabled={!editable}
                onChange={(e) => setDescription(e.target.value)}
                onBlur={() => description !== order.description && patch({ description })}
                placeholder="O'lcham, material, rang, manzil..."
              />
            </Field>

            {/* To'lovlar */}
            <section>
              <div className="mb-2 flex items-center justify-between">
                <h3 className="text-sm font-semibold text-slate-900">To'lovlar</h3>
                <div className="text-sm">
                  <span className="text-emerald-600">{formatSum(order.paid)}</span>
                  {order.debt > 0 && <span className="ml-2 font-medium text-red-600">qarz: {formatSum(order.debt)}</span>}
                </div>
              </div>
              {order.price > 0 && (
                <div className="mb-3 h-2 overflow-hidden rounded-full bg-slate-100">
                  <div className="h-full rounded-full bg-emerald-500" style={{ width: `${Math.min(100, (order.paid / order.price) * 100)}%` }} />
                </div>
              )}
              <div className="space-y-1">
                {order.payments.map((p) => (
                  <div key={p.id} className="flex items-center gap-2 rounded-lg bg-slate-50 px-3 py-2 text-sm">
                    <span className={cx("font-medium", p.amount < 0 ? "text-red-600" : "text-slate-900")}>{formatSum(p.amount)}</span>
                    <span className="text-slate-500">{PAYMENT_LABELS[p.method] ?? p.method}</span>
                    <span className="ml-auto text-xs text-slate-400">
                      {formatDateTime(p.created_at)} · {userName(p.created_by_id)}
                    </span>
                    {isAdmin && (
                      <button onClick={() => remove(`/api/payments/${p.id}`, "To'lov o'chirilsinmi?")} className="text-slate-300 hover:text-red-600">
                        <Trash2 size={14} />
                      </button>
                    )}
                  </div>
                ))}
              </div>
              {editable && (
                <div className="mt-2 flex gap-2">
                  <div className="flex-1">
                    <SumInput value={payAmount} onChange={setPayAmount} placeholder={order.debt ? `Qoldiq: ${formatSum(order.debt, false)}` : "Summa"} />
                  </div>
                  <Select value={payMethod} onChange={(e) => setPayMethod(e.target.value)} className="w-32">
                    {Object.entries(PAYMENT_LABELS).map(([k, v]) => (
                      <option key={k} value={k}>
                        {v}
                      </option>
                    ))}
                  </Select>
                  <Button onClick={addPayment} disabled={!payAmount}>
                    Qo'shish
                  </Button>
                </div>
              )}
            </section>

            {/* Fayllar */}
            <section>
              <div className="mb-2 flex items-center justify-between">
                <h3 className="text-sm font-semibold text-slate-900">Fayllar va maketlar</h3>
                <input ref={fileRef} type="file" className="hidden" onChange={(e) => e.target.files?.[0] && upload(e.target.files[0])} />
                <Button size="sm" variant="secondary" onClick={() => fileRef.current?.click()} disabled={uploading}>
                  <Paperclip size={14} /> {uploading ? "Yuklanmoqda..." : "Qo'shish"}
                </Button>
              </div>
              {order.files.length === 0 && <div className="text-sm text-slate-400">Fayl yo'q</div>}
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                {order.files.map((f) => (
                  <div key={f.id} className="group overflow-hidden rounded-xl border border-slate-200">
                    <a href={`/api/order-files/${f.id}`} target="_blank" rel="noreferrer" className="block aspect-[4/3] bg-slate-50">
                      {f.mime?.startsWith("image/") ? (
                        <img src={`/api/order-files/${f.id}`} alt={f.name} className="h-full w-full object-cover" loading="lazy" />
                      ) : (
                        <div className="flex h-full items-center justify-center text-slate-400">
                          <FileText size={30} />
                        </div>
                      )}
                    </a>
                    <div className="flex items-center gap-1 px-2 py-1.5">
                      <div className="min-w-0 flex-1">
                        <div className="truncate text-xs font-medium text-slate-700">{f.name}</div>
                        <div className="text-[11px] text-slate-400">{fileSize(f.size)}</div>
                      </div>
                      <a href={`/api/order-files/${f.id}?download=1`} className="p-1 text-slate-400 hover:text-blue-600" title="Yuklab olish">
                        <Download size={14} />
                      </a>
                      {order.chat_id && (
                        <button onClick={() => sendFile(f.id)} className="p-1 text-slate-400 hover:text-blue-600" title="Mijozga Telegram orqali yuborish">
                          <Send size={14} />
                        </button>
                      )}
                      {isManager && (
                        <button onClick={() => remove(`/api/order-files/${f.id}`, "Fayl o'chirilsinmi?")} className="p-1 text-slate-400 hover:text-red-600">
                          <Trash2 size={14} />
                        </button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </section>

            {/* Tarix */}
            <section>
              <h3 className="mb-2 text-sm font-semibold text-slate-900">Izohlar va tarix</h3>
              <div className="mb-3 flex gap-2">
                <Input
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                  onKeyDown={(e) => e.key === "Enter" && addComment()}
                  placeholder="Izoh yozing..."
                />
                <Button onClick={addComment} disabled={!comment.trim()}>
                  Yozish
                </Button>
              </div>
              <ol className="space-y-2">
                {order.events.map((ev) => (
                  <li key={ev.id} className={cx("rounded-lg px-3 py-2 text-sm", ev.kind === "comment" ? "bg-amber-50" : "bg-slate-50")}>
                    <div className="whitespace-pre-wrap text-slate-800">{ev.text}</div>
                    <div className="mt-0.5 text-xs text-slate-400">
                      {userName(ev.user_id)} · {formatDateTime(ev.created_at)}
                    </div>
                  </li>
                ))}
              </ol>
            </section>

            {isAdmin && (
              <div className="border-t border-slate-100 pt-4">
                <Button variant="ghost" size="sm" className="text-red-600" onClick={deleteOrder}>
                  <Trash2 size={14} /> Zakazni o'chirish
                </Button>
              </div>
            )}
            {order.closed_at && <div className="text-xs text-slate-400">Yopilgan: {formatDate(order.closed_at, true)}</div>}
          </div>
        </div>
      )}
    </Drawer>
  );
}

function PriceEditor({ value, onSave }: { value: number; onSave: (v: number) => void }) {
  const [v, setV] = useState(value);
  useEffect(() => setV(value), [value]);
  return (
    <div onBlur={() => v !== value && onSave(v)}>
      <SumInput value={v} onChange={setV} />
    </div>
  );
}
