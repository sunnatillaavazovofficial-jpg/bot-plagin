"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

/** +998 90 123 45 67 ko'rinishida yozib borish */
function formatInput(value: string) {
  let d = value.replace(/\D/g, "");
  if (d.startsWith("998")) d = d.slice(3);
  d = d.slice(0, 9);
  const parts = [d.slice(0, 2), d.slice(2, 5), d.slice(5, 7), d.slice(7, 9)].filter(Boolean);
  return parts.join(" ");
}

/** Mijoz: telefon raqam + SMS kod bilan kirish */
export function PhoneLogin({ next }: { next?: string }) {
  const router = useRouter();
  const [step, setStep] = useState<"phone" | "code">("phone");
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [info, setInfo] = useState("");
  const [busy, setBusy] = useState(false);
  const [wait, setWait] = useState(0);
  const codeRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (wait <= 0) return;
    const t = window.setTimeout(() => setWait((w) => w - 1), 1000);
    return () => window.clearTimeout(t);
  }, [wait]);

  const requestCode = async () => {
    setBusy(true);
    setError("");
    const res = await fetch("/api/auth/otp/request", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ phone: "998" + phone.replace(/\D/g, "") }),
    });
    const json = await res.json().catch(() => ({}));
    setBusy(false);
    if (!res.ok || !json.ok) {
      setError(json.error || "Xatolik yuz berdi");
      return;
    }
    setStep("code");
    setWait(json.resendIn ?? 60);
    setInfo(json.devCode ? `Sinov rejimi: kod — ${json.devCode}` : `Kod +998 ${phone} raqamiga SMS orqali yuborildi`);
    setTimeout(() => codeRef.current?.focus(), 50);
  };

  const verify = async (value = code) => {
    if (value.length !== 6) return;
    setBusy(true);
    setError("");
    const res = await fetch("/api/auth/otp/verify", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ phone: "998" + phone.replace(/\D/g, ""), code: value, name }),
    });
    const json = await res.json().catch(() => ({}));
    if (!res.ok || !json.ok) {
      setBusy(false);
      setError(json.error || "Xatolik yuz berdi");
      return;
    }
    router.push(next && next.startsWith("/") && !next.startsWith("//") ? next : "/");
    router.refresh();
  };

  if (step === "phone") {
    return (
      <form
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault();
          requestCode();
        }}
      >
        <div>
          <label className="label" htmlFor="name">
            Ismingiz
          </label>
          <input id="name" className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="Masalan: Aziz" maxLength={60} autoComplete="name" />
        </div>
        <div>
          <label className="label" htmlFor="phone">
            Telefon raqam
          </label>
          <div className="flex">
            <span className="flex items-center rounded-l-xl border border-r-0 border-white/10 bg-white/5 px-3 text-sm text-mist">+998</span>
            <input
              id="phone"
              className="input rounded-l-none"
              value={phone}
              onChange={(e) => setPhone(formatInput(e.target.value))}
              placeholder="90 123 45 67"
              inputMode="tel"
              autoComplete="tel-national"
              required
            />
          </div>
        </div>
        {error && <p className="text-sm text-rose-300">{error}</p>}
        <button className="btn-primary w-full py-3" disabled={busy || phone.replace(/\D/g, "").length !== 9}>
          {busy ? "Yuborilmoqda…" : "SMS kod olish"}
        </button>
      </form>
    );
  }

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        verify();
      }}
    >
      <p className="rounded-xl bg-brand/10 p-3 text-sm text-mist">{info}</p>
      <div>
        <label className="label" htmlFor="code">
          SMS kod
        </label>
        <input
          id="code"
          ref={codeRef}
          className="input text-center font-display text-2xl tracking-[0.5em]"
          value={code}
          onChange={(e) => {
            const v = e.target.value.replace(/\D/g, "").slice(0, 6);
            setCode(v);
            if (v.length === 6) verify(v);
          }}
          inputMode="numeric"
          autoComplete="one-time-code"
          placeholder="••••••"
        />
      </div>
      {error && <p className="text-sm text-rose-300">{error}</p>}
      <button className="btn-primary w-full py-3" disabled={busy || code.length !== 6}>
        {busy ? "Tekshirilmoqda…" : "Kirish"}
      </button>
      <div className="flex justify-between text-sm">
        <button type="button" className="text-mist/70 hover:text-white" onClick={() => setStep("phone")}>
          ← Raqamni o&apos;zgartirish
        </button>
        <button type="button" className="text-brand disabled:text-muted" disabled={wait > 0 || busy} onClick={requestCode}>
          {wait > 0 ? `Qayta yuborish (${wait})` : "Kodni qayta yuborish"}
        </button>
      </div>
    </form>
  );
}
