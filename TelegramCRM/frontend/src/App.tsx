import { useCallback, useEffect, useState } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import Layout from "./components/Layout";
import { Spinner } from "./components/ui";
import { api, setUnauthorizedHandler } from "./lib/api";
import { AppProvider } from "./lib/app";
import { startLive, stopLive } from "./lib/live";
import { setupTelegramWebApp, telegramInitData } from "./lib/telegram";
import type { Me } from "./lib/types";
import ClientsPage from "./pages/Clients";
import FinancePage from "./pages/Finance";
import InboxPage from "./pages/Inbox";
import LoginPage from "./pages/Login";
import OrdersPage from "./pages/Orders";
import ReportsPage from "./pages/Reports";
import SettingsPage from "./pages/Settings";

export default function App() {
  const [me, setMe] = useState<Me | null | undefined>(undefined);
  const [loginNote, setLoginNote] = useState("");

  const loadMe = useCallback(async () => {
    try {
      setMe(await api.get<Me>("/api/auth/me"));
      return;
    } catch {
      /* kirilmagan */
    }
    // Telegram ichida ochilgan bo'lsa — bot orqali avtomatik kirish
    const initData = telegramInitData();
    if (initData) {
      try {
        await api.post("/api/auth/telegram", { init_data: initData });
        setMe(await api.get<Me>("/api/auth/me"));
        return;
      } catch (e) {
        setLoginNote((e as Error).message);
      }
    }
    setMe(null);
  }, []);

  useEffect(() => {
    setupTelegramWebApp();
    setUnauthorizedHandler(() => {
      stopLive();
      setMe(null);
    });
    loadMe();
  }, [loadMe]);

  useEffect(() => {
    if (me) startLive();
  }, [me]);

  if (me === undefined) {
    return (
      <div className="flex h-full items-center justify-center">
        <Spinner className="h-8 w-8" />
      </div>
    );
  }
  if (me === null) return <LoginPage onLogin={loadMe} note={loginNote} />;

  const home = me.role === "xodim" && (me.account_ids?.length ?? 0) === 0 ? "/orders" : "/chats";
  return (
    <BrowserRouter>
      <AppProvider initialMe={me}>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/" element={<Navigate to={home} replace />} />
            <Route path="/chats" element={<InboxPage />} />
            <Route path="/chats/:chatId" element={<InboxPage />} />
            <Route path="/orders" element={<OrdersPage />} />
            <Route path="/orders/:orderId" element={<OrdersPage />} />
            <Route path="/clients" element={<ClientsPage />} />
            <Route path="/clients/:clientId" element={<ClientsPage />} />
            <Route path="/finance" element={<FinancePage />} />
            <Route path="/finance/:tab" element={<FinancePage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/settings/:tab" element={<SettingsPage />} />
            <Route path="*" element={<Navigate to={home} replace />} />
          </Route>
        </Routes>
      </AppProvider>
    </BrowserRouter>
  );
}
