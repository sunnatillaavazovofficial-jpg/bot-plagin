"""Saytdan o'zgartiriladigan sozlamalar (bazadagi settings jadvali)."""

from __future__ import annotations

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from .models import Setting

DEFAULTS: dict[str, str] = {
    "company_name": "TelegramCRM",
    "unanswered_minutes": "30",
    "daily_time": "09:00",
    "notify_deadline": "1",
    "notify_unanswered": "1",
    "notify_daily": "1",
    "notify_assign": "1",
}


async def get_all(session: AsyncSession) -> dict[str, str]:
    rows = (await session.scalars(select(Setting))).all()
    data = dict(DEFAULTS)
    data.update({row.key: row.value for row in rows if row.key in DEFAULTS})
    return data


async def set_many(session: AsyncSession, values: dict[str, str]) -> None:
    for key, value in values.items():
        if key not in DEFAULTS:
            continue
        row = await session.get(Setting, key)
        if row is None:
            session.add(Setting(key=key, value=str(value)))
        else:
            row.value = str(value)


def as_bool(value: str) -> bool:
    return value in {"1", "true", "True"}
