"""Mahalliy vaqt (TIMEZONE, odatda Asia/Tashkent)."""

from __future__ import annotations

from datetime import date, datetime
from zoneinfo import ZoneInfo

from .config import get_settings


def tz() -> ZoneInfo:
    return ZoneInfo(get_settings().timezone)


def local_now() -> datetime:
    return datetime.now(tz())


def local_today() -> date:
    return local_now().date()
