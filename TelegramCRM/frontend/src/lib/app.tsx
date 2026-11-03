import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { api } from "./api";
import { useLive } from "./live";
import type { Account, Me, SettingsResp, Status, UserItem } from "./types";

interface Toast {
  id: number;
  text: string;
  kind: "ok" | "error";
}

interface AppState {
  me: Me;
  accounts: Account[];
  users: UserItem[];
  statuses: Status[];
  settings: SettingsResp | null;
  counts: { unread: number; waiting: number };
  accountById: (id: number | null | undefined) => Account | undefined;
  userName: (id: number | null | undefined) => string;
  statusById: (id: number) => Status | undefined;
  reloadAccounts: () => Promise<void>;
  reloadUsers: () => Promise<void>;
  reloadStatuses: () => Promise<void>;
  reloadSettings: () => Promise<void>;
  reloadMe: () => Promise<void>;
  reloadCounts: () => void;
  toast: (text: string, kind?: "ok" | "error") => void;
  isAdmin: boolean;
  isManager: boolean;
}

const Ctx = createContext<AppState | null>(null);

export function useApp(): AppState {
  const v = useContext(Ctx);
  if (!v) throw new Error("AppProvider yo'q");
  return v;
}

export function AppProvider({ initialMe, children }: { initialMe: Me; children: ReactNode }) {
  const [me, setMe] = useState(initialMe);
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [users, setUsers] = useState<UserItem[]>([]);
  const [statuses, setStatuses] = useState<Status[]>([]);
  const [settings, setSettings] = useState<SettingsResp | null>(null);
  const [counts, setCounts] = useState({ unread: 0, waiting: 0 });
  const [toasts, setToasts] = useState<Toast[]>([]);

  const toast = useCallback((text: string, kind: "ok" | "error" = "ok") => {
    const id = Date.now() + Math.random();
    setToasts((t) => [...t, { id, text, kind }]);
    window.setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), kind === "error" ? 6000 : 3000);
  }, []);

  const reloadAccounts = useCallback(async () => {
    const r = await api.get<{ accounts: Account[] }>("/api/accounts");
    setAccounts(r.accounts);
  }, []);
  const reloadUsers = useCallback(async () => setUsers(await api.get<UserItem[]>("/api/users")), []);
  const reloadStatuses = useCallback(async () => setStatuses(await api.get<Status[]>("/api/statuses")), []);
  const reloadSettings = useCallback(async () => setSettings(await api.get<SettingsResp>("/api/settings")), []);
  const reloadMe = useCallback(async () => setMe(await api.get<Me>("/api/auth/me")), []);
  const reloadCounts = useCallback(() => {
    api
      .get<{ unread: number; waiting: number }>("/api/chats/counts")
      .then(setCounts)
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    reloadAccounts().catch(() => undefined);
    reloadUsers().catch(() => undefined);
    reloadStatuses().catch(() => undefined);
    reloadSettings().catch(() => undefined);
    reloadCounts();
    const t = window.setInterval(reloadCounts, 60000);
    return () => window.clearInterval(t);
  }, [reloadAccounts, reloadUsers, reloadStatuses, reloadSettings, reloadCounts]);

  useLive((e) => {
    if (e.type === "account.update") {
      setAccounts((list) => {
        const exists = list.some((a) => a.id === e.account.id);
        return exists
          ? list.map((a) => (a.id === e.account.id ? { ...a, ...e.account } : a))
          : [...list, e.account];
      });
    } else if (e.type === "account.deleted") {
      setAccounts((list) => list.filter((a) => a.id !== e.account_id));
    } else if (e.type === "statuses.update") {
      reloadStatuses().catch(() => undefined);
    }
    if (e.type === "message.new" || e.type === "chat.update" || e.type === "chats.reload") {
      reloadCountsDebounced();
    }
  });

  const reloadCountsDebounced = useMemo(() => {
    let t: number | undefined;
    return () => {
      window.clearTimeout(t);
      t = window.setTimeout(reloadCounts, 800);
    };
  }, [reloadCounts]);

  const value = useMemo<AppState>(() => {
    const accMap = new Map(accounts.map((a) => [a.id, a]));
    const userMap = new Map(users.map((u) => [u.id, u]));
    const statusMap = new Map(statuses.map((s) => [s.id, s]));
    return {
      me,
      accounts,
      users,
      statuses,
      settings,
      counts,
      accountById: (id) => (id ? accMap.get(id) : undefined),
      userName: (id) => (id ? (userMap.get(id)?.name ?? "—") : "—"),
      statusById: (id) => statusMap.get(id),
      reloadAccounts,
      reloadUsers,
      reloadStatuses,
      reloadSettings,
      reloadMe,
      reloadCounts,
      toast,
      isAdmin: me.role === "admin",
      isManager: me.role === "admin" || me.role === "menejer",
    };
  }, [me, accounts, users, statuses, settings, counts, reloadAccounts, reloadUsers, reloadStatuses, reloadSettings, reloadMe, reloadCounts, toast]);

  return (
    <Ctx.Provider value={value}>
      {children}
      <div className="pointer-events-none fixed inset-x-0 bottom-20 z-[100] flex flex-col items-center gap-2 px-4 md:bottom-6">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`pointer-events-auto max-w-md rounded-xl px-4 py-2.5 text-sm font-medium text-white shadow-lg ${
              t.kind === "error" ? "bg-red-600" : "bg-slate-900"
            }`}
          >
            {t.text}
          </div>
        ))}
      </div>
    </Ctx.Provider>
  );
}
