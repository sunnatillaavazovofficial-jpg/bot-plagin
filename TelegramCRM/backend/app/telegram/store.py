"""Telegram'dan kelgan chat va xabarlarni bazaga yozish (haqiqiy va sinov rejimi uchun umumiy)."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

from sqlalchemy import func, select, update
from sqlalchemy.dialects.sqlite import insert as sqlite_insert
from sqlalchemy.ext.asyncio import AsyncSession

from ..models import Chat, Message, TgAccount
from ..serializers import chat_dict, message_dict
from ..ws_hub import hub

PREVIEW_LEN = 200

MEDIA_LABELS = {
    "photo": "🖼 Rasm",
    "video": "🎬 Video",
    "voice": "🎤 Ovozli xabar",
    "audio": "🎵 Audio",
    "sticker": "Stiker",
    "gif": "GIF",
    "document": "📎 Fayl",
    "location": "📍 Joylashuv",
    "contact": "👤 Kontakt",
    "other": "Media",
}


@dataclass
class ChatData:
    peer_id: int
    peer_type: str
    title: str
    access_hash: int | None = None
    username: str | None = None
    phone: str | None = None
    kind: str = "private"  # private | bot | group | channel
    can_send: bool = True


@dataclass
class MsgData:
    tg_id: int
    out: bool
    text: str
    date: datetime
    media_type: str | None = None
    media_name: str | None = None
    media_mime: str | None = None
    media_size: int | None = None
    media_path: str | None = None
    sender_name: str | None = None
    reply_to_tg_id: int | None = None
    buttons: str | None = None  # JSON


def default_visible(account: TgAccount | None, kind: str) -> bool:
    """Yangi chat saytda avtomatik ko'rinsinmi — akkaunt sozlamasiga qarab."""
    if account is None:
        return kind == "private"
    return {
        "private": account.show_private,
        "group": account.show_groups,
        "channel": account.show_channels,
        "bot": account.show_bots,
    }.get(kind, False)


def preview_text(data: MsgData | Message) -> str:
    text = (data.text or "").strip()
    if data.media_type:
        label = MEDIA_LABELS.get(data.media_type, "Media")
        if data.media_type == "document" and data.media_name:
            label = f"📎 {data.media_name}"
        text = f"{label} {text}".strip()
    return text[:PREVIEW_LEN]


async def get_chat_by_peer(session: AsyncSession, account_id: int, peer_id: int) -> Chat | None:
    return await session.scalar(
        select(Chat).where(Chat.account_id == account_id, Chat.peer_id == peer_id)
    )


async def upsert_chat(session: AsyncSession, account_id: int, data: ChatData) -> Chat:
    chat = await get_chat_by_peer(session, account_id, data.peer_id)
    if chat is None:
        account = await session.get(TgAccount, account_id)
        chat = Chat(
            account_id=account_id,
            peer_id=data.peer_id,
            peer_type=data.peer_type,
            kind=data.kind,
            can_send=data.can_send,
            visible=default_visible(account, data.kind),
            muted=False,
            title=data.title or "Nomsiz",
            access_hash=data.access_hash,
            username=data.username,
            phone=data.phone,
            unread_count=0,
            read_outbox_max_id=0,
            last_message_out=False,
            history_complete=False,
        )
        session.add(chat)
        await session.flush()
    else:
        chat.title = data.title or chat.title
        chat.kind = data.kind
        chat.can_send = data.can_send
        if data.access_hash is not None:
            chat.access_hash = data.access_hash
        if data.username is not None:
            chat.username = data.username
        if data.phone is not None:
            chat.phone = data.phone
    return chat


async def save_message(
    session: AsyncSession,
    chat: Chat,
    data: MsgData,
    *,
    live: bool,
    sent_by_user_id: int | None = None,
) -> tuple[Message, bool]:
    """Xabarni saqlaydi. live=True — yangi kelgan xabar (o'qilmaganlar soni oshadi)."""
    stmt = (
        sqlite_insert(Message)
        .values(
            chat_id=chat.id,
            account_id=chat.account_id,
            tg_id=data.tg_id,
            out=data.out,
            text=data.text or "",
            date=data.date,
            edited=False,
            deleted=False,
            media_type=data.media_type,
            media_name=data.media_name,
            media_mime=data.media_mime,
            media_size=data.media_size,
            media_path=data.media_path,
            sender_name=data.sender_name,
            reply_to_tg_id=data.reply_to_tg_id,
            buttons=data.buttons,
            sent_by_user_id=sent_by_user_id,
        )
        .on_conflict_do_nothing(index_elements=["chat_id", "tg_id"])
    )
    result = await session.execute(stmt)
    created = bool(result.rowcount)
    msg = await session.scalar(
        select(Message).where(Message.chat_id == chat.id, Message.tg_id == data.tg_id)
    )
    assert msg is not None
    if not created:
        if sent_by_user_id is not None and msg.sent_by_user_id is None:
            msg.sent_by_user_id = sent_by_user_id
        if data.media_path and not msg.media_path:
            msg.media_path = data.media_path

    if created:
        apply_preview(chat, data, live=live)
    return msg, created


def apply_preview(chat: Chat, data: MsgData, *, live: bool) -> None:
    """Chat ro'yxatidagi oxirgi xabar va o'qilmaganlar sonini yangilaydi."""
    if chat.last_message_at is not None and data.date < chat.last_message_at:
        return
    chat.last_message_at = data.date
    chat.last_message_text = preview_text(data)
    chat.last_message_out = data.out
    if not data.out:
        chat.last_incoming_at = data.date
        if live:
            chat.unread_count = (chat.unread_count or 0) + 1
    elif live:
        # Telefondan yoki saytdan javob yozilgan — chat o'qilgan hisoblanadi
        chat.unread_count = 0


async def publish_message(chat: Chat, msg: Message, kind: str = "message.new") -> None:
    await hub.publish(
        {"type": kind, "chat": chat_dict(chat), "message": message_dict(msg)},
        account_id=chat.account_id,
    )


async def publish_chat(chat: Chat) -> None:
    await hub.publish({"type": "chat.update", "chat": chat_dict(chat)}, account_id=chat.account_id)


async def mark_edited(
    session: AsyncSession,
    account_id: int,
    peer_id: int,
    tg_id: int,
    text: str,
    buttons: str | None | bool = False,
) -> tuple[Chat, Message] | None:
    """buttons=False — tugmalarga tegilmaydi; None/JSON — yangilanadi (bot xabarni o'zgartirganda)."""
    chat = await get_chat_by_peer(session, account_id, peer_id)
    if chat is None:
        return None
    msg = await session.scalar(
        select(Message).where(Message.chat_id == chat.id, Message.tg_id == tg_id)
    )
    if msg is None:
        return None
    if (text or "") != msg.text:
        msg.edited = True
    msg.text = text or ""
    if buttons is not False:
        msg.buttons = buttons
    return chat, msg


async def mark_deleted(
    session: AsyncSession, account_id: int, tg_ids: list[int], peer_id: int | None = None
) -> list[tuple[Chat, Message]]:
    query = select(Message, Chat).join(Chat, Chat.id == Message.chat_id).where(
        Message.account_id == account_id, Message.tg_id.in_(tg_ids)
    )
    if peer_id is not None:
        query = query.where(Chat.peer_id == peer_id)
    else:
        # Shaxsiy chat va oddiy guruhlarda xabar raqamlari akkaunt bo'yicha yagona
        query = query.where(Chat.peer_type != "channel")
    rows = (await session.execute(query)).all()
    out = []
    for msg, chat in rows:
        msg.deleted = True
        out.append((chat, msg))
    return out


async def max_tg_id(session: AsyncSession, chat_id: int) -> int:
    value = await session.scalar(select(func.max(Message.tg_id)).where(Message.chat_id == chat_id))
    return int(value or 0)


async def min_tg_id(session: AsyncSession, chat_id: int) -> int | None:
    value = await session.scalar(select(func.min(Message.tg_id)).where(Message.chat_id == chat_id))
    return int(value) if value is not None else None


async def set_read_outbox(session: AsyncSession, chat: Chat, max_id: int) -> None:
    if max_id > (chat.read_outbox_max_id or 0):
        chat.read_outbox_max_id = max_id


async def clear_unread(session: AsyncSession, chat_id: int) -> None:
    await session.execute(update(Chat).where(Chat.id == chat_id).values(unread_count=0))
