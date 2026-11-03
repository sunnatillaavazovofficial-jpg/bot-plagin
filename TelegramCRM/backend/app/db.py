"""Ma'lumotlar bazasi (SQLite) ulanishi."""

from __future__ import annotations

from collections.abc import AsyncIterator

from sqlalchemy import event
from sqlalchemy.ext.asyncio import AsyncEngine, AsyncSession, async_sessionmaker, create_async_engine

from .config import get_settings

_engine: AsyncEngine | None = None
_sessionmaker: async_sessionmaker[AsyncSession] | None = None


def _set_pragmas(dbapi_conn, _record) -> None:  # pragma: no cover - trivial
    cur = dbapi_conn.cursor()
    cur.execute("PRAGMA journal_mode=WAL")
    cur.execute("PRAGMA foreign_keys=ON")
    cur.execute("PRAGMA busy_timeout=5000")
    cur.close()


def sync_url() -> str:
    return f"sqlite:///{get_settings().db_path}"


def init_engine() -> AsyncEngine:
    global _engine, _sessionmaker
    settings = get_settings()
    settings.data_dir.mkdir(parents=True, exist_ok=True)
    _engine = create_async_engine(f"sqlite+aiosqlite:///{settings.db_path}")
    event.listen(_engine.sync_engine, "connect", _set_pragmas)
    _sessionmaker = async_sessionmaker(_engine, expire_on_commit=False)
    return _engine


async def dispose_engine() -> None:
    global _engine, _sessionmaker
    if _engine is not None:
        await _engine.dispose()
    _engine = None
    _sessionmaker = None


def SessionLocal() -> AsyncSession:  # noqa: N802 - factory used like a class
    if _sessionmaker is None:
        init_engine()
    assert _sessionmaker is not None
    return _sessionmaker()


async def get_session() -> AsyncIterator[AsyncSession]:
    async with SessionLocal() as session:
        yield session
