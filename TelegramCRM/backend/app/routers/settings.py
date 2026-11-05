from __future__ import annotations

import re

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import admin_only, current_user
from ..models import User
from ..notify.bot import notify_bot
from ..notify.scheduler import backup_database
from ..settings_store import DEFAULTS, get_all, set_many
from ..site import clean_verification
from ..telegram.base import get_telegram
from ..tunnel import public_url

router = APIRouter(prefix="/api/settings", tags=["settings"])


@router.get("")
async def read_settings(_: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    data = await get_all(session)
    tg = get_telegram()
    return {
        "values": data,
        "bot": {
            "enabled": notify_bot.enabled,
            "username": notify_bot.username,
            "error": notify_bot.error,
            "mini_app": notify_bot.mini_app,
        },
        "telegram_configured": tg.configured,
        "public_url": public_url(),
        "fake_mode": type(tg).__name__ == "FakeTelegram",
    }


@router.put("", dependencies=[Depends(admin_only)])
async def save_settings(values: dict[str, str], session: AsyncSession = Depends(get_session)):
    clean = {k: str(v)[:4000] for k, v in values.items() if k in DEFAULTS}
    for key in ("google_verification", "yandex_verification"):
        if key in clean:
            clean[key] = clean_verification(clean[key])
    for key in ("site_telegram", "site_instagram", "site_website"):
        value = clean.get(key, "").strip()
        if value.startswith("@"):
            value = ("https://t.me/" if key == "site_telegram" else "https://instagram.com/") + value[1:]
        if value and not value.startswith(("https://", "http://")):
            raise HTTPException(400, "Havola https:// bilan boshlanishi kerak (yoki @username)")
        if key in clean:
            clean[key] = value
    if "daily_time" in clean and not re.fullmatch(r"([01]\d|2[0-3]):[0-5]\d", clean["daily_time"]):
        raise HTTPException(400, "Vaqt formati: 09:00")
    if "unanswered_minutes" in clean:
        try:
            minutes = int(clean["unanswered_minutes"])
        except ValueError as exc:
            raise HTTPException(400, "Daqiqani son bilan kiriting") from exc
        if not 5 <= minutes <= 1440:
            raise HTTPException(400, "Daqiqa 5 dan 1440 gacha bo'lsin")
    await set_many(session, clean)
    await session.commit()
    return await get_all(session)


@router.post("/backup", dependencies=[Depends(admin_only)])
async def make_backup():
    import asyncio

    path = await asyncio.to_thread(backup_database)
    return {"ok": True, "file": path.rsplit("/", 1)[-1]}
