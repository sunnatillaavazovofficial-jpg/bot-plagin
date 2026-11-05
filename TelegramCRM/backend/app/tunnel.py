"""Kompyuter server bo'lganda: Cloudflare tunnel bergan https manzilni kuzatish.

Bepul tunnel (trycloudflare.com) har safar ishga tushganda yangi manzil oladi. Ilova uni tunnelning
`/quicktunnel` manzilidan o'qiydi, botdagi «CRM» tugmasini yangilaydi va xodimlarga yangi manzilni yuboradi.
"""

from __future__ import annotations

import asyncio
import logging
import re

import httpx
from sqlalchemy import select

from .config import get_settings
from .db import SessionLocal
from .models import Setting, User
from .notify.bot import notify_bot

log = logging.getLogger(__name__)

STATE_KEY = "tunnel_url"  # oxirgi xabar qilingan manzil (ilova qayta yonganda takror yubormaslik uchun)
_HOST_RE = re.compile(r"^[a-z0-9-]+(\.[a-z0-9-]+)+$")
_runtime_url = ""


def public_url() -> str:
    """Saytning tashqi manzili: tunnel manzili, bo'lmasa .env dagi PUBLIC_URL."""
    return _runtime_url or get_settings().public_url


def reset() -> None:
    global _runtime_url
    _runtime_url = ""


async def read_hostname(http: httpx.AsyncClient, metrics_url: str) -> str:
    try:
        resp = await http.get(f"{metrics_url}/quicktunnel")
        host = str(resp.json().get("hostname") or "").strip().lower()
    except Exception:  # noqa: BLE001 — tunnel hali tayyor emas
        return ""
    return host if _HOST_RE.match(host) else ""


def _message(url: str) -> tuple[str, dict]:
    text = (
        "🌐 <b>CRM yangi manzilda</b>\n"
        f"{url}\n\n"
        "Kompyuter (server) qayta yoqilgani uchun manzil o'zgardi. Botdagi «CRM» tugmasi ham yangilandi."
    )
    markup = {"inline_keyboard": [[{"text": "CRM'ni ochish", "web_app": {"url": url}}]]}
    return text, markup


async def check_once(http: httpx.AsyncClient, metrics_url: str) -> str | None:
    """Tunnel manzilini tekshiradi. Manzil o'zgargan bo'lsa, uni qaytaradi."""
    global _runtime_url
    host = await read_hostname(http, metrics_url)
    if not host:
        return None
    url = f"https://{host}"
    if url == _runtime_url:
        return None
    _runtime_url = url
    log.info("Sayt manzili: %s", url)
    await notify_bot.set_menu_url(url)
    async with SessionLocal() as s:
        row = await s.get(Setting, STATE_KEY)
        if row is not None and row.value == url:
            return url
        if row is None:
            s.add(Setting(key=STATE_KEY, value=url))
        else:
            row.value = url
        users = (
            await s.scalars(select(User).where(User.is_active.is_(True), User.tg_user_id.is_not(None)))
        ).all()
        await s.commit()
    text, markup = _message(url)
    for user in users:
        await notify_bot.send(user.tg_user_id, text, reply_markup=markup)
    return url


async def watch(metrics_url: str, interval: float = 15.0) -> None:
    async with httpx.AsyncClient(timeout=5.0) as http:
        while True:
            try:
                await check_once(http, metrics_url)
            except asyncio.CancelledError:
                raise
            except Exception:  # noqa: BLE001
                log.warning("Tunnel manzilini tekshirishda xato", exc_info=True)
            await asyncio.sleep(interval)
