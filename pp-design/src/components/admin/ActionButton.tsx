"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import type { ActionResult } from "@/app/actions/reviews";
import { cn } from "@/lib/utils";

/** Server action'ni chaqiradigan tugma (o'chirish, tasdiqlash va h.k.) */
export function ActionButton({
  action,
  children,
  variant = "ghost",
  confirmText,
  className,
  title,
}: {
  action: () => Promise<ActionResult>;
  children: React.ReactNode;
  variant?: "ghost" | "primary" | "danger";
  confirmText?: string;
  className?: string;
  title?: string;
}) {
  const [pending, startTransition] = useTransition();
  const router = useRouter();
  return (
    <button
      type="button"
      title={title}
      disabled={pending}
      className={cn(variant === "primary" ? "btn-primary" : variant === "danger" ? "btn-danger" : "btn-ghost", "!px-3 !py-1.5 text-xs", className)}
      onClick={() => {
        if (confirmText && !confirm(confirmText)) return;
        startTransition(async () => {
          const res = await action();
          if (!res.ok) alert(res.error);
          router.refresh();
        });
      }}
    >
      {pending ? "…" : children}
    </button>
  );
}
