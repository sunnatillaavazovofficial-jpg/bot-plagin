import { Api, InlineKeyboard } from "grammy";
import type { Designer, Review, User } from "@prisma/client";
import { adminTelegramIds, botToken, siteUrl } from "./env";
import { formatUzPhone } from "./phone";
import { escapeHtml } from "./utils";

/** Telegram'ga xabar yuborish (bot handlerlarisiz, faqat API). Token bo'lmasa jim o'tib ketadi. */
let api: Api | null = null;
function telegram(): Api | null {
  const token = botToken();
  if (!token) return null;
  api ??= new Api(token);
  return api;
}

async function send(chatId: bigint | number | string, html: string, keyboard?: InlineKeyboard): Promise<void> {
  const tg = telegram();
  if (!tg) {
    console.log(`[Telegram o'chirilgan] → ${chatId}:\n${html}\n`);
    return;
  }
  try {
    await tg.sendMessage(String(chatId), html, {
      parse_mode: "HTML",
      link_preview_options: { is_disabled: true },
      reply_markup: keyboard,
    });
  } catch (error) {
    // Masalan: foydalanuvchi botni hali /start qilmagan bo'lsa
    console.error(`Telegram xabar yuborilmadi (${chatId}):`, error);
  }
}

async function sendToAdmins(html: string, keyboard?: InlineKeyboard): Promise<void> {
  await Promise.all(adminTelegramIds().map((id) => send(id, html, keyboard)));
}

export function designerKeyboard(designerId: string): InlineKeyboard {
  return new InlineKeyboard()
    .text("✅ Tasdiqlash", `dz:approve:${designerId}`)
    .text("❌ Rad etish", `dz:reject:${designerId}`);
}

export function newDesignerText(designer: Designer, user: Pick<User, "telegramId" | "telegramUsername">): string {
  const lines = [
    "🆕 <b>Yangi dizayner ro'yxatdan o'tdi</b>",
    "",
    `👤 Ism: <b>${escapeHtml(designer.fullName)}</b>`,
    user.telegramUsername ? `💬 Telegram: @${escapeHtml(user.telegramUsername)}` : null,
    user.telegramId ? `🆔 ID: <code>${user.telegramId}</code>` : null,
    `🔗 Profil: ${siteUrl()}/designer/${designer.username}`,
    "",
    "Tasdiqlaysizmi? Tasdiqlanmaguncha profil saytda ko'rinmaydi.",
  ];
  return lines.filter((line) => line !== null).join("\n");
}

export async function notifyNewDesigner(designer: Designer, user: Pick<User, "telegramId" | "telegramUsername">) {
  await sendToAdmins(newDesignerText(designer, user), designerKeyboard(designer.id));
}

export async function notifyNewReview(
  review: Review,
  designer: Designer & { user: Pick<User, "telegramId"> },
  client: Pick<User, "name" | "phone">,
  isUpdate: boolean,
) {
  const stars = "⭐".repeat(review.rating) + "☆".repeat(5 - review.rating);
  const html = [
    isUpdate ? "✏️ <b>Otziv yangilandi</b>" : "💬 <b>Yangi otziv</b>",
    "",
    `🎨 Dizayner: <b>${escapeHtml(designer.fullName)}</b>`,
    `👤 Mijoz: ${escapeHtml(client.name || "Ismsiz")}`,
    client.phone ? `📞 Raqam: ${formatUzPhone(client.phone)}` : null,
    `Baho: ${stars} (${review.rating}/5)`,
    "",
    `<i>${escapeHtml(review.text)}</i>`,
    "",
    `🔗 ${siteUrl()}/designer/${designer.username}`,
  ]
    .filter((line) => line !== null)
    .join("\n");

  const tasks = [sendToAdmins(html)];
  const designerChat = designer.user.telegramId;
  if (designerChat && !adminTelegramIds().includes(designerChat)) tasks.push(send(designerChat, html));
  await Promise.all(tasks);
}

export async function notifyDesignerStatus(designer: Designer & { user: Pick<User, "telegramId"> }) {
  const chat = designer.user.telegramId;
  if (!chat) return;
  const profile = `${siteUrl()}/designer/${designer.username}`;
  const messages: Record<string, string> = {
    APPROVED: `🎉 <b>Tabriklaymiz!</b> Profilingiz admin tomonidan tasdiqlandi va saytda ko'rinadi.\n\nProfilni to'ldirish: ${siteUrl()}/dashboard\nSizning sahifangiz: ${profile}`,
    REJECTED: `😔 Afsuski, dizayner sifatida so'rovingiz rad etildi.\nSavollar bo'lsa admin bilan bog'laning. Ma'lumotlarni to'ldirib, qayta so'rov yuborishingiz mumkin: ${siteUrl()}/dashboard`,
    BLOCKED: "⛔ Profilingiz bloklandi va saytda ko'rinmaydi. Batafsil ma'lumot uchun admin bilan bog'laning.",
  };
  const html = messages[designer.status];
  if (html) await send(chat, html);
}

export async function notifyResubmission(designer: Designer, user: Pick<User, "telegramId" | "telegramUsername">) {
  await sendToAdmins(`🔁 <b>Qayta so'rov</b>\n\n${newDesignerText(designer, user)}`, designerKeyboard(designer.id));
}
