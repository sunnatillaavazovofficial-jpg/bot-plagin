"""Bazadagi obyektlarni JSON ko'rinishiga o'tkazish."""

from __future__ import annotations

import json
from datetime import date, datetime

from .models import (
    Chat,
    Client,
    Folder,
    Message,
    Order,
    OrderEvent,
    OrderFile,
    OrderStatus,
    Payment,
    TgAccount,
    User,
)


def _dt(value: datetime | None) -> str | None:
    return value.isoformat() if value else None


def _d(value: date | None) -> str | None:
    return value.isoformat() if value else None


def _json(value: str | None):
    if not value:
        return None
    try:
        return json.loads(value)
    except ValueError:
        return None


def user_dict(u: User, account_ids: list[int] | None = None) -> dict:
    data = {
        "id": u.id,
        "name": u.name,
        "login": u.login,
        "role": u.role,
        "is_active": u.is_active,
        "tg_linked": u.tg_user_id is not None,
        "tg_username": u.tg_username,
    }
    if account_ids is not None:
        data["account_ids"] = account_ids
    return data


def account_dict(a: TgAccount, connected: bool | None = None) -> dict:
    return {
        "id": a.id,
        "phone": a.phone,
        "name": a.name,
        "username": a.username,
        "color": a.color,
        "status": a.status,
        "last_error": a.last_error,
        "show_private": a.show_private,
        "show_groups": a.show_groups,
        "show_channels": a.show_channels,
        "show_bots": a.show_bots,
        "synced_at": _dt(a.synced_at),
        "connected": connected,
    }


def chat_dict(c: Chat) -> dict:
    return {
        "id": c.id,
        "account_id": c.account_id,
        "peer_type": c.peer_type,
        "kind": c.kind,
        "visible": c.visible,
        "muted": c.muted,
        "can_send": c.can_send,
        "folder_id": c.folder_id,
        "title": c.title,
        "username": c.username,
        "phone": c.phone,
        "unread_count": c.unread_count,
        "last_message_at": _dt(c.last_message_at),
        "last_message_text": c.last_message_text,
        "last_message_out": c.last_message_out,
        "last_incoming_at": _dt(c.last_incoming_at),
        "read_outbox_max_id": c.read_outbox_max_id,
        "client_id": c.client_id,
        "assigned_user_id": c.assigned_user_id,
    }


def message_dict(m: Message) -> dict:
    return {
        "id": m.id,
        "chat_id": m.chat_id,
        "tg_id": m.tg_id,
        "out": m.out,
        "text": m.text,
        "date": _dt(m.date),
        "edited": m.edited,
        "deleted": m.deleted,
        "media_type": m.media_type,
        "media_name": m.media_name,
        "media_mime": m.media_mime,
        "media_size": m.media_size,
        "sender_name": m.sender_name,
        "reply_to_tg_id": m.reply_to_tg_id,
        "buttons": _json(m.buttons),
        "sent_by_user_id": m.sent_by_user_id,
    }


def folder_dict(f: Folder) -> dict:
    return {"id": f.id, "name": f.name, "color": f.color, "position": f.position}


def client_dict(c: Client, **extra) -> dict:
    return {
        "id": c.id,
        "name": c.name,
        "phone": c.phone,
        "company": c.company,
        "notes": c.notes,
        "created_at": _dt(c.created_at),
        **extra,
    }


def status_dict(s: OrderStatus) -> dict:
    return {
        "id": s.id,
        "name": s.name,
        "color": s.color,
        "position": s.position,
        "is_done": s.is_done,
        "is_cancel": s.is_cancel,
    }


def order_dict(o: Order, client_name: str | None = None) -> dict:
    return {
        "id": o.id,
        "number": f"#{o.id:04d}",
        "title": o.title,
        "description": o.description,
        "client_id": o.client_id,
        "client_name": client_name,
        "chat_id": o.chat_id,
        "account_id": o.account_id,
        "status_id": o.status_id,
        "position": o.position,
        "price": o.price,
        "paid": o.paid,
        "debt": max(o.price - o.paid, 0),
        "deadline": _d(o.deadline),
        "assignee_id": o.assignee_id,
        "created_by_id": o.created_by_id,
        "created_at": _dt(o.created_at),
        "updated_at": _dt(o.updated_at),
        "closed_at": _dt(o.closed_at),
    }


def payment_dict(p: Payment) -> dict:
    return {
        "id": p.id,
        "order_id": p.order_id,
        "amount": p.amount,
        "method": p.method,
        "note": p.note,
        "created_at": _dt(p.created_at),
        "created_by_id": p.created_by_id,
    }


def file_dict(f: OrderFile) -> dict:
    return {
        "id": f.id,
        "order_id": f.order_id,
        "name": f.name,
        "mime": f.mime,
        "size": f.size,
        "created_at": _dt(f.created_at),
        "uploaded_by_id": f.uploaded_by_id,
    }


def event_dict(e: OrderEvent) -> dict:
    return {
        "id": e.id,
        "order_id": e.order_id,
        "user_id": e.user_id,
        "kind": e.kind,
        "text": e.text,
        "created_at": _dt(e.created_at),
    }
