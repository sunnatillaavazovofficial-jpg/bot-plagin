"""Eslatma matnlari va kimga yuborilishi."""

from __future__ import annotations

import html
from datetime import date, datetime, time, timedelta

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import SessionLocal
from ..finance import balances, period_totals
from ..models import AccountAccess, Chat, Client, NotifyLog, Order, OrderStatus, TgAccount, User, utcnow
from ..settings_store import as_bool, get_all
from ..timeutil import tz
from .bot import notify_users


def fmt_sum(value: int) -> str:
    return f"{value:,}".replace(",", " ") + " so'm"


def esc(text: str | None) -> str:
    return html.escape(text or "")


async def _once(session: AsyncSession, key: str) -> bool:
    """Bir xil eslatma ikki marta ketmasligi uchun. True — hali yuborilmagan."""
    if await session.get(NotifyLog, key) is not None:
        return False
    session.add(NotifyLog(key=key))
    return True


async def managers(session: AsyncSession, account_id: int | None = None) -> list[User]:
    users = (
        await session.scalars(select(User).where(User.is_active.is_(True), User.role.in_(("admin", "menejer"))))
    ).all()
    if account_id is None:
        return list(users)
    access = set(
        (await session.scalars(select(AccountAccess.user_id).where(AccountAccess.account_id == account_id))).all()
    )
    return [u for u in users if u.role == "admin" or u.id in access]


async def order_line(session: AsyncSession, order: Order) -> str:
    client = await session.get(Client, order.client_id) if order.client_id else None
    parts = [f"<b>#{order.id:04d}</b> {esc(order.title)}"]
    if client:
        parts.append(f"👤 {esc(client.name)}")
    if order.deadline:
        parts.append(f"📅 {order.deadline.strftime('%d.%m.%Y')}")
    return " · ".join(parts)


async def order_assigned(order_id: int, by_name: str) -> None:
    async with SessionLocal() as s:
        settings = await get_all(s)
        order = await s.get(Order, order_id)
        if not as_bool(settings["notify_assign"]) or order is None or order.assignee_id is None:
            return
        assignee = await s.get(User, order.assignee_id)
        if assignee is None:
            return
        text = f"📌 Sizga yangi zakaz biriktirildi ({esc(by_name)}):\n{await order_line(s, order)}"
        await notify_users([assignee], text)


async def check_deadlines(today: date) -> int:
    """Ertaga muddati tugaydigan va muddati o'tgan zakazlar (kuniga bir marta)."""
    sent = 0
    async with SessionLocal() as s:
        settings = await get_all(s)
        if not as_bool(settings["notify_deadline"]):
            return 0
        rows = (
            await s.scalars(
                select(Order)
                .join(OrderStatus, OrderStatus.id == Order.status_id)
                .where(
                    OrderStatus.is_done.is_(False),
                    OrderStatus.is_cancel.is_(False),
                    Order.deadline.is_not(None),
                    Order.deadline <= today + timedelta(days=1),
                )
                .order_by(Order.deadline)
            )
        ).all()
        per_user: dict[int, tuple[User, list[str]]] = {}
        all_managers = await managers(s)
        for order in rows:
            kind = "overdue" if order.deadline < today else ("today" if order.deadline == today else "tomorrow")
            if not await _once(s, f"deadline:{order.id}:{today.isoformat()}"):
                continue
            label = {"overdue": "⛔️ Muddati o'tgan", "today": "⚠️ Bugun topshiriladi", "tomorrow": "⏰ Ertaga muddat"}[kind]
            line = f"{label}: {await order_line(s, order)}"
            targets: list[User] = []
            if order.assignee_id:
                assignee = await s.get(User, order.assignee_id)
                if assignee:
                    targets.append(assignee)
            if not targets or kind == "overdue":
                targets.extend(u for u in all_managers if u not in targets)
            for user in targets:
                per_user.setdefault(user.id, (user, []))[1].append(line)
        await s.commit()
        for user, lines in per_user.values():
            sent += await notify_users([user], "\n".join(lines))
    return sent


async def check_unanswered() -> int:
    """X daqiqadan beri javob berilmagan chatlar (har bir oxirgi xabar uchun bir marta)."""
    sent = 0
    async with SessionLocal() as s:
        settings = await get_all(s)
        if not as_bool(settings["notify_unanswered"]):
            return 0
        minutes = int(settings["unanswered_minutes"] or 30)
        border = utcnow() - timedelta(minutes=minutes)
        rows = (
            await s.scalars(
                select(Chat).where(
                    Chat.kind == "private",
                    Chat.visible.is_(True),
                    Chat.muted.is_(False),
                    Chat.last_message_out.is_(False),
                    Chat.last_incoming_at.is_not(None),
                    Chat.last_incoming_at < border,
                    # Juda eski chatlar uchun eslatmaymiz (2 kundan eskisi)
                    Chat.last_incoming_at > utcnow() - timedelta(days=2),
                )
            )
        ).all()
        for chat in rows:
            if not await _once(s, f"unanswered:{chat.id}:{int(chat.last_incoming_at.timestamp())}"):
                continue
            account = await s.get(TgAccount, chat.account_id)
            targets: list[User] = []
            if chat.assigned_user_id:
                assignee = await s.get(User, chat.assigned_user_id)
                if assignee:
                    targets = [assignee]
            if not targets:
                targets = await managers(s, chat.account_id)
            text = (
                f"💬 {minutes} daqiqadan beri javob yo'q: <b>{esc(chat.title)}</b>"
                f" ({esc(account.name if account else '')})\n«{esc((chat.last_message_text or '')[:120])}»"
            )
            sent += await notify_users(targets, text)
        await s.commit()
    return sent


async def daily_summary(today: date) -> int:
    async with SessionLocal() as s:
        settings = await get_all(s)
        if not as_bool(settings["notify_daily"]):
            return 0
        if not await _once(s, f"daily:{today.isoformat()}"):
            return 0
        await s.commit()
        start = datetime.combine(today - timedelta(days=1), time.min, tzinfo=tz())
        end = datetime.combine(today, time.min, tzinfo=tz())
        yesterday = today - timedelta(days=1)
        totals = await period_totals(s, yesterday, yesterday)
        kassa = await balances(s)
        new_orders = await s.scalar(select(func.count(Order.id)).where(Order.created_at >= start, Order.created_at < end))
        open_q = select(Order).join(OrderStatus, OrderStatus.id == Order.status_id).where(
            OrderStatus.is_done.is_(False), OrderStatus.is_cancel.is_(False)
        )
        open_orders = (await s.scalars(open_q)).all()
        due_today = [o for o in open_orders if o.deadline == today]
        overdue = [o for o in open_orders if o.deadline and o.deadline < today]
        debt = await s.scalar(
            select(func.coalesce(func.sum(Order.price - Order.paid), 0))
            .join(OrderStatus, OrderStatus.id == Order.status_id)
            .where(OrderStatus.is_cancel.is_(False), Order.price > Order.paid)
        )
        unread = await s.scalar(
            select(func.count(Chat.id)).where(Chat.unread_count > 0, Chat.visible.is_(True), Chat.muted.is_(False))
        )
        lines = [
            f"☀️ <b>{esc(settings['company_name'])}</b> — {today.strftime('%d.%m.%Y')}",
            f"Kecha: {new_orders or 0} ta yangi zakaz",
            f"Kecha kirim: {fmt_sum(totals['kirim'])}, chiqim: {fmt_sum(totals['chiqim'])}, foyda: {fmt_sum(totals['foyda'])}",
            f"Kassa qoldig'i: {fmt_sum(kassa['jami'])} (naqd {fmt_sum(kassa['naqd'])}, karta {fmt_sum(kassa['karta'])})",
            f"Jarayonda: {len(open_orders)} ta zakaz",
            f"Bugun topshiriladi: {len(due_today)} ta",
            f"Muddati o'tgan: {len(overdue)} ta",
            f"Umumiy qarzdorlik: {fmt_sum(int(debt or 0))}",
            f"O'qilmagan chatlar: {unread or 0} ta",
        ]
        for order in due_today[:10]:
            lines.append("• " + await order_line(s, order))
        admins = (await s.scalars(select(User).where(User.role == "admin", User.is_active.is_(True)))).all()
        return await notify_users(list(admins), "\n".join(lines))
