from __future__ import annotations

import asyncio
import mimetypes
import re
import secrets
from datetime import timedelta
from pathlib import Path

from fastapi import APIRouter, Depends, File, Form, HTTPException, Query, UploadFile
from fastapi.responses import FileResponse
from pydantic import BaseModel
from sqlalchemy import and_, func, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..config import get_settings
from ..db import get_session
from ..deps import allowed_accounts, current_user, get_chat_checked, staff_manager
from ..models import Chat, Client, Folder, Message, Order, User, utcnow
from ..serializers import chat_dict, client_dict, message_dict, order_dict
from ..settings_store import get_all
from ..telegram import store
from ..telegram.base import TelegramError, get_telegram, media_dir_for
from ..ws_hub import hub

router = APIRouter(prefix="/api", tags=["chats"])

_background: set[asyncio.Task] = set()


def run_background(coro) -> None:
    task = asyncio.create_task(coro)
    _background.add(task)
    task.add_done_callback(_background.discard)

MAX_UPLOAD = 1900 * 1024 * 1024  # Telegram cheklovi ~2 GB


def safe_name(name: str) -> str:
    name = Path(name or "fayl").name
    name = re.sub(r"[^\w.\- ()]+", "_", name, flags=re.UNICODE).strip() or "fayl"
    return name[:120]


async def save_upload(upload: UploadFile, folder: Path) -> Path:
    target_dir = folder / secrets.token_hex(6)
    target_dir.mkdir(parents=True, exist_ok=True)
    target = target_dir / safe_name(upload.filename or "fayl")
    size = 0
    with target.open("wb") as fh:
        while chunk := await upload.read(1024 * 1024):
            size += len(chunk)
            if size > MAX_UPLOAD:
                fh.close()
                target.unlink(missing_ok=True)
                raise HTTPException(400, "Fayl juda katta")
            fh.write(chunk)
    return target


KIND_PATTERN = "^(private|bot|group|channel)$"


def _search(query, q: str | None):
    if not q:
        return query
    like = f"%{q.strip()}%"
    digits = re.sub(r"\D", "", q)
    conds = [Chat.title.ilike(like), Chat.username.ilike(like), Chat.last_message_text.ilike(like)]
    if len(digits) >= 3:
        conds.append(Chat.phone.like(f"%{digits}%"))
    return query.where(or_(*conds))


def waiting_condition():
    """«Javob kutmoqda» faqat shaxsiy (mijoz) chatlar uchun, ovozsizlari hisobga olinmaydi."""
    return and_(Chat.kind == "private", Chat.muted.is_(False), Chat.last_message_out.is_(False))


@router.get("/chats")
async def list_chats(
    account_id: int | None = None,
    filter: str = Query("all", pattern="^(all|unread|unanswered|mine|noclient)$"),
    kind: str | None = Query(None, pattern=KIND_PATTERN),
    folder_id: int | None = Query(None, description="0 — papkasiz chatlar"),
    q: str | None = None,
    limit: int = Query(60, le=200),
    offset: int = 0,
    user: User = Depends(current_user),
    session: AsyncSession = Depends(get_session),
):
    allowed = await allowed_accounts(session, user)
    query = select(Chat).where(Chat.last_message_at.is_not(None), Chat.visible.is_(True))
    if allowed is not None:
        query = query.where(Chat.account_id.in_(allowed or {0}))
    if account_id:
        query = query.where(Chat.account_id == account_id)
    if kind:
        query = query.where(Chat.kind == kind)
    if folder_id is not None:
        query = query.where(Chat.folder_id.is_(None) if folder_id == 0 else Chat.folder_id == folder_id)
    if filter == "unread":
        query = query.where(Chat.unread_count > 0)
    elif filter == "unanswered":
        query = query.where(waiting_condition())
    elif filter == "mine":
        query = query.where(Chat.assigned_user_id == user.id)
    elif filter == "noclient":
        query = query.where(Chat.client_id.is_(None))
    query = _search(query, q)
    query = query.order_by(Chat.last_message_at.desc()).limit(limit).offset(offset)
    chats = (await session.scalars(query)).all()
    return [chat_dict(c) for c in chats]


@router.get("/chats/counts")
async def chat_counts(user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    """Menyudagi raqamlar: o'qilmagan chatlar va javob kutayotganlar (yashirin va ovozsizlar hisoblanmaydi)."""
    allowed = await allowed_accounts(session, user)
    base = [Chat.last_message_at.is_not(None), Chat.visible.is_(True), Chat.muted.is_(False)]
    if allowed is not None:
        base.append(Chat.account_id.in_(allowed or {0}))
    unread = await session.scalar(select(func.count(Chat.id)).where(*base, Chat.unread_count > 0))
    settings = await get_all(session)
    late = utcnow() - timedelta(minutes=int(settings["unanswered_minutes"] or 30))
    waiting = await session.scalar(
        select(func.count(Chat.id)).where(*base, waiting_condition(), Chat.last_incoming_at < late)
    )
    by_kind = dict(
        (
            await session.execute(
                select(Chat.kind, func.count(Chat.id)).where(*base, Chat.unread_count > 0).group_by(Chat.kind)
            )
        ).all()
    )
    return {"unread": int(unread or 0), "waiting": int(waiting or 0), "by_kind": by_kind}


@router.get("/chats/picker")
async def chat_picker(
    account_id: int | None = None,
    kind: str | None = Query(None, pattern=KIND_PATTERN),
    visible: bool | None = None,
    q: str | None = None,
    limit: int = Query(200, le=1000),
    offset: int = 0,
    user: User = Depends(staff_manager),
    session: AsyncSession = Depends(get_session),
):
    """«Chatlarni tanlash» oynasi: yashirinlari bilan birga barcha chatlar."""
    allowed = await allowed_accounts(session, user)
    query = select(Chat)
    if allowed is not None:
        query = query.where(Chat.account_id.in_(allowed or {0}))
    if account_id:
        query = query.where(Chat.account_id == account_id)
    if kind:
        query = query.where(Chat.kind == kind)
    if visible is not None:
        query = query.where(Chat.visible.is_(visible))
    query = _search(query, q)
    total = await session.scalar(select(func.count()).select_from(query.subquery()))
    query = query.order_by(Chat.last_message_at.desc().nulls_last(), Chat.id).limit(limit).offset(offset)
    chats = (await session.scalars(query)).all()
    counts_q = select(Chat.kind, Chat.visible, func.count(Chat.id)).group_by(Chat.kind, Chat.visible)
    if allowed is not None:
        counts_q = counts_q.where(Chat.account_id.in_(allowed or {0}))
    if account_id:
        counts_q = counts_q.where(Chat.account_id == account_id)
    counts: dict[str, dict[str, int]] = {}
    for k, vis, n in (await session.execute(counts_q)).all():
        counts.setdefault(k, {"visible": 0, "hidden": 0})["visible" if vis else "hidden"] += n
    return {"total": int(total or 0), "counts": counts, "chats": [chat_dict(c) for c in chats]}


class BulkIn(BaseModel):
    chat_ids: list[int]
    visible: bool | None = None
    muted: bool | None = None
    folder_id: int | None = None
    clear_folder: bool = False


async def _apply_view(session: AsyncSession, chat: Chat, visible, muted, folder_id, clear_folder) -> bool:
    """Ko'rinish, ovozsiz va papka sozlamasi. Qaytaradi: chat endi ko'rinadigan bo'ldimi."""
    became_visible = visible is True and not chat.visible
    if visible is not None:
        chat.visible = visible
    if muted is not None:
        chat.muted = muted
    if clear_folder:
        chat.folder_id = None
    elif folder_id is not None:
        if await session.get(Folder, folder_id) is None:
            raise HTTPException(404, "Papka topilmadi")
        chat.folder_id = folder_id
    return became_visible


@router.post("/chats/bulk")
async def bulk_update(data: BulkIn, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    if len(data.chat_ids) > 2000:
        raise HTTPException(400, "Bir martada ko'pi bilan 2000 ta chat")
    allowed = await allowed_accounts(session, user)
    chats = (await session.scalars(select(Chat).where(Chat.id.in_(data.chat_ids or [0])))).all()
    shown: list[int] = []
    for chat in chats:
        if allowed is not None and chat.account_id not in allowed:
            raise HTTPException(403, "Bu akkauntga ruxsat yo'q")
        if await _apply_view(session, chat, data.visible, data.muted, data.folder_id, data.clear_folder):
            shown.append(chat.id)
    await session.commit()
    for cid in shown:
        run_background(get_telegram().show_chat(cid))
    await hub.publish({"type": "chats.reload", "account_id": 0})
    return {"ok": True, "updated": len(chats)}


@router.get("/chats/{chat_id}")
async def get_chat(chat_id: int, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    chat = await get_chat_checked(session, user, chat_id)
    data = chat_dict(chat)
    client = await session.get(Client, chat.client_id) if chat.client_id else None
    data["client"] = client_dict(client) if client else None
    orders_q = (
        select(Order)
        .where(or_(Order.chat_id == chat.id, and_(Order.client_id.is_not(None), Order.client_id == chat.client_id)))
        .order_by(Order.created_at.desc())
        .limit(20)
    )
    if user.role == "xodim":
        orders_q = orders_q.where(Order.assignee_id == user.id)
    data["orders"] = [order_dict(o) for o in (await session.scalars(orders_q)).all()]
    return data


class ChatPatch(BaseModel):
    assigned_user_id: int | None = None
    client_id: int | None = None
    visible: bool | None = None
    muted: bool | None = None
    folder_id: int | None = None


@router.patch("/chats/{chat_id}")
async def update_chat(
    chat_id: int, data: ChatPatch, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    chat = await get_chat_checked(session, user, chat_id)
    fields = data.model_fields_set
    if "assigned_user_id" in fields:
        chat.assigned_user_id = data.assigned_user_id
    if "client_id" in fields:
        if user.role == "xodim":
            raise HTTPException(403, "Ruxsat yo'q")
        if data.client_id is not None and await session.get(Client, data.client_id) is None:
            raise HTTPException(404, "Mijoz topilmadi")
        chat.client_id = data.client_id
    became_visible = False
    if fields & {"visible", "muted", "folder_id"}:
        if user.role == "xodim":
            raise HTTPException(403, "Ruxsat yo'q")
        became_visible = await _apply_view(
            session,
            chat,
            data.visible if "visible" in fields else None,
            data.muted if "muted" in fields else None,
            data.folder_id,
            "folder_id" in fields and data.folder_id is None,
        )
    await session.commit()
    if became_visible:
        run_background(get_telegram().show_chat(chat.id))
    if "visible" in fields:
        await hub.publish({"type": "chats.reload", "account_id": chat.account_id}, account_id=chat.account_id)
    await store.publish_chat(chat)
    return chat_dict(chat)


@router.post("/chats/{chat_id}/client")
async def create_client_from_chat(
    chat_id: int, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    if user.role == "xodim":
        raise HTTPException(403, "Ruxsat yo'q")
    chat = await get_chat_checked(session, user, chat_id)
    if chat.client_id:
        client = await session.get(Client, chat.client_id)
        return client_dict(client) if client else None
    client = None
    if chat.phone:
        client = await session.scalar(select(Client).where(Client.phone == chat.phone))
    if client is None:
        client = Client(name=chat.title or "Mijoz", phone=chat.phone)
        session.add(client)
        await session.flush()
    chat.client_id = client.id
    await session.commit()
    await store.publish_chat(chat)
    return client_dict(client)


@router.post("/chats/{chat_id}/read")
async def mark_read(chat_id: int, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    chat = await get_chat_checked(session, user, chat_id)
    had_unread = chat.unread_count > 0
    chat.unread_count = 0
    await session.commit()
    if had_unread:
        await get_telegram().mark_read(chat)
        await store.publish_chat(chat)
    return {"ok": True}


@router.get("/chats/{chat_id}/messages")
async def list_messages(
    chat_id: int,
    before_id: int | None = None,
    limit: int = Query(50, le=200),
    user: User = Depends(current_user),
    session: AsyncSession = Depends(get_session),
):
    chat = await get_chat_checked(session, user, chat_id)

    async def fetch() -> list[Message]:
        query = select(Message).where(Message.chat_id == chat.id)
        if before_id:
            query = query.where(Message.tg_id < before_id)
        return list((await session.scalars(query.order_by(Message.tg_id.desc()).limit(limit))).all())

    messages = await fetch()
    if len(messages) < limit and not chat.history_complete:
        if await get_telegram().load_older(chat.id, limit=max(limit, 50)):
            session.expire_all()
            messages = await fetch()
        await session.refresh(chat)
    messages.reverse()
    return {
        "messages": [message_dict(m) for m in messages],
        "has_more": len(messages) >= limit or not chat.history_complete,
    }


@router.post("/chats/{chat_id}/messages")
async def send_message(
    chat_id: int,
    text: str = Form(""),
    reply_to: int | None = Form(None),
    as_document: bool = Form(False),
    file: UploadFile | None = File(None),
    user: User = Depends(current_user),
    session: AsyncSession = Depends(get_session),
):
    chat = await get_chat_checked(session, user, chat_id)
    if not chat.can_send:
        raise HTTPException(400, "Bu chatga yozish huquqi yo'q (masalan, siz admin bo'lmagan kanal)")
    text = text.strip()
    if not text and file is None:
        raise HTTPException(400, "Xabar bo'sh")
    if len(text) > 4096 or (file is not None and len(text) > 1024):
        raise HTTPException(400, "Xabar juda uzun (matn 4096, fayl izohi 1024 belgigacha)")
    path = None
    if file is not None:
        path = await save_upload(file, media_dir_for(get_settings().media_dir, chat.account_id, chat.id))
    try:
        msg = await get_telegram().send_message(
            chat, text, user_id=user.id, reply_to=reply_to, file_path=path, as_document=as_document
        )
    except TelegramError as exc:
        raise HTTPException(400, str(exc)) from exc
    return message_dict(msg)


class ClickIn(BaseModel):
    row: int
    col: int


@router.post("/messages/{message_id}/click")
async def click_button(
    message_id: int, data: ClickIn, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    """Bot xabaridagi tugmani bosish (bot javobi bo'lsa qaytariladi)."""
    msg = await session.get(Message, message_id)
    if msg is None:
        raise HTTPException(404, "Xabar topilmadi")
    await get_chat_checked(session, user, msg.chat_id)
    try:
        answer = await get_telegram().click_button(msg, data.row, data.col)
    except TelegramError as exc:
        raise HTTPException(400, str(exc)) from exc
    return {"ok": True, "answer": answer}


@router.get("/media/{message_id}")
async def get_media(
    message_id: int,
    download: bool = False,
    user: User = Depends(current_user),
    session: AsyncSession = Depends(get_session),
):
    msg = await session.get(Message, message_id)
    if msg is None or not msg.media_type:
        raise HTTPException(404, "Fayl topilmadi")
    await get_chat_checked(session, user, msg.chat_id)
    try:
        path = await get_telegram().download_media(msg)
    except TelegramError as exc:
        raise HTTPException(404, str(exc)) from exc
    media_root = get_settings().media_dir.resolve()
    if not path.resolve().is_relative_to(media_root) or not path.exists():
        raise HTTPException(404, "Fayl topilmadi")
    mime = msg.media_mime or mimetypes.guess_type(path.name)[0] or "application/octet-stream"
    return file_response(path, mime, msg.media_name or path.name, download)


INLINE_SAFE = re.compile(r"^(image/(jpeg|png|gif|webp|svg\+xml)|video/(mp4|webm|quicktime)|audio/(ogg|mpeg|mp4|x-m4a)|application/pdf)$")


def file_response(path: Path, mime: str, name: str, download: bool) -> FileResponse:
    """Begona fayllar saytimiz nomidan skript ishga tushira olmasligi uchun qat'iy sarlavhalar."""
    inline = not download and bool(INLINE_SAFE.match(mime))
    return FileResponse(
        path,
        media_type=mime if inline else "application/octet-stream" if not download else mime,
        filename=name,
        content_disposition_type="inline" if inline else "attachment",
        headers={
            "Cache-Control": "private, max-age=86400",
            "X-Content-Type-Options": "nosniff",
            "Content-Security-Policy": "sandbox; default-src 'none'; img-src 'self' data:; style-src 'unsafe-inline'; media-src 'self'",
        },
    )

