"""Har daqiqada ishlaydigan fon vazifalari: eslatmalar va kunlik zaxira nusxa."""

from __future__ import annotations

import asyncio
import logging
import sqlite3
from datetime import timedelta

from sqlalchemy import delete

from ..config import get_settings
from ..db import SessionLocal
from ..models import NotifyLog, utcnow
from ..settings_store import get_all
from ..timeutil import local_now
from .events import check_deadlines, check_unanswered, daily_summary

log = logging.getLogger(__name__)

BACKUP_HOUR = 3
BACKUP_KEEP = 14


def backup_database() -> str:
    settings = get_settings()
    settings.backup_dir.mkdir(parents=True, exist_ok=True)
    target = settings.backup_dir / f"crm-{local_now().strftime('%Y%m%d')}.db"
    src = sqlite3.connect(settings.db_path)
    dst = sqlite3.connect(target)
    try:
        src.backup(dst)
    finally:
        dst.close()
        src.close()
    for old in sorted(settings.backup_dir.glob("crm-*.db"))[:-BACKUP_KEEP]:
        old.unlink(missing_ok=True)
    return str(target)


async def tick() -> None:
    now = local_now()
    today = now.date()
    async with SessionLocal() as s:
        settings = await get_all(s)
    await check_unanswered()
    if now.strftime("%H:%M") >= (settings["daily_time"] or "09:00"):
        await check_deadlines(today)
        await daily_summary(today)
    if now.hour >= BACKUP_HOUR:
        async with SessionLocal() as s:
            key = f"backup:{today.isoformat()}"
            if await s.get(NotifyLog, key) is None:
                s.add(NotifyLog(key=key))
                await s.execute(delete(NotifyLog).where(NotifyLog.sent_at < utcnow() - timedelta(days=30)))
                await s.commit()
                path = await asyncio.to_thread(backup_database)
                log.info("Zaxira nusxa: %s", path)


async def run_forever() -> None:
    await asyncio.sleep(10)
    while True:
        try:
            await tick()
        except asyncio.CancelledError:
            raise
        except Exception:  # noqa: BLE001
            log.exception("Fon vazifasida xato")
        await asyncio.sleep(60)
