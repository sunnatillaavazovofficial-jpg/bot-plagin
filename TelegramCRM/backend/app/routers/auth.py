from __future__ import annotations

import time
from collections import defaultdict

from fastapi import APIRouter, Depends, HTTPException, Request, Response
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from ..config import get_settings
from ..db import get_session
from ..deps import allowed_accounts, current_user
from ..models import User
from ..notify.bot import notify_bot
from ..security import TOKEN_COOKIE, TOKEN_DAYS, create_token, hash_password, verify_password, verify_telegram_init_data
from ..serializers import user_dict

router = APIRouter(prefix="/api/auth", tags=["auth"])

MAX_FAILS = 10
WINDOW = 15 * 60
_fails: dict[str, list[float]] = defaultdict(list)


def _client_ip(request: Request) -> str:
    forwarded = request.headers.get("x-forwarded-for")
    if forwarded:
        return forwarded.split(",")[0].strip()
    return request.client.host if request.client else "?"


class LoginIn(BaseModel):
    login: str
    password: str


@router.post("/login")
async def login(data: LoginIn, request: Request, response: Response, session: AsyncSession = Depends(get_session)):
    ip = _client_ip(request)
    now = time.monotonic()
    _fails[ip] = [t for t in _fails[ip] if now - t < WINDOW]
    if len(_fails[ip]) >= MAX_FAILS:
        raise HTTPException(429, "Juda ko'p noto'g'ri urinish. 15 daqiqadan keyin qayta urinib ko'ring.")
    user = await session.scalar(select(User).where(User.login == data.login.strip().lower()))
    if user is None or not user.is_active or not verify_password(data.password, user.password_hash):
        _fails[ip].append(now)
        raise HTTPException(401, "Login yoki parol noto'g'ri")
    _fails.pop(ip, None)
    _set_cookie(response, user.id, request)
    return {"ok": True}


def _set_cookie(response: Response, user_id: int, request: Request) -> None:
    # COOKIE_SECURE=1 bo'lsa doim, aks holda sayt https orqali ochilganda (masalan tunnel) Secure bo'ladi
    secure = get_settings().cookie_secure or request.url.scheme == "https"
    response.set_cookie(
        TOKEN_COOKIE,
        create_token(user_id),
        max_age=TOKEN_DAYS * 86400,
        httponly=True,
        samesite="lax",
        secure=secure,
    )


class TelegramIn(BaseModel):
    init_data: str


@router.post("/telegram")
async def login_telegram(
    data: TelegramIn, request: Request, response: Response, session: AsyncSession = Depends(get_session)
):
    """Sayt Telegram ichida (Mini App) ochilganda: botga bog'langan xodim avtomatik kiradi."""
    ip = _client_ip(request)
    now = time.monotonic()
    _fails[ip] = [t for t in _fails[ip] if now - t < WINDOW]
    if len(_fails[ip]) >= MAX_FAILS:
        raise HTTPException(429, "Juda ko'p urinish. Keyinroq qayta urinib ko'ring.")
    tg_user = verify_telegram_init_data(data.init_data, get_settings().bot_token)
    if tg_user is None:
        _fails[ip].append(now)
        raise HTTPException(401, "Telegram ma'lumoti tasdiqlanmadi")
    user = await session.scalar(select(User).where(User.tg_user_id == int(tg_user["id"]), User.is_active.is_(True)))
    if user is None:
        raise HTTPException(
            403, "Bu Telegram akkaunt hech bir xodimga bog'lanmagan. Saytga login bilan kirib, Profil → Botni ulash qiling."
        )
    _set_cookie(response, user.id, request)
    return {"ok": True}


@router.post("/logout")
async def logout(response: Response):
    response.delete_cookie(TOKEN_COOKIE)
    return {"ok": True}


@router.get("/me")
async def me(user: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    allowed = await allowed_accounts(session, user)
    data = user_dict(user, sorted(allowed) if allowed is not None else None)
    data["bot_username"] = notify_bot.username
    return data


class PasswordIn(BaseModel):
    old_password: str
    new_password: str


@router.post("/password")
async def change_password(
    data: PasswordIn, user: User = Depends(current_user), session: AsyncSession = Depends(get_session)
):
    if not verify_password(data.old_password, user.password_hash):
        raise HTTPException(400, "Eski parol noto'g'ri")
    if len(data.new_password) < 6:
        raise HTTPException(400, "Yangi parol kamida 6 belgidan iborat bo'lsin")
    db_user = await session.get(User, user.id)
    assert db_user is not None
    db_user.password_hash = hash_password(data.new_password)
    await session.commit()
    return {"ok": True}
