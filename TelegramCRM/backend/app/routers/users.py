from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy import delete, select
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import admin_only, current_user
from ..models import ROLES, AccountAccess, User
from ..notify.bot import notify_bot
from ..security import hash_password, random_code
from ..serializers import user_dict
from ..ws_hub import hub

router = APIRouter(prefix="/api/users", tags=["users"])


async def _account_ids(session: AsyncSession, user_id: int) -> list[int]:
    rows = await session.scalars(select(AccountAccess.account_id).where(AccountAccess.user_id == user_id))
    return sorted(rows.all())


@router.get("")
async def list_users(user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    users = (await session.scalars(select(User).order_by(User.is_active.desc(), User.name))).all()
    if user.role != "admin":
        # Boshqalar uchun faqat ism (zakaz biriktirish va ko'rsatish uchun)
        return [{"id": u.id, "name": u.name, "role": u.role, "is_active": u.is_active} for u in users]
    return [user_dict(u, await _account_ids(session, u.id)) for u in users]


class UserIn(BaseModel):
    name: str
    login: str
    password: str
    role: str = "xodim"
    account_ids: list[int] = []


class UserPatch(BaseModel):
    name: str | None = None
    login: str | None = None
    password: str | None = None
    role: str | None = None
    is_active: bool | None = None
    account_ids: list[int] | None = None


async def _set_access(session: AsyncSession, user_id: int, account_ids: list[int]) -> None:
    await session.execute(delete(AccountAccess).where(AccountAccess.user_id == user_id))
    for account_id in set(account_ids):
        session.add(AccountAccess(user_id=user_id, account_id=account_id))


def _check_role(role: str) -> None:
    if role not in ROLES:
        raise HTTPException(400, "Noto'g'ri rol")


@router.post("", dependencies=[Depends(admin_only)])
async def create_user(data: UserIn, session: AsyncSession = Depends(get_session)):
    _check_role(data.role)
    login = data.login.strip().lower()
    if not login or len(data.password) < 6:
        raise HTTPException(400, "Login kiriting, parol kamida 6 belgi bo'lsin")
    if await session.scalar(select(User).where(User.login == login)):
        raise HTTPException(400, "Bu login band")
    user = User(name=data.name.strip() or login, login=login, password_hash=hash_password(data.password), role=data.role)
    session.add(user)
    await session.flush()
    await _set_access(session, user.id, data.account_ids)
    await session.commit()
    return user_dict(user, await _account_ids(session, user.id))


@router.patch("/{user_id}")
async def update_user(
    user_id: int, data: UserPatch, admin: User = Depends(admin_only), session: AsyncSession = Depends(get_session)
):
    user = await session.get(User, user_id)
    if user is None:
        raise HTTPException(404, "Xodim topilmadi")
    if user.id == admin.id and (data.role not in (None, "admin") or data.is_active is False):
        raise HTTPException(400, "O'zingizni admin rolidan chiqara olmaysiz")
    if data.name is not None:
        user.name = data.name.strip() or user.name
    if data.login is not None:
        login = data.login.strip().lower()
        if login != user.login and await session.scalar(select(User).where(User.login == login)):
            raise HTTPException(400, "Bu login band")
        user.login = login
    if data.password:
        if len(data.password) < 6:
            raise HTTPException(400, "Parol kamida 6 belgi bo'lsin")
        user.password_hash = hash_password(data.password)
    if data.role is not None:
        _check_role(data.role)
        user.role = data.role
    if data.is_active is not None:
        user.is_active = data.is_active
    if data.account_ids is not None:
        await _set_access(session, user.id, data.account_ids)
    await session.commit()
    ids = await _account_ids(session, user.id)
    if not user.is_active:
        hub.drop_user(user.id)
    else:
        hub.update_access(user.id, user.role, None if user.role == "admin" else set(ids))
    return user_dict(user, ids)


@router.post("/me/link-code")
async def link_code(user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    if not notify_bot.enabled:
        raise HTTPException(400, "Eslatma boti sozlanmagan (.env da BOT_TOKEN yo'q).")
    db_user = await session.get(User, user.id)
    assert db_user is not None
    db_user.link_code = random_code(12)
    await session.commit()
    return {"url": f"https://t.me/{notify_bot.username}?start={db_user.link_code}"}


@router.post("/me/unlink")
async def unlink(user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    db_user = await session.get(User, user.id)
    assert db_user is not None
    db_user.tg_user_id = None
    db_user.tg_username = None
    await session.commit()
    return {"ok": True}
