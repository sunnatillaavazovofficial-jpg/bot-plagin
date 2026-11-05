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
# Ochiq bosh sahifa (Google/Yandex) — app/site.py; bo'sh bo'lsa sayt qidiruvdan yopiq
for _key in (
    "site_name", "site_title_uz", "site_title_ru", "site_tagline_uz", "site_tagline_ru", "site_about_uz", "site_about_ru",
    "site_services_uz", "site_services_ru", "site_facts_uz", "site_facts_ru", "site_phone",
    "site_address_uz", "site_address_ru", "site_hours_uz", "site_hours_ru",
    "site_telegram", "site_instagram", "site_website", "google_verification", "yandex_verification",
):
    DEFAULTS[_key] = ""


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
