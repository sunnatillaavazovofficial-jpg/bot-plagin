"""Baza tuzilmasini yangilash (Alembic). Har ishga tushganda avtomatik bajariladi."""

from __future__ import annotations

from pathlib import Path

from alembic import command
from alembic.config import Config

from .config import get_settings
from .db import sync_url

ALEMBIC_DIR = Path(__file__).resolve().parent.parent / "alembic"


def alembic_config() -> Config:
    cfg = Config()
    cfg.set_main_option("script_location", str(ALEMBIC_DIR))
    cfg.set_main_option("sqlalchemy.url", sync_url())
    return cfg


def upgrade_database() -> None:
    get_settings().data_dir.mkdir(parents=True, exist_ok=True)
    command.upgrade(alembic_config(), "head")
