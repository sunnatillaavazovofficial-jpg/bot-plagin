"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function AdminLoginForm() {
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const router = useRouter();
  return (
    <form
      className="space-y-3"
      onSubmit={async (e) => {
        e.preventDefault();
        setBusy(true);
        setError("");
        const res = await fetch("/api/auth/admin", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ password }) });
        const json = await res.json().catch(() => ({}));
        setBusy(false);
        if (!res.ok || !json.ok) return setError(json.error || "Xatolik");
        router.push("/admin");
        router.refresh();
      }}
    >
      <input type="password" className="input" placeholder="Admin paroli" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
      {error && <p className="text-sm text-rose-300">{error}</p>}
      <button className="btn-primary w-full py-3" disabled={busy}>
        {busy ? "Tekshirilmoqda…" : "Kirish"}
      </button>
    </form>
  );
}
