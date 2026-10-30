"use client";

import Link from "next/link";
import { useState, useTransition } from "react";
import { submitReview } from "@/app/actions/reviews";
import { cn } from "@/lib/utils";

export function ReviewForm({
  designerId,
  username,
  viewer,
  existing,
}: {
  designerId: string;
  username: string;
  viewer: { role: "CLIENT" | "DESIGNER" | "ADMIN"; hasName: boolean } | null;
  existing: { rating: number; text: string } | null;
}) {
  const [rating, setRating] = useState(existing?.rating ?? 5);
  const [hover, setHover] = useState(0);
  const [text, setText] = useState(existing?.text ?? "");
  const [name, setName] = useState("");
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null);
  const [pending, startTransition] = useTransition();

  if (!viewer) {
    return (
      <div className="glass rounded-2xl p-6 text-center">
        <p className="font-semibold">Dizayner bilan ishlaganmisiz? Fikringizni qoldiring.</p>
        <p className="mt-1 text-sm text-mist/70">Otziv qoldirish uchun telefon raqamingiz bilan kiring.</p>
        <Link href={`/login?tab=client&next=${encodeURIComponent(`/designer/${username}#otzivlar`)}`} className="btn-primary mt-4">
          Kirish va otziv yozish
        </Link>
      </div>
    );
  }
  if (viewer.role !== "CLIENT") {
    return <p className="glass rounded-2xl p-5 text-sm text-mist/70">Otzivlarni faqat mijozlar qoldira oladi.</p>;
  }

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    startTransition(async () => {
      const res = await submitReview({ designerId, rating, text, name: viewer.hasName ? undefined : name });
      setResult(res.ok ? { ok: true, text: res.message ?? "Saqlandi" } : { ok: false, text: res.error });
    });
  };

  return (
    <form onSubmit={submit} className="glass space-y-4 rounded-2xl p-5 sm:p-6">
      <p className="font-display text-lg font-semibold">{existing ? "Otzivingizni tahrirlash" : "Otziv qoldirish"}</p>
      <div className="flex items-center gap-1" onMouseLeave={() => setHover(0)} role="radiogroup" aria-label="Baho">
        {[1, 2, 3, 4, 5].map((i) => (
          <button
            key={i}
            type="button"
            role="radio"
            aria-checked={rating === i}
            aria-label={`${i} yulduz`}
            onMouseEnter={() => setHover(i)}
            onClick={() => setRating(i)}
            className="p-0.5 transition hover:scale-110"
          >
            <svg viewBox="0 0 20 20" className={cn("h-8 w-8", (hover || rating) >= i ? "text-amber-300" : "text-white/15")} fill="currentColor" aria-hidden>
              <path d="M10 1.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L10 14.9l-5.2 2.7 1-5.8L1.5 7.7l5.9-.9L10 1.5z" />
            </svg>
          </button>
        ))}
        <span className="ml-2 text-sm text-mist/70">{rating}/5</span>
      </div>
      {!viewer.hasName && (
        <div>
          <label className="label" htmlFor="review-name">
            Ismingiz
          </label>
          <input id="review-name" className="input" value={name} onChange={(e) => setName(e.target.value)} required maxLength={60} />
        </div>
      )}
      <div>
        <label className="label" htmlFor="review-text">
          Fikringiz
        </label>
        <textarea
          id="review-text"
          className="input min-h-28 resize-y"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Dizayner bilan ishlash qanday bo'ldi?"
          maxLength={1500}
          required
        />
      </div>
      {result && <p className={cn("text-sm", result.ok ? "text-emerald-300" : "text-rose-300")}>{result.text}</p>}
      <button className="btn-primary" disabled={pending || text.trim().length < 5}>
        {pending ? "Yuborilmoqda…" : existing ? "Yangilash" : "Yuborish"}
      </button>
    </form>
  );
}
