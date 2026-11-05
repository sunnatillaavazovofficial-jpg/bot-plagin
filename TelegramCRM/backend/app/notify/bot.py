"""Eslatma boti (oddiy Telegram bot, BotFather token). Xodimlarga xabar yuboradi."""

from __future__ import annotations

import asyncio
import html
import logging

import httpx
from sqlalchemy import select

from ..db import SessionLocal
from ..models import User
from .finance_bot import FinanceBot

log = logging.getLogger(__name__)

BOT_COMMANDS = [
    {"command": "balans", "description": "Kassa qoldig'i"},
    {"command": "bugun", "description": "Bugungi kirim-chiqim"},
    {"command": "oy", "description": "Shu oy natijasi"},
    {"command": "qarzlar", "description": "Ochiq qarzlar"},
    {"command": "yordam", "description": "Qanday ishlatish"},
]


class NotifyBot:
    def __init__(self) -> None:
        self.token = ""
        self.username: str | None = None
        self.error: str | None = None
        self._task: asyncio.Task | None = None
        self._http: httpx.AsyncClient | None = None
        self.sent: list[tuple[int, str]] = []  # sinov uchun: oxirgi yuborilganlar
        self.calls: list[tuple[str, dict]] = []  # sinov uchun: boshqa Bot API chaqiruvlari
        self.mini_app = False
        self.finance = FinanceBot(self)

    @property
    def enabled(self) -> bool:
        return bool(self.token) and self.username is not None

    def _url(self, method: str) -> str:
        return f"https://api.telegram.org/bot{self.token}/{method}"

    async def start(self, token: str, public_url: str = "") -> None:
        self.token = token
        self.mini_app = False
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
        await self.api("setMyCommands", {"commands": BOT_COMMANDS})
        await self.set_menu_url(public_url)
        self._task = asyncio.create_task(self._poll())

    async def set_menu_url(self, url: str) -> bool:
        """Botdagi «CRM» menyu tugmasi saytni Telegram ichida ochadi (Mini App). Faqat https bilan ishlaydi."""
        if not url.startswith("https://"):
            return False
        self.mini_app = await self.api(
            "setChatMenuButton",
            {"menu_button": {"type": "web_app", "text": "CRM", "web_app": {"url": url}}},
        )
        return self.mini_app

    async def api(self, method: str, payload: dict) -> bool:
        self.calls.append((method, payload))
        self.calls = self.calls[-50:]
        if not self.token or self._http is None:
            return False
        try:
            resp = await self._http.post(self._url(method), json=payload)
            return bool(resp.json().get("ok"))
        except Exception:  # noqa: BLE001
            log.warning("Bot API %s xatosi", method, exc_info=True)
            return False

    async def edit(self, chat_id: int, message_id: int, text: str, reply_markup: dict | None = None) -> bool:
        payload = {"chat_id": chat_id, "message_id": message_id, "text": text, "parse_mode": "HTML"}
        if reply_markup is not None:
            payload["reply_markup"] = reply_markup
        return await self.api("editMessageText", payload)

    async def answer(self, callback_id: str, text: str | None = None) -> bool:
        payload: dict = {"callback_query_id": callback_id}
        if text:
            payload["text"] = text
        return await self.api("answerCallbackQuery", payload)

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
                    self._url("getUpdates"),
                    params={"timeout": 30, "offset": offset, "allowed_updates": '["message","callback_query"]'},
                )
                data = resp.json()
                for update in data.get("result", []):
                    offset = update["update_id"] + 1
                    try:
                        await self.handle_update(update)
                    except Exception:  # noqa: BLE001
                        log.exception("Bot xabarini qayta ishlashda xato")
            except asyncio.CancelledError:
                raise
            except Exception:  # noqa: BLE001
                log.warning("Bot getUpdates xatosi", exc_info=True)
                await asyncio.sleep(5)

    async def handle_update(self, update: dict) -> None:
        if "callback_query" in update:
            await self.finance.handle_callback(update["callback_query"])
        elif "message" in update:
            await self._handle(update["message"])

    async def _handle(self, message: dict) -> None:
        text = (message.get("text") or "").strip()
        chat = message.get("chat") or {}
        sender = message.get("from") or {}
        if chat.get("type") != "private" or not text:
            return
        if not text.startswith("/start"):
            if not await self.finance.handle_text(chat["id"], sender.get("id"), text):
                await self.send(chat["id"], "Buyruqni tushunmadim. /yordam — qanday ishlatish.")
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
        await self.send(
            chat["id"],
            f"✅ {html.escape(name)}, bot ulandi. Endi eslatmalar shu yerga keladi.\n\n"
            "Admin va menejerlar kassaga shu yerdan yozishi mumkin: <code>+500000 avans</code>, "
            "<code>-80000 material</code>. Batafsil: /yordam",
        )

    async def send(self, tg_user_id: int, text: str, reply_markup: dict | None = None) -> bool:
        self.sent.append((tg_user_id, text))
        self.sent = self.sent[-50:]
        if not self.token or self._http is None:
            return False
        payload = {"chat_id": tg_user_id, "text": text, "parse_mode": "HTML", "disable_web_page_preview": True}
        if reply_markup is not None:
            payload["reply_markup"] = reply_markup
        try:
            resp = await self._http.post(self._url("sendMessage"), json=payload)
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
