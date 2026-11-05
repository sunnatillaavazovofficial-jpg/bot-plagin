"""Bot orqali kassaga tezkor yozish: «+500000 Aziz avans», «-80 ming bo'yoq karta», «+1.5mln #12».

Faqat botga bog'langan admin va menejerlar uchun. Buyruqlar: /balans /bugun /oy /qarzlar /yordam.
"""

from __future__ import annotations

import html
import re
from dataclasses import dataclass
from datetime import timedelta

from sqlalchemy import select

from ..db import SessionLocal
from ..finance import (
    METHOD_LABELS,
    FinanceError,
    balances,
    create_transaction,
    debt_paid,
    delete_transaction,
    fmt_sum,
    period_totals,
    validate,
)
from ..models import Category, Debt, Order, OrderEvent, Transaction, User
from ..timeutil import local_today
from ..ws_hub import hub

HELP_TEXT = (
    "<b>Kassaga yozish</b>\n"
    "<code>+500000 Aziz avans</code> — kirim\n"
    "<code>-80 ming bo'yoq</code> — chiqim\n"
    "<code>+1.5mln karta #12</code> — 12-zakazga to'lov, karta orqali\n\n"
    "Summa: <code>500000</code>, <code>500 000</code>, <code>80k</code>, <code>80 ming</code>, <code>1.5mln</code>.\n"
    "So'zlar: <i>karta, plastik, click, payme</i> → karta; aks holda naqd. "
    "Toifa nomi (material, ijara, oylik...) yozilsa o'sha toifaga tushadi.\n\n"
    "<b>Buyruqlar</b>\n"
    "/balans — kassa qoldig'i\n"
    "/bugun — bugungi kirim-chiqim\n"
    "/oy — shu oy natijasi\n"
    "/qarzlar — ochiq qarzlar\n"
    "/yordam — shu yordam"
)

CARD_WORDS = ("karta", "kartadan", "kartaga", "plastik", "click", "payme", "uzcard", "humo", "terminal", "perevod")
BANK_WORDS = ("o'tkazma", "otkazma", "bank", "hisob raqam", "perechisleniye")
CASH_WORDS = ("naqd", "naqt", "nalichka")

# Toifa nomi matnda bo'lmasa, mashhur so'zlar bo'yicha topish
CATEGORY_HINTS = {
    "Material": ("material", "akril", "pvx", "plyonka", "led", "profil", "bo'yoq", "boyoq", "kley", "list"),
    "Ijara": ("ijara", "arenda"),
    "Oylik": ("oylik", "maosh", "zarplata"),
    "Transport": ("transport", "taksi", "benzin", "damas", "yetkazib", "dostavka", "yo'lkira"),
    "Reklama": ("reklama", "target", "instagram"),
    "Kommunal": ("kommunal", "svet", "elektr", "gaz", "suv", "internet"),
    "Avans": ("avans", "predoplata", "oldindan"),
}

# Summa: 500000 | 500 000 | 500.000 | 1.5 (faqat «mln/ming» bilan) ; keyin ixtiyoriy mln/ming/k
NUMBER_RE = re.compile(
    r"^\s*(?P<sign>[+\-−–])\s*"
    r"(?P<num>\d{1,3}(?:[ .,']\d{3})+|\d+(?:[.,]\d+)?)"
    r"\s*(?P<suffix>mln|million|млн|m|ming|минг|тыс|k|к)?"
    r"(?=$|\s|[^\w'])",
    re.IGNORECASE,
)


@dataclass
class ParsedEntry:
    kind: str
    amount: int
    method: str
    note: str
    order_id: int | None = None
    category_name: str | None = None


def _to_amount(num: str, suffix: str | None) -> int:
    raw = num.strip()
    if suffix:
        mult = 1_000_000 if suffix.lower() in ("mln", "million", "млн", "m") else 1_000
        value = float(re.sub(r"[ ']", "", raw).replace(",", "."))
        return int(round(value * mult))
    if re.fullmatch(r"\d+", raw):
        return int(raw)
    if re.fullmatch(r"\d{1,3}(?:[ .,']\d{3})+", raw):
        return int(re.sub(r"\D", "", raw))
    # 1.5 kabi kasr son «mln/ming» siz noaniq
    raise ValueError(raw)


def parse_entry(text: str, category_names: list[str] | None = None) -> ParsedEntry | None:
    """Matndan kassa yozuvini ajratib oladi. Tushunarsiz bo'lsa — None."""
    match = NUMBER_RE.match(text or "")
    if not match:
        return None
    try:
        amount = _to_amount(match.group("num"), match.group("suffix"))
    except ValueError:
        return None
    if amount <= 0 or amount > 10**11:  # telefon raqam (+998...) yoki xato
        return None
    kind = "kirim" if match.group("sign") == "+" else "chiqim"
    note = text[match.end():].strip()
    order_id = None
    order_match = re.search(r"#\s*(\d+)", note)
    if order_match:
        order_id = int(order_match.group(1))
        note = (note[: order_match.start()] + note[order_match.end():]).strip()
    lower = note.lower()
    words = set(re.findall(r"[\w']+", lower))
    if words & set(CARD_WORDS):
        method = "karta"
    elif any(w in lower for w in BANK_WORDS):
        method = "o'tkazma"
    else:
        method = "naqd"
    category = None
    for name in category_names or []:
        if name.lower() in lower:
            category = name
            break
    if category is None:
        for name, hints in CATEGORY_HINTS.items():
            if words & set(hints) and (category_names is None or name in category_names):
                category = name
                break
    note = re.sub(r"\s{2,}", " ", note).strip(" ,.-")
    return ParsedEntry(kind=kind, amount=amount, method=method, note=note, order_id=order_id, category_name=category)


def esc(text: str | None) -> str:
    return html.escape(text or "")


async def _tx_text(session, tx: Transaction) -> str:
    cat = await session.get(Category, tx.category_id) if tx.category_id else None
    sign = "➕ Kirim" if tx.kind == "kirim" else "➖ Chiqim"
    lines = [
        f"{sign}: <b>{fmt_sum(tx.amount)}</b>",
        f"Toifa: {esc(cat.name if cat else 'Toifasiz')}",
        f"Usul: {METHOD_LABELS.get(tx.method, tx.method)}",
    ]
    if tx.order_id:
        lines.append(f"Zakaz: #{tx.order_id:04d}")
    if tx.note:
        lines.append(f"Izoh: {esc(tx.note)}")
    kassa = await balances(session)
    lines.append(f"\nKassa: {fmt_sum(kassa['jami'])} (naqd {fmt_sum(kassa['naqd'])}, karta {fmt_sum(kassa['karta'])})")
    return "\n".join(lines)


def _tx_keyboard(tx: Transaction) -> dict:
    other = "💵 Naqd qilish" if tx.method != "naqd" else "💳 Karta qilish"
    return {
        "inline_keyboard": [
            [{"text": "📂 Toifa", "callback_data": f"tc:{tx.id}"}, {"text": other, "callback_data": f"tm:{tx.id}"}],
            [{"text": "❌ Bekor qilish", "callback_data": f"td:{tx.id}"}],
        ]
    }


class FinanceBot:
    """notify_bot ichidan chaqiriladi; javoblarni bot orqali yuboradi."""

    def __init__(self, bot) -> None:
        self.bot = bot

    async def _user(self, session, tg_id: int) -> User | None:
        return await session.scalar(select(User).where(User.tg_user_id == tg_id, User.is_active.is_(True)))

    async def handle_text(self, chat_id: int, tg_id: int, text: str) -> bool:
        """True — xabar shu yerda qayta ishlandi."""
        command = text.split()[0].split("@")[0].lower() if text.startswith("/") else ""
        is_entry = bool(NUMBER_RE.match(text))
        if not command and not is_entry:
            return False
        async with SessionLocal() as s:
            user = await self._user(s, tg_id)
            if user is None:
                await self.bot.send(chat_id, "Avval saytda <b>Sozlamalar → Profil → Botni ulash</b> tugmasini bosing.")
                return True
            if command in ("/yordam", "/help"):
                await self.bot.send(chat_id, HELP_TEXT)
                return True
            if user.role not in ("admin", "menejer"):
                await self.bot.send(chat_id, "Kassa faqat admin va menejerlar uchun. Sizga eslatmalar kelib turadi.")
                return True
            if command == "/balans":
                await self.bot.send(chat_id, await self._balance_text(s))
            elif command == "/bugun":
                await self.bot.send(chat_id, await self._day_text(s))
            elif command == "/oy":
                await self.bot.send(chat_id, await self._month_text(s))
            elif command == "/qarzlar":
                await self.bot.send(chat_id, await self._debts_text(s))
            elif is_entry:
                await self._create(s, chat_id, user, text)
            else:
                return False
        return True

    async def _create(self, s, chat_id: int, user: User, text: str) -> None:
        cats = (await s.scalars(select(Category).where(Category.system_key.is_(None)))).all()
        parsed = parse_entry(text, [c.name for c in cats])
        if parsed is None:
            await self.bot.send(chat_id, "Summani tushunmadim.\n\n" + HELP_TEXT)
            return
        category_id = next(
            (c.id for c in cats if c.name == parsed.category_name and c.kind == parsed.kind), None
        )
        if parsed.order_id is not None and await s.get(Order, parsed.order_id) is None:
            await self.bot.send(chat_id, f"#{parsed.order_id:04d} raqamli zakaz topilmadi.")
            return
        try:
            tx = await create_transaction(
                s,
                kind=parsed.kind,
                amount=parsed.amount,
                method=parsed.method,
                category_id=category_id if parsed.order_id is None else None,
                note=parsed.note or None,
                order_id=parsed.order_id,
                user_id=user.id,
                source="bot",
            )
        except FinanceError as exc:
            await self.bot.send(chat_id, f"Xato: {esc(str(exc))}")
            return
        if tx.order_id:
            label = "To'lov" if tx.kind == "kirim" else "Qaytarildi"
            s.add(OrderEvent(order_id=tx.order_id, user_id=user.id, kind="payment",
                             text=f"{label} (bot): {fmt_sum(tx.amount)} ({tx.method})"))
        await s.commit()
        await hub.publish({"type": "finance.update"})
        if tx.order_id:
            await hub.publish({"type": "order.update", "order_id": tx.order_id})
        await self.bot.send(chat_id, "✅ Yozildi\n" + await _tx_text(s, tx), reply_markup=_tx_keyboard(tx))

    async def handle_callback(self, callback: dict) -> None:
        data = callback.get("data") or ""
        tg_id = (callback.get("from") or {}).get("id")
        message = callback.get("message") or {}
        chat_id = (message.get("chat") or {}).get("id")
        message_id = message.get("message_id")
        parts = data.split(":")
        if len(parts) < 2 or not parts[1].isdigit():
            await self.bot.answer(callback["id"])
            return
        tx_id = int(parts[1])
        async with SessionLocal() as s:
            user = await self._user(s, tg_id) if tg_id else None
            tx = await s.get(Transaction, tx_id)
            if user is None or user.role not in ("admin", "menejer"):
                await self.bot.answer(callback["id"], "Ruxsat yo'q")
                return
            if tx is None:
                await self.bot.answer(callback["id"], "Yozuv topilmadi (o'chirilgan)")
                return
            if user.role != "admin" and tx.created_by_id != user.id:
                await self.bot.answer(callback["id"], "Faqat o'zingiz kiritgan yozuvni o'zgartira olasiz")
                return
            action = parts[0]
            if action == "td":
                await delete_transaction(s, tx)
                await s.commit()
                await hub.publish({"type": "finance.update"})
                await self.bot.edit(chat_id, message_id, "❌ Bekor qilindi")
                await self.bot.answer(callback["id"], "O'chirildi")
                return
            if action == "tc":
                cats = (
                    await s.scalars(
                        select(Category)
                        .where(Category.kind == tx.kind, Category.system_key.is_(None))
                        .order_by(Category.position)
                    )
                ).all()
                rows, row = [], []
                for cat in cats:
                    row.append({"text": cat.name, "callback_data": f"ts:{tx.id}:{cat.id}"})
                    if len(row) == 2:
                        rows.append(row)
                        row = []
                if row:
                    rows.append(row)
                await self.bot.edit(chat_id, message_id, "Toifani tanlang:\n" + await _tx_text(s, tx), {"inline_keyboard": rows})
                await self.bot.answer(callback["id"])
                return
            if action == "ts" and len(parts) == 3 and parts[2].isdigit():
                tx.category_id = int(parts[2])
            elif action == "tm":
                tx.method = "karta" if tx.method == "naqd" else "naqd"
            try:
                await validate(s, tx)
            except FinanceError as exc:
                await s.rollback()
                await self.bot.answer(callback["id"], str(exc))
                return
            await s.commit()
            await hub.publish({"type": "finance.update"})
            await self.bot.edit(chat_id, message_id, "✅ Yozildi\n" + await _tx_text(s, tx), _tx_keyboard(tx))
            await self.bot.answer(callback["id"], "Saqlandi")

    async def _balance_text(self, s) -> str:
        kassa = await balances(s)
        lines = [f"💰 <b>Kassa qoldig'i: {fmt_sum(kassa['jami'])}</b>"]
        for key in ("naqd", "karta", "o'tkazma", "boshqa"):
            if key in ("naqd", "karta") or kassa.get(key):
                lines.append(f"{METHOD_LABELS[key]}: {fmt_sum(kassa.get(key, 0))}")
        return "\n".join(lines)

    async def _day_text(self, s) -> str:
        today = local_today()
        totals = await period_totals(s, today, today)
        txs = (
            await s.scalars(
                select(Transaction).where(Transaction.date == today, Transaction.kind.in_(("kirim", "chiqim")))
                .order_by(Transaction.id)
            )
        ).all()
        lines = [
            f"📅 <b>Bugun ({today.strftime('%d.%m.%Y')})</b>",
            f"Kirim: {fmt_sum(totals['kirim'])}",
            f"Chiqim: {fmt_sum(totals['chiqim'])}",
            f"Farq: {fmt_sum(totals['foyda'])}",
        ]
        if txs:
            lines.append("")
            for t in txs[-15:]:
                sign = "+" if t.kind == "kirim" else "−"
                lines.append(f"{sign}{fmt_sum(t.amount)} {esc(t.note or '')}".strip())
        return "\n".join(lines)

    async def _month_text(self, s) -> str:
        today = local_today()
        start = today.replace(day=1)
        totals = await period_totals(s, start, today)
        prev_end = start - timedelta(days=1)
        prev = await period_totals(s, prev_end.replace(day=1), prev_end)
        return (
            f"🗓 <b>{start.strftime('%m.%Y')} oyi</b>\n"
            f"Kirim: {fmt_sum(totals['kirim'])}\n"
            f"Chiqim: {fmt_sum(totals['chiqim'])}\n"
            f"Foyda: <b>{fmt_sum(totals['foyda'])}</b>\n\n"
            f"O'tgan oy foydasi: {fmt_sum(prev['foyda'])}"
        )

    async def _debts_text(self, s) -> str:
        debts = (await s.scalars(select(Debt).where(Debt.closed_at.is_(None)).order_by(Debt.due_date))).all()
        if not debts:
            return "Ochiq qarz yo'q ✅"
        to_us, by_us = [], []
        for d in debts:
            left = d.amount - await debt_paid(s, d)
            due = f" (muddat {d.due_date.strftime('%d.%m')})" if d.due_date else ""
            line = f"• {esc(d.person)}: {fmt_sum(left)}{due}"
            (to_us if d.direction == "bizga" else by_us).append(line)
        parts = []
        if to_us:
            parts.append("<b>Bizdan qarzdorlar</b>\n" + "\n".join(to_us))
        if by_us:
            parts.append("<b>Biz qarzdormiz</b>\n" + "\n".join(by_us))
        return "\n\n".join(parts)
