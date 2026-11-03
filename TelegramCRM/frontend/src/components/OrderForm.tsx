import { useEffect, useState, type FormEvent } from "react";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { formatSum, parseSum, todayISO } from "../lib/format";
import type { Client, Order } from "../lib/types";
import { Button, Field, Input, Modal, Select, Textarea } from "./ui";

export interface OrderDraft {
  title?: string;
  description?: string;
  chat_id?: number | null;
  client_id?: number | null;
  client_name?: string | null;
  message_ids?: number[];
}

export function SumInput({ value, onChange, placeholder }: { value: number; onChange: (v: number) => void; placeholder?: string }) {
  return (
    <Input
      inputMode="numeric"
      value={value ? formatSum(value, false) : ""}
      placeholder={placeholder ?? "0"}
      onChange={(e) => onChange(parseSum(e.target.value))}
    />
  );
}

export default function OrderForm({
  open,
  draft,
  onClose,
  onCreated,
}: {
  open: boolean;
  draft: OrderDraft;
  onClose: () => void;
  onCreated: (o: Order) => void;
}) {
  const { statuses, users, toast } = useApp();
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [price, setPrice] = useState(0);
  const [deadline, setDeadline] = useState("");
  const [assignee, setAssignee] = useState<string>("");
  const [statusId, setStatusId] = useState<string>("");
  const [clientId, setClientId] = useState<number | null>(null);
  const [clientQuery, setClientQuery] = useState("");
  const [clientOptions, setClientOptions] = useState<Client[]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTitle(draft.title ?? "");
    setDescription(draft.description ?? "");
    setPrice(0);
    setDeadline("");
    setAssignee("");
    setStatusId(statuses[0] ? String(statuses[0].id) : "");
    setClientId(draft.client_id ?? null);
    setClientQuery(draft.client_name ?? "");
  }, [open, draft, statuses]);

  useEffect(() => {
    if (!open || draft.chat_id || clientId || clientQuery.length < 2) {
      setClientOptions([]);
      return;
    }
    const t = window.setTimeout(() => {
      api.get<Client[]>("/api/clients", { q: clientQuery, limit: 8 }).then(setClientOptions).catch(() => undefined);
    }, 250);
    return () => window.clearTimeout(t);
  }, [clientQuery, clientId, open, draft.chat_id]);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      let cid = clientId;
      if (!draft.chat_id && !cid && clientQuery.trim()) {
        const c = await api.post<Client>("/api/clients", { name: clientQuery.trim() });
        cid = c.id;
      }
      const order = await api.post<Order>("/api/orders", {
        title,
        description,
        price,
        deadline: deadline || null,
        assignee_id: assignee ? Number(assignee) : null,
        status_id: statusId ? Number(statusId) : null,
        chat_id: draft.chat_id ?? null,
        client_id: cid,
        message_ids: draft.message_ids ?? [],
      });
      toast(`Zakaz ${order.number} yaratildi`);
      onCreated(order);
      onClose();
    } catch (err) {
      toast((err as Error).message, "error");
    } finally {
      setBusy(false);
    }
  }

  const active = users.filter((u) => u.is_active);

  return (
    <Modal open={open} onClose={onClose} title="Yangi zakaz" wide>
      <form onSubmit={submit} className="space-y-3">
        <Field label="Nomi *">
          <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Masalan: Hajmli harf «NUR MARKET»" autoFocus required />
        </Field>
        <Field label="Tavsif (o'lcham, material, rang...)">
          <Textarea rows={3} value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
        {draft.chat_id ? (
          <div className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-600">
            Mijoz: <b>{draft.client_name || "chatdan avtomatik yaratiladi"}</b>
            {!!draft.message_ids?.length && <span className="ml-2 text-slate-400">· {draft.message_ids.length} ta xabar fayllari qo'shiladi</span>}
          </div>
        ) : (
          <Field label="Mijoz">
            <div className="relative">
              <Input
                value={clientQuery}
                onChange={(e) => {
                  setClientQuery(e.target.value);
                  setClientId(null);
                }}
                placeholder="Ism yoki telefon (yangi bo'lsa yaratiladi)"
              />
              {clientOptions.length > 0 && (
                <div className="absolute inset-x-0 top-11 z-10 rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
                  {clientOptions.map((c) => (
                    <button
                      type="button"
                      key={c.id}
                      className="block w-full px-3 py-2 text-left text-sm hover:bg-slate-50"
                      onClick={() => {
                        setClientId(c.id);
                        setClientQuery(c.name);
                        setClientOptions([]);
                      }}
                    >
                      {c.name} <span className="text-slate-400">{c.phone}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          </Field>
        )}
        <div className="grid grid-cols-2 gap-3">
          <Field label="Narxi (so'm)">
            <SumInput value={price} onChange={setPrice} />
          </Field>
          <Field label="Muddat">
            <Input type="date" value={deadline} min={todayISO()} onChange={(e) => setDeadline(e.target.value)} />
          </Field>
          <Field label="Mas'ul xodim">
            <Select value={assignee} onChange={(e) => setAssignee(e.target.value)}>
              <option value="">— Tanlanmagan —</option>
              {active.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Holat">
            <Select value={statusId} onChange={(e) => setStatusId(e.target.value)}>
              {statuses.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Bekor qilish
          </Button>
          <Button type="submit" disabled={busy || !title.trim()}>
            {busy ? "Saqlanmoqda..." : "Yaratish"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
