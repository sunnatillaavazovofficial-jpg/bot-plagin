"use client";

import { useState } from "react";

/** Ko'nikmalar kabi teglar ro'yxati: yozib Enter yoki vergul bosiladi */
export function TagInput({ value, onChange, placeholder, max = 15 }: { value: string[]; onChange: (v: string[]) => void; placeholder?: string; max?: number }) {
  const [draft, setDraft] = useState("");
  const add = () => {
    const tag = draft.trim().replace(/,$/, "");
    if (tag && !value.includes(tag) && value.length < max) onChange([...value, tag.slice(0, 30)]);
    setDraft("");
  };
  return (
    <div className="input flex flex-wrap items-center gap-2 !py-2">
      {value.map((tag) => (
        <span key={tag} className="chip gap-1.5">
          {tag}
          <button type="button" className="text-muted hover:text-white" onClick={() => onChange(value.filter((t) => t !== tag))} aria-label={`${tag} ni o'chirish`}>
            ✕
          </button>
        </span>
      ))}
      <input
        className="min-w-[140px] flex-1 bg-transparent py-1 outline-none"
        value={draft}
        placeholder={value.length ? "" : placeholder}
        onChange={(e) => {
          const v = e.target.value;
          if (!v.includes(",")) return setDraft(v);
          const next = [...value];
          for (const part of v.split(",").map((s) => s.trim()).filter(Boolean)) {
            if (!next.includes(part) && next.length < max) next.push(part.slice(0, 30));
          }
          onChange(next);
          setDraft("");
        }}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            e.preventDefault();
            add();
          }
          if (e.key === "Backspace" && !draft && value.length) onChange(value.slice(0, -1));
        }}
        onBlur={add}
      />
    </div>
  );
}
