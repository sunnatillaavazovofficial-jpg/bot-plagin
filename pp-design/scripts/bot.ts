/**
 * Botni polling rejimida ishga tushirish (lokal sinov yoki webhook ishlatmasangiz).
 *   npm run bot
 * Productionda webhook tavsiya etiladi: npm run webhook:set
 */
import { existsSync } from "node:fs";

if (existsSync(".env")) process.loadEnvFile(".env");

async function main() {
  const { createBot } = await import("../src/lib/bot");
  const bot = createBot();
  if (!bot) {
    console.error("TELEGRAM_BOT_TOKEN .env faylda ko'rsatilmagan");
    process.exit(1);
  }
  await bot.api.deleteWebhook();
  console.log("🤖 Bot polling rejimida ishga tushdi. To'xtatish: Ctrl+C");
  await bot.start({ allowed_updates: ["message", "callback_query"] });
}

main();
