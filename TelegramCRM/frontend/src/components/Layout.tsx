import { BarChart3, ClipboardList, LogOut, MessageCircle, Settings, Users, Wallet, WifiOff } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";
import { api } from "../lib/api";
import { useApp } from "../lib/app";
import { ROLE_LABELS } from "../lib/format";
import { stopLive, useLiveConnected } from "../lib/live";
import { Avatar, cx } from "./ui";

export default function Layout() {
  const { me, counts, isAdmin, isManager, settings } = useApp();
  const live = useLiveConnected();
  const hasChats = me.role !== "xodim" || (me.account_ids?.length ?? 0) > 0;

  const items = [
    hasChats && { to: "/chats", label: "Chatlar", icon: MessageCircle, badge: counts.unread },
    { to: "/orders", label: "Zakazlar", icon: ClipboardList },
    isManager && { to: "/finance", label: "Kassa", icon: Wallet },
    isManager && { to: "/clients", label: "Mijozlar", icon: Users },
    isAdmin && { to: "/reports", label: "Hisobot", icon: BarChart3 },
    { to: "/settings", label: "Sozlamalar", icon: Settings },
  ].filter(Boolean) as { to: string; label: string; icon: typeof MessageCircle; badge?: number }[];

  async function logout() {
    await api.post("/api/auth/logout").catch(() => undefined);
    stopLive();
    location.href = "/";
  }

  return (
    <div className="flex h-full flex-col md:flex-row">
      <aside className="hidden w-60 shrink-0 flex-col border-r border-slate-200 bg-slate-900 text-slate-300 md:flex">
        <div className="flex items-center gap-2.5 px-5 py-5">
          <img src="/favicon.svg" alt="" className="h-8 w-8" />
          <div className="min-w-0">
            <div className="truncate font-semibold text-white">{settings?.values.company_name || "TelegramCRM"}</div>
            {settings?.fake_mode && <div className="text-[11px] text-amber-300">Sinov rejimi</div>}
          </div>
        </div>
        <nav className="flex-1 space-y-0.5 px-3">
          {items.map((it) => (
            <NavLink
              key={it.to}
              to={it.to}
              className={({ isActive }) =>
                cx(
                  "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition",
                  isActive ? "bg-white/10 text-white" : "hover:bg-white/5 hover:text-white",
                )
              }
            >
              <it.icon size={18} />
              <span className="flex-1">{it.label}</span>
              {!!it.badge && (
                <span className="rounded-full bg-blue-500 px-1.5 py-0.5 text-[11px] font-semibold text-white">{it.badge}</span>
              )}
            </NavLink>
          ))}
        </nav>
        {!live && (
          <div className="mx-3 mb-2 flex items-center gap-2 rounded-lg bg-amber-500/15 px-3 py-2 text-xs text-amber-200">
            <WifiOff size={14} /> Aloqa tiklanmoqda...
          </div>
        )}
        {counts.waiting > 0 && hasChats && (
          <NavLink to="/chats?filter=unanswered" className="mx-3 mb-2 rounded-lg bg-red-500/15 px-3 py-2 text-xs text-red-200">
            {counts.waiting} ta chat javob kutmoqda
          </NavLink>
        )}
        <div className="flex items-center gap-3 border-t border-white/10 px-4 py-4">
          <Avatar name={me.name} size={34} />
          <div className="min-w-0 flex-1">
            <div className="truncate text-sm font-medium text-white">{me.name}</div>
            <div className="text-xs text-slate-400">{ROLE_LABELS[me.role]}</div>
          </div>
          <button onClick={logout} className="rounded-lg p-2 text-slate-400 hover:bg-white/10 hover:text-white" title="Chiqish">
            <LogOut size={17} />
          </button>
        </div>
      </aside>

      <main className="min-h-0 min-w-0 flex-1">
        <Outlet />
      </main>

      <nav className="pb-safe flex shrink-0 border-t border-slate-200 bg-white md:hidden">
        {items.map((it) => (
          <NavLink
            key={it.to}
            to={it.to}
            className={({ isActive }) =>
              cx(
                "relative flex flex-1 flex-col items-center gap-0.5 py-2 text-[11px] font-medium",
                isActive ? "text-blue-600" : "text-slate-500",
              )
            }
          >
            <it.icon size={21} />
            {it.label}
            {!!it.badge && (
              <span className="absolute left-1/2 top-1 ml-2 rounded-full bg-blue-600 px-1.5 text-[10px] font-semibold text-white">
                {it.badge}
              </span>
            )}
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
