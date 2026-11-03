"""Kirish va ruxsatlarni tekshirish."""

from __future__ import annotations

from fastapi import Depends, HTTPException, Request, status
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from .db import get_session
from .models import AccountAccess, Chat, Order, User
from .security import TOKEN_COOKIE, decode_token


async def user_from_token(session: AsyncSession, token: str | None) -> User | None:
    if not token:
        return None
    user_id = decode_token(token)
    if user_id is None:
        return None
    user = await session.get(User, user_id)
    if user is None or not user.is_active:
        return None
    return user


async def current_user(request: Request, session: AsyncSession = Depends(get_session)) -> User:
    user = await user_from_token(session, request.cookies.get(TOKEN_COOKIE))
    if user is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Tizimga kiring")
    return user


def require_roles(*roles: str):
    async def dep(user: User = Depends(current_user)) -> User:
        if user.role not in roles:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "Bu bo'lim uchun ruxsat yo'q")
        return user

    return dep


admin_only = require_roles("admin")
staff_manager = require_roles("admin", "menejer")


async def allowed_accounts(session: AsyncSession, user: User) -> set[int] | None:
    """None — barcha akkauntlar (admin)."""
    if user.role == "admin":
        return None
    rows = await session.scalars(select(AccountAccess.account_id).where(AccountAccess.user_id == user.id))
    return set(rows.all())


def can_see_account(allowed: set[int] | None, account_id: int | None) -> bool:
    return allowed is None or (account_id is not None and account_id in allowed)


async def get_chat_checked(session: AsyncSession, user: User, chat_id: int) -> Chat:
    chat = await session.get(Chat, chat_id)
    if chat is None:
        raise HTTPException(404, "Chat topilmadi")
    if not can_see_account(await allowed_accounts(session, user), chat.account_id):
        raise HTTPException(403, "Bu akkauntga ruxsat yo'q")
    return chat


async def get_order_checked(session: AsyncSession, user: User, order_id: int) -> Order:
    order = await session.get(Order, order_id)
    if order is None:
        raise HTTPException(404, "Zakaz topilmadi")
    if user.role == "xodim" and order.assignee_id != user.id:
        raise HTTPException(403, "Bu zakaz sizga biriktirilmagan")
    return order
