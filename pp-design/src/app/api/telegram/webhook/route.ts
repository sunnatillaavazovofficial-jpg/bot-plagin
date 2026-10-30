import { webhookCallback } from "grammy";
import { createBot } from "@/lib/bot";
import { jsonError } from "@/lib/http";

let handler: ((request: Request) => Promise<Response>) | null = null;

/** Telegram shu manzilga yangilanishlarni yuboradi: npm run webhook:set */
export async function POST(request: Request) {
  if (!handler) {
    const bot = createBot();
    if (!bot) return jsonError("Bot sozlanmagan", 503);
    handler = webhookCallback(bot, "std/http", {
      secretToken: process.env.TELEGRAM_WEBHOOK_SECRET || undefined,
    });
  }
  return handler(request);
}
