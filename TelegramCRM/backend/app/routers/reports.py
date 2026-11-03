from __future__ import annotations

from collections import defaultdict
from datetime import date, datetime, time, timedelta

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import admin_only
from ..models import Chat, Client, Message, Order, OrderStatus, Payment, TgAccount, User
from ..timeutil import local_today, tz

router = APIRouter(prefix="/api/reports", tags=["reports"], dependencies=[Depends(admin_only)])


def _bounds(date_from: date, date_to: date) -> tuple[datetime, datetime]:
    zone = tz()
    return (
        datetime.combine(date_from, time.min, tzinfo=zone),
        datetime.combine(date_to + timedelta(days=1), time.min, tzinfo=zone),
    )


def _local_day(value: datetime) -> date:
    return value.astimezone(tz()).date()


@router.get("/summary")
async def summary(
    date_from: date | None = None,
    date_to: date | None = None,
    session: AsyncSession = Depends(get_session),
):
    today = local_today()
    date_to = date_to or today
    date_from = date_from or date_to.replace(day=1)
    if date_from > date_to or (date_to - date_from).days > 731:
        raise HTTPException(400, "Davr noto'g'ri tanlangan")
    start, end = _bounds(date_from, date_to)

    statuses = {s.id: s for s in (await session.scalars(select(OrderStatus).order_by(OrderStatus.position))).all()}
    users = {u.id: u for u in (await session.scalars(select(User))).all()}
    accounts = {a.id: a for a in (await session.scalars(select(TgAccount).order_by(TgAccount.id))).all()}

    payments = (
        await session.execute(
            select(Payment, Order.assignee_id, Order.account_id, Order.client_id)
            .join(Order, Order.id == Payment.order_id)
            .where(Payment.created_at >= start, Payment.created_at < end)
        )
    ).all()
    created = (await session.scalars(select(Order).where(Order.created_at >= start, Order.created_at < end))).all()
    closed = (
        await session.scalars(select(Order).where(Order.closed_at >= start, Order.closed_at < end))
    ).all()
    done = [o for o in closed if statuses.get(o.status_id) and statuses[o.status_id].is_done]

    # Kunlik grafik
    days = [date_from + timedelta(days=i) for i in range((date_to - date_from).days + 1)]
    daily = {d: {"date": d.isoformat(), "revenue": 0, "orders": 0} for d in days}
    for p, *_ in payments:
        day = _local_day(p.created_at)
        if day in daily:
            daily[day]["revenue"] += p.amount
    for o in created:
        day = _local_day(o.created_at)
        if day in daily:
            daily[day]["orders"] += 1

    revenue = sum(p.amount for p, *_ in payments)
    live_created = [o for o in created if not (statuses.get(o.status_id) and statuses[o.status_id].is_cancel)]
    order_sum = sum(o.price for o in live_created)

    # Joriy holatlar (barcha ochiq zakazlar)
    status_rows = (
        await session.execute(
            select(Order.status_id, func.count(Order.id), func.coalesce(func.sum(Order.price), 0)).group_by(Order.status_id)
        )
    ).all()
    status_map = {sid: (cnt, total) for sid, cnt, total in status_rows}
    by_status = [
        {
            "status_id": s.id,
            "name": s.name,
            "color": s.color,
            "count": int(status_map.get(s.id, (0, 0))[0]),
            "sum": int(status_map.get(s.id, (0, 0))[1]),
            "is_done": s.is_done,
            "is_cancel": s.is_cancel,
        }
        for s in statuses.values()
    ]
    open_ids = [s.id for s in statuses.values() if not s.is_done and not s.is_cancel]
    cancel_ids = [s.id for s in statuses.values() if s.is_cancel]
    debt = await session.scalar(
        select(func.coalesce(func.sum(Order.price - Order.paid), 0)).where(
            Order.price > Order.paid, Order.status_id.not_in(cancel_ids or [0])
        )
    )
    overdue = await session.scalar(
        select(func.count(Order.id)).where(Order.status_id.in_(open_ids or [0]), Order.deadline < today)
    )

    # Xodimlar kesimida
    staff: dict[int | None, dict] = defaultdict(lambda: {"orders": 0, "done": 0, "done_sum": 0, "revenue": 0})
    for o in live_created:
        staff[o.assignee_id]["orders"] += 1
    for o in done:
        staff[o.assignee_id]["done"] += 1
        staff[o.assignee_id]["done_sum"] += o.price
    for p, assignee_id, *_ in payments:
        staff[assignee_id]["revenue"] += p.amount
    by_staff = [
        {"user_id": uid, "name": users[uid].name if uid in users else "Biriktirilmagan", **vals}
        for uid, vals in staff.items()
    ]
    by_staff.sort(key=lambda r: (-r["revenue"], -r["orders"]))

    # Akkauntlar kesimida
    msg_rows = (
        await session.execute(
            select(Message.account_id, func.count(Message.id), func.count(func.distinct(Message.chat_id)))
            .where(Message.out.is_(False), Message.date >= start, Message.date < end)
            .group_by(Message.account_id)
        )
    ).all()
    msg_map = {aid: (cnt, chats) for aid, cnt, chats in msg_rows}
    acc_orders: dict[int | None, list[int]] = defaultdict(lambda: [0, 0])
    for o in live_created:
        acc_orders[o.account_id][0] += 1
        acc_orders[o.account_id][1] += o.price
    acc_revenue: dict[int | None, int] = defaultdict(int)
    for p, _assignee, account_id, _client in payments:
        acc_revenue[account_id] += p.amount
    by_account = []
    for aid, acc in accounts.items():
        by_account.append(
            {
                "account_id": aid,
                "name": acc.name,
                "color": acc.color,
                "incoming": int(msg_map.get(aid, (0, 0))[0]),
                "chats": int(msg_map.get(aid, (0, 0))[1]),
                "orders": acc_orders[aid][0],
                "order_sum": acc_orders[aid][1],
                "revenue": acc_revenue[aid],
            }
        )
    if acc_orders.get(None) or acc_revenue.get(None):
        by_account.append(
            {
                "account_id": None,
                "name": "Chatsiz (qo'lda)",
                "color": "#94a3b8",
                "incoming": 0,
                "chats": 0,
                "orders": acc_orders[None][0],
                "order_sum": acc_orders[None][1],
                "revenue": acc_revenue[None],
            }
        )

    by_method: dict[str, int] = defaultdict(int)
    for p, *_ in payments:
        by_method[p.method] += p.amount

    client_rev: dict[int, int] = defaultdict(int)
    for p, _a, _acc, client_id in payments:
        if client_id:
            client_rev[client_id] += p.amount
    top_ids = sorted(client_rev, key=lambda c: -client_rev[c])[:5]
    names = {c.id: c.name for c in (await session.scalars(select(Client).where(Client.id.in_(top_ids or [0])))).all()}
    new_clients = await session.scalar(
        select(func.count(Client.id)).where(Client.created_at >= start, Client.created_at < end)
    )
    new_chats = await session.scalar(select(func.count(Chat.id)).where(Chat.created_at >= start, Chat.created_at < end))

    return {
        "date_from": date_from.isoformat(),
        "date_to": date_to.isoformat(),
        "kpi": {
            "revenue": revenue,
            "orders_created": len(live_created),
            "orders_done": len(done),
            "order_sum": order_sum,
            "avg_check": order_sum // len(live_created) if live_created else 0,
            "debt": int(debt or 0),
            "overdue": int(overdue or 0),
            "new_clients": int(new_clients or 0),
            "new_chats": int(new_chats or 0),
            "incoming": sum(int(v[0]) for v in msg_map.values()),
        },
        "daily": list(daily.values()),
        "by_status": by_status,
        "by_staff": by_staff,
        "by_account": by_account,
        "by_method": [{"method": m, "amount": a} for m, a in sorted(by_method.items(), key=lambda x: -x[1])],
        "top_clients": [{"client_id": cid, "name": names.get(cid, "?"), "revenue": client_rev[cid]} for cid in top_ids],
    }
