from __future__ import annotations

import re

from fastapi import APIRouter, Depends, HTTPException, Query
from pydantic import BaseModel
from sqlalchemy import case, func, or_, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import admin_only, allowed_accounts, staff_manager
from ..models import Chat, Client, Order, OrderStatus, User
from ..serializers import chat_dict, client_dict, order_dict

router = APIRouter(prefix="/api/clients", tags=["clients"])


def _order_stats():
    active = case((OrderStatus.is_cancel.is_(False), 1), else_=0)
    return (
        select(
            Order.client_id,
            func.count(Order.id).label("orders"),
            func.coalesce(func.sum(Order.price * active), 0).label("total"),
            func.coalesce(
                func.sum(case((Order.price > Order.paid, Order.price - Order.paid), else_=0) * active), 0
            ).label("debt"),
            func.max(Order.created_at).label("last_order_at"),
        )
        .join(OrderStatus, OrderStatus.id == Order.status_id)
        .group_by(Order.client_id)
        .subquery()
    )


@router.get("")
async def list_clients(
    q: str | None = None,
    debt: bool = False,
    limit: int = Query(100, le=500),
    offset: int = 0,
    _: User = Depends(staff_manager),
    session: AsyncSession = Depends(get_session),
):
    stats = _order_stats()
    query = select(Client, stats.c.orders, stats.c.total, stats.c.debt, stats.c.last_order_at).outerjoin(
        stats, stats.c.client_id == Client.id
    )
    if q:
        like = f"%{q.strip()}%"
        digits = re.sub(r"\D", "", q)
        conds = [Client.name.ilike(like), Client.company.ilike(like), Client.notes.ilike(like)]
        if len(digits) >= 3:
            conds.append(Client.phone.like(f"%{digits}%"))
        query = query.where(or_(*conds))
    if debt:
        query = query.where(stats.c.debt > 0)
    query = query.order_by(func.coalesce(stats.c.last_order_at, Client.created_at).desc()).limit(limit).offset(offset)
    rows = (await session.execute(query)).all()
    return [
        client_dict(c, orders=int(n or 0), total=int(t or 0), debt=int(d or 0)) for c, n, t, d, _last in rows
    ]


class ClientIn(BaseModel):
    name: str
    phone: str | None = None
    company: str | None = None
    notes: str | None = None


def _norm_phone(phone: str | None) -> str | None:
    if not phone:
        return None
    digits = re.sub(r"\D", "", phone)
    return f"+{digits}" if digits else None


@router.post("")
async def create_client(data: ClientIn, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    if not data.name.strip():
        raise HTTPException(400, "Mijoz ismini kiriting")
    client = Client(
        name=data.name.strip(), phone=_norm_phone(data.phone), company=data.company, notes=data.notes
    )
    session.add(client)
    await session.commit()
    return client_dict(client, orders=0, total=0, debt=0)


@router.get("/{client_id}")
async def get_client(
    client_id: int, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    client = await session.get(Client, client_id)
    if client is None:
        raise HTTPException(404, "Mijoz topilmadi")
    allowed = await allowed_accounts(session, user)
    chats_q = select(Chat).where(Chat.client_id == client.id).order_by(Chat.last_message_at.desc())
    if allowed is not None:
        chats_q = chats_q.where(Chat.account_id.in_(allowed or {0}))
    chats = (await session.scalars(chats_q)).all()
    orders = (
        await session.scalars(select(Order).where(Order.client_id == client.id).order_by(Order.created_at.desc()))
    ).all()
    cancel_ids = set((await session.scalars(select(OrderStatus.id).where(OrderStatus.is_cancel))).all())
    live = [o for o in orders if o.status_id not in cancel_ids]
    return client_dict(
        client,
        chats=[chat_dict(c) for c in chats],
        orders=[order_dict(o, client.name) for o in orders],
        total=sum(o.price for o in live),
        paid=sum(o.paid for o in live),
        debt=sum(max(o.price - o.paid, 0) for o in live),
    )


@router.patch("/{client_id}")
async def update_client(
    client_id: int, data: ClientIn, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    client = await session.get(Client, client_id)
    if client is None:
        raise HTTPException(404, "Mijoz topilmadi")
    if data.name.strip():
        client.name = data.name.strip()
    client.phone = _norm_phone(data.phone)
    client.company = data.company
    client.notes = data.notes
    await session.commit()
    return client_dict(client)


class MergeIn(BaseModel):
    other_id: int


@router.post("/{client_id}/merge", dependencies=[Depends(staff_manager)])
async def merge_client(client_id: int, data: MergeIn, session: AsyncSession = Depends(get_session)):
    """Ikki mijozni birlashtirish: other_id dagi chat va zakazlar shu mijozga o'tadi."""
    client = await session.get(Client, client_id)
    other = await session.get(Client, data.other_id)
    if client is None or other is None or client.id == other.id:
        raise HTTPException(400, "Mijozlarni tanlang")
    await session.execute(update(Chat).where(Chat.client_id == other.id).values(client_id=client.id))
    await session.execute(update(Order).where(Order.client_id == other.id).values(client_id=client.id))
    client.phone = client.phone or other.phone
    client.company = client.company or other.company
    if other.notes:
        client.notes = f"{client.notes or ''}\n{other.notes}".strip()
    await session.delete(other)
    await session.commit()
    return {"ok": True}


@router.delete("/{client_id}", dependencies=[Depends(admin_only)])
async def delete_client(client_id: int, session: AsyncSession = Depends(get_session)):
    client = await session.get(Client, client_id)
    if client is None:
        raise HTTPException(404, "Mijoz topilmadi")
    await session.delete(client)
    await session.commit()
    return {"ok": True}
