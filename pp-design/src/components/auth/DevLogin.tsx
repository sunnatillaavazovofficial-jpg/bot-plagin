"use client";

import { useRouter } from "next/navigation";

/** Faqat lokal sinov (DEV_LOGIN=1): Telegram'siz dizayner/admin/mijoz bo'lib kirish */
export function DevLogin() {
  const router = useRouter();
  const login = async (as: "designer" | "admin" | "client") => {
    const res = await fetch("/api/auth/dev", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ as }),
    });
    const json = await res.json();
    if (json.ok) {
      router.push(json.redirectTo);
      router.refresh();
    }
  };
  return (
    <div className="mt-6 rounded-2xl border border-dashed border-amber-300/40 p-4">
      <p className="mb-3 text-xs font-semibold tracking-wide text-amber-200 uppercase">Sinov rejimi (faqat lokal)</p>
      <div className="flex flex-wrap gap-2">
        <button className="btn-ghost !py-1.5 text-xs" onClick={() => login("designer")}>
          Yangi dizayner
        </button>
        <button className="btn-ghost !py-1.5 text-xs" onClick={() => login("admin")}>
          Admin
        </button>
        <button className="btn-ghost !py-1.5 text-xs" onClick={() => login("client")}>
          Mijoz
        </button>
      </div>
    </div>
  );
}
