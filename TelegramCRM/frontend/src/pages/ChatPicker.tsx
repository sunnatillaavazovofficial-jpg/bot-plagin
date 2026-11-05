import { BellOff, Eye, EyeOff, Folder as FolderIcon, Plus, Search, Trash2 } from "lucide-react";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { KIND_LABELS, KIND_SINGULAR, KINDS, KindIcon } from "../components/chatKinds";
import { AccountTag, Avatar, Button, Input, Select, Spinner, cx } from "../components/ui";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { chatTime } from "../lib/format";
import { useLive } from "../lib/live";
import type { Chat, ChatKind } from "../lib/types";

interface PickerResp {
  total: number;
  counts: Partial<Record<ChatKind, { visible: number; hidden: number }>>;
  chats: Chat[];
}

const PAGE = 200;
const FOLDER_COLORS = ["#2563eb", "#16a34a", "#ea580c", "#db2777", "#7c3aed", "#0891b2", "#ca8a04", "#64748b"];

/** «Chatlarni tanlash»: qaysi chat, guruh, kanal va bot saytda ko'rinishini belgilash, papkalarga joylash. */
export default function ChatPicker() {
  const { accounts, accountById, folders, reloadFolders, toast } = useApp();
  const [account, setAccount] = useState("");
  const [kind, setKind] = useState<ChatKind | "">("");
  const [visibility, setVisibility] = useState<"" | "true" | "false">("");
  const [q, setQ] = useState("");
  const [debouncedQ, setDebouncedQ] = useState("");
  const [data, setData] = useState<PickerResp | null>(null);
  const [extra, setExtra] = useState<Chat[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<Set<number>>(new Set());

  useEffect(() => {
    const t = window.setTimeout(() => setDebouncedQ(q), 300);
    return () => window.clearTimeout(t);
  }, [q]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const r = await api.get<PickerResp>("/api/chats/picker", {
        account_id: account || undefined,
        kind: kind || undefined,
        visible: visibility || undefined,
        q: debouncedQ || undefined,
        limit: PAGE,
      });
      setData(r);
      setExtra([]);
      setSelected(new Set());
    } catch (e) {
      toast((e as Error).message, "error");
    } finally {
      setLoading(false);
    }
  }, [account, kind, visibility, debouncedQ, toast]);

  useEffect(() => {
    load();
  }, [load]);

  useLive((e) => {
    if (e.type === "chats.reload" && e.account_id !== 0) load();
  });

  const chats = useMemo(() => [...(data?.chats ?? []), ...extra], [data, extra]);

  async function loadMore() {
    const r = await api.get<PickerResp>("/api/chats/picker", {
      account_id: account || undefined,
      kind: kind || undefined,
      visible: visibility || undefined,
      q: debouncedQ || undefined,
      limit: PAGE,
      offset: chats.length,
    });
    setExtra((list) => [...list, ...r.chats]);
  }

  function updateLocal(ids: number[], patch: Partial<Chat>) {
    const set = new Set(ids);
    setData((d) => (d ? { ...d, chats: d.chats.map((c) => (set.has(c.id) ? { ...c, ...patch } : c)) } : d));
    setExtra((list) => list.map((c) => (set.has(c.id) ? { ...c, ...patch } : c)));
  }

  async function bulk(ids: number[], body: Record<string, unknown>, ok: string) {
    if (!ids.length) return;
    try {
      await api.post("/api/chats/bulk", { chat_ids: ids, ...body });
      const patch: Partial<Chat> = {};
      if ("visible" in body) patch.visible = body.visible as boolean;
      if ("muted" in body) patch.muted = body.muted as boolean;
      if ("folder_id" in body) patch.folder_id = body.folder_id as number;
      if (body.clear_folder) patch.folder_id = null;
      updateLocal(ids, patch);
      toast(ok);
      reloadFolders();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  const selectedIds = [...selected];
  const allIds = chats.map((c) => c.id);
  const allSelected = chats.length > 0 && selected.size === chats.length;

  return (
    <div className="space-y-5">
      <FoldersCard />

      <section className="rounded-2xl bg-white shadow-sm ring-1 ring-slate-200">
        <div className="space-y-3 border-b border-slate-100 p-4">
          <div>
            <h2 className="font-semibold text-slate-900">Chatlarni tanlash</h2>
            <p className="text-sm text-slate-500">
              Belgilangan chatlar «Chatlar» oynasida ko'rinadi. Yashirin chatlarning xabarlari saytga yuklanmaydi.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <div className="relative min-w-48 flex-1">
              <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
              <Input value={q} onChange={(e) => setQ(e.target.value)} placeholder="Nom yoki @username..." className="pl-9" />
            </div>
            <Select value={account} onChange={(e) => setAccount(e.target.value)} className="w-auto">
              <option value="">Barcha akkauntlar</option>
              {accounts.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name}
                </option>
              ))}
            </Select>
            <Select value={visibility} onChange={(e) => setVisibility(e.target.value as "" | "true" | "false")} className="w-auto">
              <option value="">Ko'rinadigan va yashirin</option>
              <option value="true">Faqat ko'rinadiganlar</option>
              <option value="false">Faqat yashirinlar</option>
            </Select>
          </div>
          <div className="scroll-thin flex gap-1.5 overflow-x-auto">
            <KindTab active={kind === ""} onClick={() => setKind("")} label="Hammasi" />
            {KINDS.map((k) => {
              const c = data?.counts[k];
              return (
                <KindTab
                  key={k}
                  active={kind === k}
                  onClick={() => setKind(k)}
                  label={
                    <>
                      <KindIcon kind={k} size={14} /> {KIND_LABELS[k]}
                      {c && (
                        <span className="text-xs opacity-70">
                          {c.visible}/{c.visible + c.hidden}
                        </span>
                      )}
                    </>
                  }
                />
              );
            })}
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2 border-b border-slate-100 bg-slate-50 px-4 py-2.5 text-sm">
          <label className="mr-1 flex items-center gap-2">
            <input
              type="checkbox"
              checked={allSelected}
              onChange={(e) => setSelected(e.target.checked ? new Set(allIds) : new Set())}
            />
            {selected.size ? `${selected.size} ta tanlandi` : "Hammasini belgilash"}
          </label>
          {selected.size > 0 ? (
            <>
              <Button size="sm" variant="secondary" onClick={() => bulk(selectedIds, { visible: true }, "Ko'rsatildi")}>
                <Eye size={14} /> Ko'rsatish
              </Button>
              <Button size="sm" variant="secondary" onClick={() => bulk(selectedIds, { visible: false }, "Yashirildi")}>
                <EyeOff size={14} /> Yashirish
              </Button>
              <Button size="sm" variant="secondary" onClick={() => bulk(selectedIds, { muted: true }, "Ovozsiz qilindi")}>
                <BellOff size={14} /> Ovozsiz
              </Button>
              <Select
                value=""
                onChange={(e) => {
                  const v = e.target.value;
                  if (v === "none") bulk(selectedIds, { clear_folder: true }, "Papkadan chiqarildi");
                  else if (v) bulk(selectedIds, { folder_id: Number(v) }, "Papkaga joylandi");
                }}
                className="h-8 w-auto text-xs"
              >
                <option value="">Papkaga joylash...</option>
                {folders.map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.name}
                  </option>
                ))}
                <option value="none">— Papkasiz —</option>
              </Select>
            </>
          ) : (
            <span className="text-slate-500">
              {data ? `${data.total} ta chat` : ""} · ro'yxatdagilarni belgilab, birdan ko'rsatish yoki yashirish mumkin
            </span>
          )}
        </div>

        {loading ? (
          <div className="flex justify-center py-12">
            <Spinner />
          </div>
        ) : chats.length === 0 ? (
          <div className="py-12 text-center text-sm text-slate-500">Chat topilmadi</div>
        ) : (
          <ul className="divide-y divide-slate-100">
            {chats.map((c) => {
              const acc = accountById(c.account_id);
              return (
                <li key={c.id} className={cx("flex flex-wrap items-center gap-3 px-4 py-2.5", !c.visible && "bg-slate-50/70")}>
                  <input
                    type="checkbox"
                    checked={selected.has(c.id)}
                    onChange={(e) => {
                      const next = new Set(selected);
                      if (e.target.checked) next.add(c.id);
                      else next.delete(c.id);
                      setSelected(next);
                    }}
                    aria-label={`${c.title} ni belgilash`}
                  />
                  <div className={cx("flex min-w-0 flex-1 items-center gap-3", !c.visible && "opacity-60")}>
                    <Avatar name={c.title} size={36} />
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5 text-sm font-medium text-slate-900">
                        <KindIcon kind={c.kind} size={13} className="shrink-0 text-slate-400" />
                        <span className="truncate">{c.title}</span>
                        {c.muted && <BellOff size={12} className="shrink-0 text-slate-400" />}
                      </div>
                      <div className="flex items-center gap-2 text-xs text-slate-500">
                        {acc && <AccountTag name={acc.name} color={acc.color} />}
                        <span>{KIND_SINGULAR[c.kind]}</span>
                        {c.last_message_at && <span>{chatTime(c.last_message_at)}</span>}
                        {c.unread_count > 0 && <span className="text-blue-600">{c.unread_count} yangi</span>}
                      </div>
                    </div>
                  </div>
                  <Select
                    value={c.folder_id ?? ""}
                    onChange={(e) =>
                      e.target.value
                        ? bulk([c.id], { folder_id: Number(e.target.value) }, "Papkaga joylandi")
                        : bulk([c.id], { clear_folder: true }, "Papkadan chiqarildi")
                    }
                    className="h-8 w-36 text-xs"
                    aria-label="Papka"
                  >
                    <option value="">Papkasiz</option>
                    {folders.map((f) => (
                      <option key={f.id} value={f.id}>
                        {f.name}
                      </option>
                    ))}
                  </Select>
                  <button
                    onClick={() => bulk([c.id], { muted: !c.muted }, c.muted ? "Ovoz yoqildi" : "Ovozsiz qilindi")}
                    className={cx("rounded-lg p-1.5", c.muted ? "text-slate-700" : "text-slate-300 hover:text-slate-500")}
                    title={c.muted ? "Ovozsiz (o'qilmaganlar hisoblanmaydi)" : "Ovozsiz qilish"}
                  >
                    <BellOff size={16} />
                  </button>
                  <Toggle checked={c.visible} onChange={(v) => bulk([c.id], { visible: v }, v ? "Ko'rsatildi" : "Yashirildi")} />
                </li>
              );
            })}
          </ul>
        )}
        {data && chats.length < data.total && (
          <button onClick={loadMore} className="w-full border-t border-slate-100 py-3 text-sm text-blue-600 hover:bg-slate-50">
            Yana yuklash ({data.total - chats.length} ta qoldi)
          </button>
        )}
      </section>
    </div>
  );
}

function KindTab({ active, onClick, label }: { active: boolean; onClick: () => void; label: ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={cx(
        "flex items-center gap-1.5 whitespace-nowrap rounded-lg px-3 py-1.5 text-sm font-medium",
        active ? "bg-slate-900 text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200",
      )}
    >
      {label}
    </button>
  );
}

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={cx("flex items-center gap-2 rounded-full py-0.5 pl-0.5 pr-2.5 text-xs font-medium transition", checked ? "bg-emerald-100 text-emerald-800" : "bg-slate-200 text-slate-600")}
      title={checked ? "Saytda ko'rinadi" : "Yashirin"}
    >
      <span className={cx("h-5 w-5 rounded-full shadow-sm transition", checked ? "bg-emerald-600" : "bg-white")} />
      {checked ? "Ko'rinadi" : "Yashirin"}
    </button>
  );
}

function FoldersCard() {
  const { folders, reloadFolders, toast } = useApp();
  const [name, setName] = useState("");

  async function add() {
    if (!name.trim()) return;
    try {
      await api.post("/api/folders", { name, color: FOLDER_COLORS[folders.length % FOLDER_COLORS.length] });
      setName("");
      reloadFolders();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function save(id: number, body: { name: string; color: string }) {
    try {
      await api.patch(`/api/folders/${id}`, body);
      reloadFolders();
    } catch (e) {
      toast((e as Error).message, "error");
    }
  }

  async function remove(id: number, fname: string) {
    if (!window.confirm(`«${fname}» papkasi o'chirilsinmi? Ichidagi chatlar o'chmaydi, papkasiz qoladi.`)) return;
    await api.del(`/api/folders/${id}`);
    reloadFolders();
  }

  return (
    <section className="rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
      <h2 className="font-semibold text-slate-900">Papkalar</h2>
      <p className="mb-3 text-sm text-slate-500">Chatlarni guruhlash uchun: masalan «Mijozlar», «Ta'minotchilar», «Ustalar».</p>
      <div className="flex flex-wrap gap-2">
        {folders.map((f) => (
          <div key={f.id} className="flex items-center gap-1 rounded-lg border border-slate-200 py-1 pl-1.5 pr-1">
            <input
              type="color"
              value={f.color}
              onChange={(e) => save(f.id, { name: f.name, color: e.target.value })}
              className="h-6 w-6 cursor-pointer rounded border-0 p-0"
              title="Rang"
            />
            <FolderIcon size={14} style={{ color: f.color }} />
            <input
              defaultValue={f.name}
              onBlur={(e) => e.target.value.trim() && e.target.value !== f.name && save(f.id, { name: e.target.value, color: f.color })}
              className="w-28 rounded px-1 text-sm outline-none focus:bg-slate-50"
            />
            <span className="text-xs text-slate-400">{f.chats ?? 0}</span>
            <button onClick={() => remove(f.id, f.name)} className="rounded p-1 text-slate-300 hover:text-red-600" title="O'chirish">
              <Trash2 size={13} />
            </button>
          </div>
        ))}
        <div className="flex items-center gap-1">
          <Input value={name} onChange={(e) => setName(e.target.value)} onKeyDown={(e) => e.key === "Enter" && add()} placeholder="Yangi papka" className="h-9 w-40" />
          <Button size="sm" onClick={add} disabled={!name.trim()} className="h-9">
            <Plus size={14} /> Qo'shish
          </Button>
        </div>
      </div>
    </section>
  );
}
