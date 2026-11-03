from __future__ import annotations

import mimetypes
import shutil
from datetime import date, timedelta
from pathlib import Path

from fastapi import APIRouter, Depends, File, HTTPException, Query, UploadFile
from pydantic import BaseModel
from sqlalchemy import func, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..config import get_settings
from ..db import get_session
from ..deps import admin_only, allowed_accounts, current_user, get_chat_checked, get_order_checked, staff_manager
from ..models import Chat, Client, Message, Order, OrderEvent, OrderFile, OrderStatus, Payment, User, utcnow
from ..notify.events import order_assigned
from ..serializers import event_dict, file_dict, order_dict, payment_dict, status_dict
from ..telegram.base import TelegramError, get_telegram
from ..timeutil import local_today
from ..ws_hub import hub
from .chats import file_response, save_upload

router = APIRouter(prefix="/api", tags=["orders"])

PAYMENT_METHODS = {"naqd", "karta", "o'tkazma", "boshqa"}


def fmt_sum(value: int) -> str:
    return f"{value:,}".replace(",", " ") + " so'm"


async def publish_order(order: Order) -> None:
    await hub.publish({"type": "order.update", "order_id": order.id, "status_id": order.status_id})


# ---------- holatlar ----------


@router.get("/statuses")
async def list_statuses(_: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    rows = (await session.scalars(select(OrderStatus).order_by(OrderStatus.position, OrderStatus.id))).all()
    return [status_dict(s) for s in rows]


class StatusIn(BaseModel):
    id: int | None = None
    name: str
    color: str = "#64748b"
    is_done: bool = False
    is_cancel: bool = False


@router.put("/statuses", dependencies=[Depends(admin_only)])
async def save_statuses(items: list[StatusIn], session: AsyncSession = Depends(get_session)):
    if not items:
        raise HTTPException(400, "Kamida bitta holat bo'lishi kerak")
    existing = {s.id: s for s in (await session.scalars(select(OrderStatus))).all()}
    keep: set[int] = set()
    for pos, item in enumerate(items):
        if not item.name.strip():
            raise HTTPException(400, "Holat nomi bo'sh")
        status = existing.get(item.id) if item.id else None
        if status is None:
            status = OrderStatus()
            session.add(status)
        status.name = item.name.strip()
        status.color = item.color
        status.position = pos
        status.is_done = item.is_done
        status.is_cancel = item.is_cancel
        await session.flush()
        keep.add(status.id)
    for sid, status in existing.items():
        if sid in keep:
            continue
        used = await session.scalar(select(func.count(Order.id)).where(Order.status_id == sid))
        if used:
            raise HTTPException(400, f"\"{status.name}\" holatida zakazlar bor — avval ularni boshqa holatga o'tkazing")
        await session.delete(status)
    await session.commit()
    await hub.publish({"type": "statuses.update"})
    rows = (await session.scalars(select(OrderStatus).order_by(OrderStatus.position))).all()
    return [status_dict(s) for s in rows]


# ---------- zakazlar ----------


def _visible(query, user: User, allowed: set[int] | None):
    if user.role == "xodim":
        return query.where(Order.assignee_id == user.id)
    if allowed is not None:
        # Menejer: o'z akkauntlaridan kelgan va akkauntga bog'lanmagan zakazlar
        query = query.where(or_(Order.account_id.is_(None), Order.account_id.in_(allowed or {0})))
    return query


@router.get("/orders")
async def list_orders(
    status_id: int | None = None,
    assignee_id: int | None = None,
    client_id: int | None = None,
    account_id: int | None = None,
    q: str | None = None,
    scope: str = Query("active", pattern="^(active|all|overdue|done|cancel)$"),
    limit: int = Query(500, le=2000),
    user: User = Depends(current_user),
    session: AsyncSession = Depends(get_session),
):
    allowed = await allowed_accounts(session, user)
    query = (
        select(Order, Client.name)
        .outerjoin(Client, Client.id == Order.client_id)
        .join(OrderStatus, OrderStatus.id == Order.status_id)
    )
    query = _visible(query, user, allowed)
    if status_id:
        query = query.where(Order.status_id == status_id)
    if assignee_id:
        query = query.where(Order.assignee_id == assignee_id)
    if client_id:
        query = query.where(Order.client_id == client_id)
    if account_id:
        query = query.where(Order.account_id == account_id)
    if q:
        like = f"%{q.strip().lstrip('#')}%"
        conds = [Order.title.ilike(like), Order.description.ilike(like), Client.name.ilike(like)]
        if q.strip().lstrip("#").isdigit():
            conds.append(Order.id == int(q.strip().lstrip("#")))
        query = query.where(or_(*conds))
    if scope == "active":
        # Tugallangan/bekor qilinganlarni 30 kundan keyin doskadan yashiramiz
        query = query.where(
            or_(
                (OrderStatus.is_done.is_(False) & OrderStatus.is_cancel.is_(False)),
                Order.closed_at.is_(None),
                Order.closed_at >= utcnow() - timedelta(days=30),
            )
        )
    elif scope == "overdue":
        query = query.where(
            OrderStatus.is_done.is_(False), OrderStatus.is_cancel.is_(False), Order.deadline < local_today()
        )
    elif scope == "done":
        query = query.where(OrderStatus.is_done.is_(True))
    elif scope == "cancel":
        query = query.where(OrderStatus.is_cancel.is_(True))
    query = query.order_by(Order.position, Order.created_at.desc()).limit(limit)
    rows = (await session.execute(query)).all()
    return [order_dict(o, name) for o, name in rows]


class OrderIn(BaseModel):
    title: str
    description: str = ""
    client_id: int | None = None
    chat_id: int | None = None
    status_id: int | None = None
    price: int = 0
    deadline: date | None = None
    assignee_id: int | None = None
    message_ids: list[int] = []


async def _copy_message_files(session: AsyncSession, order: Order, chat: Chat, message_ids: list[int], user_id: int):
    """Xabardagi rasm/fayllarni zakaz fayllariga ko'chiradi."""
    tg = get_telegram()
    folder = get_settings().media_dir / "orders" / str(order.id)
    for mid in message_ids:
        msg = await session.get(Message, mid)
        if msg is None or msg.chat_id != chat.id or not msg.media_type:
            continue
        try:
            src = await tg.download_media(msg)
        except TelegramError:
            continue
        folder.mkdir(parents=True, exist_ok=True)
        dst = folder / f"{msg.id}-{src.name}"
        shutil.copy(src, dst)
        session.add(
            OrderFile(
                order_id=order.id,
                name=msg.media_name or src.name,
                path=str(dst),
                mime=msg.media_mime or mimetypes.guess_type(src.name)[0],
                size=dst.stat().st_size,
                uploaded_by_id=user_id,
            )
        )


@router.post("/orders")
async def create_order(data: OrderIn, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    if not data.title.strip():
        raise HTTPException(400, "Zakaz nomini kiriting")
    chat = await get_chat_checked(session, user, data.chat_id) if data.chat_id else None
    client_id = data.client_id
    if chat is not None and client_id is None:
        if chat.client_id is None:
            client = Client(name=chat.title or "Mijoz", phone=chat.phone)
            session.add(client)
            await session.flush()
            chat.client_id = client.id
        client_id = chat.client_id
    if chat is not None and client_id is not None and chat.client_id is None:
        chat.client_id = client_id
    status_id = data.status_id or await session.scalar(
        select(OrderStatus.id).order_by(OrderStatus.position).limit(1)
    )
    if status_id is None:
        raise HTTPException(400, "Zakaz holatlari sozlanmagan")
    order = Order(
        title=data.title.strip(),
        description=data.description,
        client_id=client_id,
        chat_id=chat.id if chat else None,
        account_id=chat.account_id if chat else None,
        status_id=status_id,
        price=max(data.price, 0),
        paid=0,
        deadline=data.deadline,
        assignee_id=data.assignee_id,
        created_by_id=user.id,
        position=0,
    )
    session.add(order)
    await session.flush()
    session.add(OrderEvent(order_id=order.id, user_id=user.id, kind="created", text="Zakaz yaratildi"))
    if chat is not None and data.message_ids:
        await _copy_message_files(session, order, chat, data.message_ids, user.id)
    await session.commit()
    await publish_order(order)
    if order.assignee_id and order.assignee_id != user.id:
        await order_assigned(order.id, user.name)
    client_name = (await session.get(Client, client_id)).name if client_id else None
    return order_dict(order, client_name)


@router.get("/orders/{order_id}")
async def get_order(order_id: int, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    order = await get_order_checked(session, user, order_id)
    client = await session.get(Client, order.client_id) if order.client_id else None
    data = order_dict(order, client.name if client else None)
    data["client_phone"] = client.phone if client else None
    data["payments"] = [
        payment_dict(p)
        for p in (await session.scalars(select(Payment).where(Payment.order_id == order.id).order_by(Payment.id))).all()
    ]
    data["files"] = [
        file_dict(f)
        for f in (await session.scalars(select(OrderFile).where(OrderFile.order_id == order.id).order_by(OrderFile.id))).all()
    ]
    data["events"] = [
        event_dict(e)
        for e in (
            await session.scalars(select(OrderEvent).where(OrderEvent.order_id == order.id).order_by(OrderEvent.id.desc()))
        ).all()
    ]
    return data


class OrderPatch(BaseModel):
    title: str | None = None
    description: str | None = None
    client_id: int | None = None
    status_id: int | None = None
    price: int | None = None
    deadline: date | None = None
    assignee_id: int | None = None
    position: int | None = None


XODIM_FIELDS = {"status_id", "position"}


@router.patch("/orders/{order_id}")
async def update_order(
    order_id: int, data: OrderPatch, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    order = await get_order_checked(session, user, order_id)
    fields = data.model_fields_set
    if user.role == "xodim" and fields - XODIM_FIELDS:
        raise HTTPException(403, "Siz faqat zakaz holatini o'zgartira olasiz")
    events: list[tuple[str, str]] = []
    newly_assigned = False
    if "status_id" in fields and data.status_id and data.status_id != order.status_id:
        new = await session.get(OrderStatus, data.status_id)
        old = await session.get(OrderStatus, order.status_id)
        if new is None:
            raise HTTPException(404, "Holat topilmadi")
        order.status_id = new.id
        order.closed_at = utcnow() if (new.is_done or new.is_cancel) else None
        events.append(("status", f"{old.name if old else '?'} → {new.name}"))
    if "title" in fields and data.title and data.title.strip():
        order.title = data.title.strip()
    if "description" in fields and data.description is not None:
        order.description = data.description
    if "client_id" in fields:
        if data.client_id is not None and await session.get(Client, data.client_id) is None:
            raise HTTPException(404, "Mijoz topilmadi")
        order.client_id = data.client_id
    if "price" in fields and data.price is not None and data.price != order.price:
        events.append(("edit", f"Narx: {fmt_sum(order.price)} → {fmt_sum(max(data.price, 0))}"))
        order.price = max(data.price, 0)
    if "deadline" in fields and data.deadline != order.deadline:
        events.append(("edit", f"Muddat: {data.deadline.strftime('%d.%m.%Y') if data.deadline else 'olib tashlandi'}"))
        order.deadline = data.deadline
    if "assignee_id" in fields and data.assignee_id != order.assignee_id:
        assignee = await session.get(User, data.assignee_id) if data.assignee_id else None
        events.append(("assign", f"Mas'ul: {assignee.name if assignee else 'yo`q'}"))
        order.assignee_id = data.assignee_id
        newly_assigned = bool(data.assignee_id and data.assignee_id != user.id)
    if "position" in fields and data.position is not None:
        order.position = data.position
    for kind, text in events:
        session.add(OrderEvent(order_id=order.id, user_id=user.id, kind=kind, text=text))
    await session.commit()
    await publish_order(order)
    if newly_assigned:
        await order_assigned(order.id, user.name)
    client = await session.get(Client, order.client_id) if order.client_id else None
    return order_dict(order, client.name if client else None)


class ReorderIn(BaseModel):
    status_id: int
    order_ids: list[int]


@router.post("/orders/reorder")
async def reorder(data: ReorderIn, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    """Kanban ustunidagi tartibni saqlash."""
    for pos, oid in enumerate(data.order_ids):
        order = await session.get(Order, oid)
        if order is None or order.status_id != data.status_id:
            continue
        if user.role == "xodim" and order.assignee_id != user.id:
            continue
        order.position = pos
    await session.commit()
    return {"ok": True}


@router.delete("/orders/{order_id}", dependencies=[Depends(admin_only)])
async def delete_order(order_id: int, session: AsyncSession = Depends(get_session)):
    order = await session.get(Order, order_id)
    if order is None:
        raise HTTPException(404, "Zakaz topilmadi")
    await session.delete(order)
    await session.commit()
    shutil.rmtree(get_settings().media_dir / "orders" / str(order_id), ignore_errors=True)
    await hub.publish({"type": "order.update", "order_id": order_id, "deleted": True})
    return {"ok": True}


# ---------- izoh, to'lov, fayl ----------


class CommentIn(BaseModel):
    text: str


@router.post("/orders/{order_id}/comments")
async def add_comment(
    order_id: int, data: CommentIn, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    order = await get_order_checked(session, user, order_id)
    if not data.text.strip():
        raise HTTPException(400, "Izoh bo'sh")
    event = OrderEvent(order_id=order.id, user_id=user.id, kind="comment", text=data.text.strip())
    session.add(event)
    await session.commit()
    await publish_order(order)
    return event_dict(event)


class PaymentIn(BaseModel):
    amount: int
    method: str = "naqd"
    note: str | None = None


async def _recalc_paid(session: AsyncSession, order: Order) -> None:
    total = await session.scalar(select(func.coalesce(func.sum(Payment.amount), 0)).where(Payment.order_id == order.id))
    order.paid = int(total or 0)


@router.post("/orders/{order_id}/payments")
async def add_payment(
    order_id: int, data: PaymentIn, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    order = await get_order_checked(session, user, order_id)
    if data.amount == 0:
        raise HTTPException(400, "Summani kiriting")
    method = data.method if data.method in PAYMENT_METHODS else "boshqa"
    payment = Payment(order_id=order.id, amount=data.amount, method=method, note=data.note, created_by_id=user.id)
    session.add(payment)
    await session.flush()
    await _recalc_paid(session, order)
    label = "To'lov" if data.amount > 0 else "Qaytarildi"
    session.add(OrderEvent(order_id=order.id, user_id=user.id, kind="payment", text=f"{label}: {fmt_sum(abs(data.amount))} ({method})"))
    await session.commit()
    await publish_order(order)
    return payment_dict(payment)


@router.delete("/payments/{payment_id}", dependencies=[Depends(admin_only)])
async def delete_payment(payment_id: int, user: User = Depends(admin_only), session: AsyncSession = Depends(get_session)):
    payment = await session.get(Payment, payment_id)
    if payment is None:
        raise HTTPException(404, "To'lov topilmadi")
    order = await session.get(Order, payment.order_id)
    assert order is not None
    await session.delete(payment)
    await session.flush()
    await _recalc_paid(session, order)
    session.add(OrderEvent(order_id=order.id, user_id=user.id, kind="payment", text=f"To'lov o'chirildi: {fmt_sum(payment.amount)}"))
    await session.commit()
    await publish_order(order)
    return {"ok": True}


@router.post("/orders/{order_id}/files")
async def upload_file(
    order_id: int,
    file: UploadFile = File(...),
    user: User = Depends(current_user),
    session: AsyncSession = Depends(get_session),
):
    order = await get_order_checked(session, user, order_id)
    path = await save_upload(file, get_settings().media_dir / "orders" / str(order.id))
    record = OrderFile(
        order_id=order.id,
        name=path.name,
        path=str(path),
        mime=file.content_type or mimetypes.guess_type(path.name)[0],
        size=path.stat().st_size,
        uploaded_by_id=user.id,
    )
    session.add(record)
    session.add(OrderEvent(order_id=order.id, user_id=user.id, kind="file", text=f"Fayl qo'shildi: {path.name}"))
    await session.commit()
    await publish_order(order)
    return file_dict(record)


async def _file_checked(session: AsyncSession, user: User, file_id: int) -> OrderFile:
    record = await session.get(OrderFile, file_id)
    if record is None:
        raise HTTPException(404, "Fayl topilmadi")
    await get_order_checked(session, user, record.order_id)
    return record


@router.get("/order-files/{file_id}")
async def get_file(
    file_id: int, download: bool = False, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    record = await _file_checked(session, user, file_id)
    path = Path(record.path)
    if not path.exists() or not path.resolve().is_relative_to(get_settings().media_dir.resolve()):
        raise HTTPException(404, "Fayl topilmadi")
    mime = record.mime or mimetypes.guess_type(path.name)[0] or "application/octet-stream"
    return file_response(path, mime, record.name, download)


@router.delete("/order-files/{file_id}")
async def delete_file(file_id: int, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    record = await _file_checked(session, user, file_id)
    Path(record.path).unlink(missing_ok=True)
    session.add(OrderEvent(order_id=record.order_id, user_id=user.id, kind="file", text=f"Fayl o'chirildi: {record.name}"))
    await session.delete(record)
    await session.commit()
    return {"ok": True}


# ---------- zakaz faylini mijozga yuborish ----------


class SendFileIn(BaseModel):
    caption: str = ""


@router.post("/order-files/{file_id}/send")
async def send_file_to_chat(
    file_id: int, data: SendFileIn, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    """Maketni zakaz bog'langan chatga (mijozga) yuborish."""
    record = await _file_checked(session, user, file_id)
    order = await session.get(Order, record.order_id)
    if order is None or order.chat_id is None:
        raise HTTPException(400, "Zakaz chatga bog'lanmagan")
    chat = await get_chat_checked(session, user, order.chat_id)
    try:
        await get_telegram().send_message(
            chat, data.caption, user_id=user.id, file_path=Path(record.path), as_document=True
        )
    except TelegramError as exc:
        raise HTTPException(400, str(exc)) from exc
    session.add(OrderEvent(order_id=order.id, user_id=user.id, kind="file", text=f"Mijozga yuborildi: {record.name}"))
    await session.commit()
    return {"ok": True}
