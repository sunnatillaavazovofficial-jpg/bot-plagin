import { createHash } from "node:crypto";

export function hashOtp(phone: string, code: string): string {
  return createHash("sha256")
    .update(`${phone}:${code}:${process.env.SESSION_SECRET || "dev"}`)
    .digest("hex");
}
