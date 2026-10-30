"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { resubmitApplication } from "@/app/actions/designer";

export function ResubmitButton() {
  const [pending, startTransition] = useTransition();
  const [message, setMessage] = useState("");
  const router = useRouter();
  return (
    <span className="inline-flex flex-wrap items-center gap-3">
      <button
        className="btn-primary !py-2"
        disabled={pending}
        onClick={() =>
          startTransition(async () => {
            const res = await resubmitApplication();
            setMessage(res.ok ? res.message ?? "" : res.error);
            router.refresh();
          })
        }
      >
        Qayta so&apos;rov yuborish
      </button>
      {message && <span className="text-sm">{message}</span>}
    </span>
  );
}
