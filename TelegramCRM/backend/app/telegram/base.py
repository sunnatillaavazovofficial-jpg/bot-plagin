"""Telegram bilan ishlash interfeysi. Haqiqiy (Telethon) va sinov (fake) rejimlari shu interfeysni bajaradi."""

from __future__ import annotations

from pathlib import Path

from ..models import Chat, Message

ACCOUNT_COLORS = [
    "#2563eb",
    "#16a34a",
    "#db2777",
    "#ea580c",
    "#7c3aed",
    "#0891b2",
    "#ca8a04",
    "#dc2626",
]


class TelegramError(Exception):
    """Foydalanuvchiga ko'rsatiladigan xato matni (o'zbekcha)."""


class BaseTelegram:
    configured = True

    async def start(self) -> None: ...

    async def stop(self) -> None: ...

    def is_connected(self, account_id: int) -> bool:
        return False

    async def login_start(self, phone: str) -> str:
        raise NotImplementedError

    async def login_code(self, login_id: str, code: str) -> dict:
        raise NotImplementedError

    async def login_password(self, login_id: str, password: str) -> dict:
        raise NotImplementedError

    async def logout(self, account_id: int) -> None:
        raise NotImplementedError

    async def detach(self, account_id: int) -> None:
        """Akkauntni to'xtatadi (Telegram'dan chiqmasdan)."""

    async def resync(self, account_id: int) -> None:
        raise NotImplementedError

    async def send_message(
        self,
        chat: Chat,
        text: str,
        *,
        user_id: int,
        reply_to: int | None = None,
        file_path: Path | None = None,
        as_document: bool = False,
    ) -> Message:
        raise NotImplementedError

    async def mark_read(self, chat: Chat) -> None:
        raise NotImplementedError

    async def download_media(self, message: Message) -> Path:
        raise NotImplementedError

    async def load_older(self, chat_id: int, limit: int = 50) -> int:
        return 0

    async def show_chat(self, chat_id: int) -> None:
        """Yashirin chat ko'rinadigan bo'lganda o'tkazib yuborilgan xabarlarni yuklaydi."""

    async def click_button(self, message: Message, row: int, col: int) -> str | None:
        """Bot xabaridagi tugmani bosish. Bot javobi (bo'lsa) qaytariladi."""
        raise NotImplementedError


class NotConfiguredTelegram(BaseTelegram):
    configured = False
    _msg = "Telegram API sozlanmagan: .env faylida TG_API_ID va TG_API_HASH ni yozing."

    async def login_start(self, phone: str) -> str:
        raise TelegramError(self._msg)

    async def login_code(self, login_id: str, code: str) -> dict:
        raise TelegramError(self._msg)

    async def login_password(self, login_id: str, password: str) -> dict:
        raise TelegramError(self._msg)

    async def logout(self, account_id: int) -> None:
        raise TelegramError(self._msg)

    async def resync(self, account_id: int) -> None:
        raise TelegramError(self._msg)

    async def send_message(self, chat, text, **kwargs):
        raise TelegramError(self._msg)

    async def mark_read(self, chat: Chat) -> None:
        return None

    async def download_media(self, message: Message) -> Path:
        raise TelegramError(self._msg)

    async def click_button(self, message: Message, row: int, col: int) -> str | None:
        raise TelegramError(self._msg)


_instance: BaseTelegram = NotConfiguredTelegram()


def get_telegram() -> BaseTelegram:
    return _instance


def set_telegram(instance: BaseTelegram) -> None:
    global _instance
    _instance = instance


def media_dir_for(base: Path, account_id: int, chat_id: int) -> Path:
    path = base / str(account_id) / str(chat_id)
    path.mkdir(parents=True, exist_ok=True)
    return path
