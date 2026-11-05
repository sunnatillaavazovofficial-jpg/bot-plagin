from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy import delete, func, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import admin_only, allowed_accounts, current_user
from ..models import Chat, Order, TgAccount, User
from ..serializers import account_dict
from ..telegram.base import TelegramError, get_telegram
from ..ws_hub import hub

router = APIRouter(prefix="/api/accounts", tags=["accounts"])


def _tg_error(exc: TelegramError) -> HTTPException:
    return HTTPException(400, str(exc))


@router.get("")
async def list_accounts(user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    allowed = await allowed_accounts(session, user)
    query = select(TgAccount).order_by(TgAccount.id)
    if allowed is not None:
        query = query.where(TgAccount.id.in_(allowed or {0}))
    accounts = (await session.scalars(query)).all()
    unread = dict(
        (
            await session.execute(
                select(Chat.account_id, func.coalesce(func.sum(Chat.unread_count), 0)).group_by(Chat.account_id)
            )
        ).all()
    )
    tg = get_telegram()
    result = []
    for acc in accounts:
        item = account_dict(acc, tg.is_connected(acc.id))
        item["unread"] = int(unread.get(acc.id, 0))
        result.append(item)
    return {"configured": tg.configured, "accounts": result}


class PhoneIn(BaseModel):
    phone: str


class CodeIn(BaseModel):
    login_id: str
    code: str


class PasswordIn(BaseModel):
    login_id: str
    password: str


@router.post("/login/start", dependencies=[Depends(admin_only)])
async def login_start(data: PhoneIn):
    try:
        return {"login_id": await get_telegram().login_start(data.phone)}
    except TelegramError as exc:
        raise _tg_error(exc) from exc


@router.post("/login/code", dependencies=[Depends(admin_only)])
async def login_code(data: CodeIn):
    try:
        return await get_telegram().login_code(data.login_id, data.code)
    except TelegramError as exc:
        raise _tg_error(exc) from exc


@router.post("/login/password", dependencies=[Depends(admin_only)])
async def login_password(data: PasswordIn):
    try:
        return await get_telegram().login_password(data.login_id, data.password)
    except TelegramError as exc:
        raise _tg_error(exc) from exc


class AccountPatch(BaseModel):
    name: str | None = None
    color: str | None = None
    show_private: bool | None = None
    show_groups: bool | None = None
    show_channels: bool | None = None
    show_bots: bool | None = None


@router.patch("/{account_id}", dependencies=[Depends(admin_only)])
async def update_account(account_id: int, data: AccountPatch, session: AsyncSession = Depends(get_session)):
    acc = await session.get(TgAccount, account_id)
    if acc is None:
        raise HTTPException(404, "Akkaunt topilmadi")
    if data.name is not None and data.name.strip():
        acc.name = data.name.strip()
    if data.color is not None:
        acc.color = data.color
    for field in ("show_private", "show_groups", "show_channels", "show_bots"):
        value = getattr(data, field)
        if value is not None:
            setattr(acc, field, value)
    await session.commit()
    payload = account_dict(acc, get_telegram().is_connected(acc.id))
    await hub.publish({"type": "account.update", "account": payload}, account_id=acc.id)
    return payload


@router.post("/{account_id}/resync", dependencies=[Depends(admin_only)])
async def resync(account_id: int):
    try:
        await get_telegram().resync(account_id)
    except TelegramError as exc:
        raise _tg_error(exc) from exc
    return {"ok": True}


@router.post("/{account_id}/logout", dependencies=[Depends(admin_only)])
async def logout(account_id: int, session: AsyncSession = Depends(get_session)):
    try:
        await get_telegram().logout(account_id)
    except TelegramError as exc:
        raise _tg_error(exc) from exc
    acc = await session.get(TgAccount, account_id)
    if acc is not None:
        await session.refresh(acc)
        await hub.publish({"type": "account.update", "account": account_dict(acc, False)}, account_id=account_id)
    return {"ok": True}


@router.delete("/{account_id}", dependencies=[Depends(admin_only)])
async def delete_account(account_id: int, session: AsyncSession = Depends(get_session)):
    """Akkauntni va uning barcha yozishmalarini saytdan o'chiradi (zakazlar qoladi)."""
    acc = await session.get(TgAccount, account_id)
    if acc is None:
        raise HTTPException(404, "Akkaunt topilmadi")
    tg = get_telegram()
    if acc.status != "logged_out":
        try:
            await tg.logout(account_id)
        except TelegramError:
            await tg.detach(account_id)
    chat_ids = select(Chat.id).where(Chat.account_id == account_id)
    await session.execute(update(Order).where(Order.chat_id.in_(chat_ids)).values(chat_id=None))
    await session.execute(update(Order).where(Order.account_id == account_id).values(account_id=None))
    await session.execute(delete(TgAccount).where(TgAccount.id == account_id))
    await session.commit()
    await hub.publish({"type": "account.deleted", "account_id": account_id})
    return {"ok": True}
