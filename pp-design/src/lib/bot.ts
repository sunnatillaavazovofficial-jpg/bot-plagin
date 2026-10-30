import { Bot, InlineKeyboard } from "grammy";
import { db } from "./db";
import { botToken, isAdminTelegramId, siteUrl } from "./env";
import { setDesignerStatus } from "./designers";
import { designerKeyboard, newDesignerText } from "./notify";
import { escapeHtml } from "./utils";

const STATUS_LABEL = {
  APPROVED: "✅ Tasdiqlandi",
  REJECTED: "❌ Rad etildi",
  BLOCKED: "⛔ Bloklandi",
  PENDING: "⏳ Kutilmoqda",
} as const;

/** Bot buyruqlari va tugmalari. Webhook (production) ham, polling (lokal) ham shu botni ishlatadi. */
export function createBot(): Bot | null {
  const token = botToken();
  if (!token) return null;
  const bot = new Bot(token);

  bot.command(["start", "id"], async (ctx) => {
    const id = ctx.from?.id;
    const admin = isAdminTelegramId(id);
    const keyboard = new InlineKeyboard().url("🌐 Saytni ochish", siteUrl());
    await ctx.reply(
      [
        "👋 <b>PP Design</b> botiga xush kelibsiz!",
        "",
        admin
          ? "Siz adminsiz: yangi dizaynerlar so'rovlari va otzivlar shu yerga keladi."
          : "Dizayner bo'lsangiz, saytda «Dizayner bo'lib kirish» tugmasini bosing. Profilingiz tasdiqlangach va mijozlar otziv qoldirganda shu yerga xabar keladi.",
        "",
        `Sizning Telegram ID: <code>${id}</code>`,
      ].join("\n"),
      { parse_mode: "HTML", reply_markup: keyboard },
    );
  });

  bot.command("pending", async (ctx) => {
    if (!isAdminTelegramId(ctx.from?.id)) return;
    const pending = await db.designer.findMany({
      where: { status: "PENDING" },
      include: { user: { select: { telegramId: true, telegramUsername: true } } },
      orderBy: { createdAt: "asc" },
      take: 20,
    });
    if (!pending.length) {
      await ctx.reply("Kutilayotgan so'rovlar yo'q ✨");
      return;
    }
    for (const designer of pending) {
      await ctx.reply(newDesignerText(designer, designer.user), {
        parse_mode: "HTML",
        reply_markup: designerKeyboard(designer.id),
        link_preview_options: { is_disabled: true },
      });
    }
  });

  bot.callbackQuery(/^dz:(approve|reject|block):(.+)$/, async (ctx) => {
    if (!isAdminTelegramId(ctx.from.id)) {
      await ctx.answerCallbackQuery({ text: "Faqat admin uchun", show_alert: true });
      return;
    }
    const [, action, designerId] = ctx.match;
    const status = action === "approve" ? "APPROVED" : action === "reject" ? "REJECTED" : "BLOCKED";
    const result = await setDesignerStatus(designerId, status);
    if (!result) {
      await ctx.answerCallbackQuery({ text: "Dizayner topilmadi", show_alert: true });
      return;
    }

    const who = ctx.from.username ? `@${ctx.from.username}` : ctx.from.first_name;
    const original = ctx.callbackQuery.message && "text" in ctx.callbackQuery.message ? ctx.callbackQuery.message.text : "";
    await ctx.editMessageText(
      `${escapeHtml(original ?? "")}\n\n<b>${STATUS_LABEL[status]}</b> — ${escapeHtml(who)}`,
      {
        parse_mode: "HTML",
        link_preview_options: { is_disabled: true },
        reply_markup:
          status === "APPROVED"
            ? new InlineKeyboard().text("⛔ Bloklash", `dz:block:${designerId}`)
            : new InlineKeyboard().text("✅ Baribir tasdiqlash", `dz:approve:${designerId}`),
      },
    );
    await ctx.answerCallbackQuery({ text: result.changed ? STATUS_LABEL[status] : "Holat o'zgarmadi" });
  });

  bot.catch((err) => console.error("Bot xatosi:", err.error));
  return bot;
}
