import { Bot, Megaphone, User, Users } from "lucide-react";
import type { ChatKind } from "../lib/types";

export const KIND_LABELS: Record<ChatKind, string> = {
  private: "Shaxsiy",
  group: "Guruhlar",
  channel: "Kanallar",
  bot: "Botlar",
};

export const KIND_SINGULAR: Record<ChatKind, string> = {
  private: "Shaxsiy chat",
  group: "Guruh",
  channel: "Kanal",
  bot: "Bot",
};

export const KINDS: ChatKind[] = ["private", "group", "channel", "bot"];

export function KindIcon({ kind, size = 14, className }: { kind: ChatKind; size?: number; className?: string }) {
  const Icon = kind === "group" ? Users : kind === "channel" ? Megaphone : kind === "bot" ? Bot : User;
  return <Icon size={size} className={className} aria-label={KIND_SINGULAR[kind]} />;
}
