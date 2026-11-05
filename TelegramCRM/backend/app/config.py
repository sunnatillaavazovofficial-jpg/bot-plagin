"""Sozlamalar: hammasi muhit o'zgaruvchilaridan (.env) o'qiladi."""

from __future__ import annotations

import os
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path


def _bool(value: str | None, default: bool = False) -> bool:
    if value is None or value == "":
        return default
    return value.strip().lower() in {"1", "true", "yes", "ha", "on"}


@dataclass(frozen=True)
class Settings:
    data_dir: Path
    secret_key: str
    tg_api_id: int
    tg_api_hash: str
    tg_fake: bool
    fake_interval: int
    demo_finance: bool
    bot_token: str
    admin_login: str
    admin_password: str
    timezone: str
    cookie_secure: bool
    static_dir: Path
    public_url: str

    @property
    def db_path(self) -> Path:
        return self.data_dir / "crm.db"

    @property
    def media_dir(self) -> Path:
        return self.data_dir / "media"

    @property
    def backup_dir(self) -> Path:
        return self.data_dir / "backups"

    @property
    def telegram_configured(self) -> bool:
        return self.tg_fake or (self.tg_api_id > 0 and bool(self.tg_api_hash))


@lru_cache
def get_settings() -> Settings:
    data_dir = Path(os.getenv("DATA_DIR", "./data")).resolve()
    secret = os.getenv("SECRET_KEY", "")
    if not secret:
        if _bool(os.getenv("TG_FAKE")):
            secret = "demo-secret-key-faqat-sinov-uchun"
        else:
            raise RuntimeError(
                "SECRET_KEY o'rnatilmagan. .env faylida uzun tasodifiy SECRET_KEY yozing."
            )
    default_static = Path(__file__).resolve().parent / "static"
    return Settings(
        data_dir=data_dir,
        secret_key=secret,
        tg_api_id=int(os.getenv("TG_API_ID", "0") or 0),
        tg_api_hash=os.getenv("TG_API_HASH", ""),
        tg_fake=_bool(os.getenv("TG_FAKE")),
        fake_interval=int(os.getenv("FAKE_INTERVAL", "45") or 45),
        demo_finance=_bool(os.getenv("DEMO_FINANCE"), default=True),
        bot_token=os.getenv("BOT_TOKEN", ""),
        admin_login=os.getenv("ADMIN_LOGIN", "admin"),
        admin_password=os.getenv("ADMIN_PASSWORD", ""),
        timezone=os.getenv("TIMEZONE", "Asia/Tashkent"),
        cookie_secure=_bool(os.getenv("COOKIE_SECURE"), default=False),
        static_dir=Path(os.getenv("STATIC_DIR", str(default_static))),
        public_url=os.getenv("PUBLIC_URL", "").strip().rstrip("/"),
    )
