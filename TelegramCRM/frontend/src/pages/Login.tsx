import { useState, type FormEvent } from "react";
import { Button, Field, Input } from "../components/ui";
import { api } from "../lib/api";

export default function LoginPage({ onLogin, note }: { onLogin: () => void; note?: string }) {
  const [login, setLogin] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      await api.post("/api/auth/login", { login, password });
      onLogin();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-blue-900 p-4">
      <form onSubmit={submit} className="w-full max-w-sm rounded-2xl bg-white p-7 shadow-2xl">
        <div className="mb-6 flex items-center gap-3">
          <img src="/favicon.svg" alt="" className="h-11 w-11" />
          <div>
            <div className="text-lg font-semibold text-slate-900">TelegramCRM</div>
            <div className="text-sm text-slate-500">Ish tizimiga kirish</div>
          </div>
        </div>
        {note && <div className="mb-4 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">{note}</div>}
        <div className="space-y-3">
          <Field label="Login">
            <Input value={login} onChange={(e) => setLogin(e.target.value)} autoFocus autoComplete="username" autoCapitalize="none" />
          </Field>
          <Field label="Parol">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
          </Field>
        </div>
        {error && <div className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}
        <Button type="submit" className="mt-5 w-full" disabled={busy || !login || !password}>
          {busy ? "Kirilmoqda..." : "Kirish"}
        </Button>
      </form>
    </div>
  );
}
