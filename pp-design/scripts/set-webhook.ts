/**
 * Telegram webhook'ni saytga ulash (serverda, HTTPS sozlangandan keyin bir marta):
 *   npm run webhook:set
 */
import { existsSync } from "node:fs";
import { Api } from "grammy";

if (existsSync(".env")) process.loadEnvFile(".env");

async function main() {
  const token = process.env.TELEGRAM_BOT_TOKEN;
  const site = (process.env.SITE_URL || "").replace(/\/+$/, "");
  if (!token || !site.startsWith("https://")) {
    console.error("TELEGRAM_BOT_TOKEN va https:// bilan boshlanadigan SITE_URL kerak");
    process.exit(1);
  }

  const api = new Api(token);
  const url = `${site}/api/telegram/webhook`;
  await api.setWebhook(url, {
    secret_token: process.env.TELEGRAM_WEBHOOK_SECRET || undefined,
    allowed_updates: ["message", "callback_query"],
    drop_pending_updates: true,
  });
  await api.setMyCommands([
    { command: "start", description: "Boshlash va Telegram ID ni bilish" },
    { command: "pending", description: "Kutilayotgan dizaynerlar (admin)" },
  ]);
  console.log("✅ Webhook o'rnatildi:", url);
  console.log(await api.getWebhookInfo());
}

main();
