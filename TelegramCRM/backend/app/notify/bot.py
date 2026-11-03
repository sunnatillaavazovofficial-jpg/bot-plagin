"""Eslatma boti (oddiy Telegram bot, BotFather token). Xodimlarga xabar yuboradi."""

from __future__ import annotations

import asyncio
import html
import logging

import httpx
from sqlalchemy import select

from ..db import SessionLocal
from ..models import User

log = logging.getLogger(__name__)


class NotifyBot:
    def __init__(self) -> None:
        self.token = ""
        self.username: str | None = None
        self.error: str | None = None
        self._task: asyncio.Task | None = None
        self._http: httpx.AsyncClient | None = None
        self.sent: list[tuple[int, str]] = []  # sinov uchun: oxirgi yuborilganlar

    @property
    def enabled(self) -> bool:
        return bool(self.token) and self.username is not None

    def _url(self, method: str) -> str:
        return f"https://api.telegram.org/bot{self.token}/{method}"

    async def start(self, token: str) -> None:
        self.token = token
        if not token:
            return
        self._http = httpx.AsyncClient(timeout=httpx.Timeout(40.0))
        try:
            resp = await self._http.get(self._url("getMe"))
            data = resp.json()
            if not data.get("ok"):
                raise RuntimeError(data.get("description", "getMe xatosi"))
            self.username = data["result"]["username"]
        except Exception as exc:  # noqa: BLE001
            self.error = f"Bot ishga tushmadi: {exc}"
            log.warning(self.error)
            return
        self._task = asyncio.create_task(self._poll())

    async def stop(self) -> None:
        if self._task:
            self._task.cancel()
        if self._http:
            await self._http.aclose()

    async def _poll(self) -> None:
        assert self._http is not None
        offset = 0
        while True:
            try:
                resp = await self._http.get(
                    self._url("getUpdates"), params={"timeout": 30, "offset": offset, "allowed_updates": '["message"]'}
                )
                data = resp.json()
                for update in data.get("result", []):
                    offset = update["update_id"] + 1
                    await self._handle(update.get("message") or {})
            except asyncio.CancelledError:
                raise
            except Exception:  # noqa: BLE001
                log.warning("Bot getUpdates xatosi", exc_info=True)
                await asyncio.sleep(5)

    async def _handle(self, message: dict) -> None:
        text = (message.get("text") or "").strip()
        chat = message.get("chat") or {}
        sender = message.get("from") or {}
        if chat.get("type") != "private" or not text.startswith("/start"):
            return
        parts = text.split(maxsplit=1)
        code = parts[1].strip() if len(parts) > 1 else ""
        async with SessionLocal() as s:
            user = await s.scalar(select(User).where(User.link_code == code)) if code else None
            if user is None:
                await self.send(chat["id"], "Salom! Bog'lanish uchun saytdagi <b>Profil → Botni ulash</b> tugmasidan foydalaning.")
                return
            # Bitta Telegram faqat bitta xodimga bog'lanadi
            for other in (await s.scalars(select(User).where(User.tg_user_id == sender["id"]))).all():
                other.tg_user_id = None
            user.tg_user_id = sender["id"]
            user.tg_username = sender.get("username")
            user.link_code = None
            await s.commit()
            name = user.name
        await self.send(chat["id"], f"✅ {html.escape(name)}, bot ulandi. Endi eslatmalar shu yerga keladi.")

    async def send(self, tg_user_id: int, text: str) -> bool:
        self.sent.append((tg_user_id, text))
        self.sent = self.sent[-50:]
        if not self.token or self._http is None:
            return False
        try:
            resp = await self._http.post(
                self._url("sendMessage"),
                json={"chat_id": tg_user_id, "text": text, "parse_mode": "HTML", "disable_web_page_preview": True},
            )
            return bool(resp.json().get("ok"))
        except Exception:  # noqa: BLE001
            log.warning("Bot xabar yubora olmadi", exc_info=True)
            return False


notify_bot = NotifyBot()


async def notify_users(users: list[User], text: str) -> int:
    count = 0
    for user in users:
        if user.tg_user_id and user.is_active:
            count += int(await notify_bot.send(user.tg_user_id, text))
    return count
