"""TelegramCRM — ilovaning kirish nuqtasi."""

from __future__ import annotations

import asyncio
import logging
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import FileResponse, JSONResponse
from fastapi.staticfiles import StaticFiles
from sqlalchemy import func, select

from .config import get_settings
from .db import SessionLocal, dispose_engine, init_engine
from .migrate import upgrade_database
from .models import OrderStatus, User
from .notify.bot import notify_bot
from .notify.scheduler import run_forever
from .routers import accounts, auth, chats, clients, orders, reports, settings, users, ws
from .security import hash_password
from .telegram.base import NotConfiguredTelegram, get_telegram, set_telegram

log = logging.getLogger("telegramcrm")

DEFAULT_STATUSES = [
    ("Yangi", "#3b82f6", False, False),
    ("Dizayn", "#8b5cf6", False, False),
    ("Tasdiqlash", "#f59e0b", False, False),
    ("Ishlab chiqarish", "#f97316", False, False),
    ("O'rnatish", "#06b6d4", False, False),
    ("Tayyor", "#10b981", False, False),
    ("Topshirildi", "#16a34a", True, False),
    ("Bekor qilindi", "#94a3b8", False, True),
]


async def seed() -> None:
    cfg = get_settings()
    async with SessionLocal() as s:
        if not await s.scalar(select(func.count(OrderStatus.id))):
            for pos, (name, color, done, cancel) in enumerate(DEFAULT_STATUSES):
                s.add(OrderStatus(name=name, color=color, position=pos, is_done=done, is_cancel=cancel))
        if not await s.scalar(select(func.count(User.id))):
            password = cfg.admin_password or ("admin123" if cfg.tg_fake else "")
            if not password:
                raise RuntimeError("Birinchi ishga tushirish uchun .env da ADMIN_PASSWORD ni yozing.")
            s.add(User(name="Administrator", login=cfg.admin_login.lower(), password_hash=hash_password(password), role="admin"))
            log.info("Admin yaratildi: %s", cfg.admin_login)
        await s.commit()


def make_telegram():
    cfg = get_settings()
    if cfg.tg_fake:
        from .telegram.fake import FakeTelegram

        return FakeTelegram(cfg)
    if cfg.telegram_configured:
        from .telegram.manager import TelegramManager

        return TelegramManager(cfg)
    log.warning("TG_API_ID/TG_API_HASH berilmagan — Telegram akkauntlarni ulab bo'lmaydi")
    return NotConfiguredTelegram()


@asynccontextmanager
async def lifespan(app: FastAPI):
    cfg = get_settings()
    cfg.data_dir.mkdir(parents=True, exist_ok=True)
    cfg.media_dir.mkdir(parents=True, exist_ok=True)
    await asyncio.to_thread(upgrade_database)
    init_engine()
    await seed()
    tg = make_telegram()
    set_telegram(tg)
    await tg.start()
    await notify_bot.start(cfg.bot_token)
    scheduler = asyncio.create_task(run_forever())
    try:
        yield
    finally:
        scheduler.cancel()
        await notify_bot.stop()
        await get_telegram().stop()
        set_telegram(NotConfiguredTelegram())
        await dispose_engine()


def create_app() -> FastAPI:
    app = FastAPI(title="TelegramCRM", lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)
    for module in (auth, users, accounts, chats, clients, orders, reports, settings, ws):
        app.include_router(module.router)

    @app.get("/api/health")
    async def health():
        return {"ok": True}

    @app.exception_handler(HTTPException)
    async def http_error(_: Request, exc: HTTPException):
        return JSONResponse({"detail": exc.detail}, status_code=exc.status_code, headers=getattr(exc, "headers", None))

    static_dir: Path = get_settings().static_dir
    index = static_dir / "index.html"
    if (static_dir / "assets").is_dir():
        app.mount("/assets", StaticFiles(directory=static_dir / "assets"), name="assets")

    @app.get("/{path:path}", include_in_schema=False)
    async def spa(path: str):
        if path.startswith("api/"):
            raise HTTPException(404, "Topilmadi")
        candidate = (static_dir / path).resolve()
        if path and candidate.is_file() and candidate.is_relative_to(static_dir.resolve()):
            return FileResponse(candidate)
        if index.exists():
            return FileResponse(index, headers={"Cache-Control": "no-cache"})
        return JSONResponse({"detail": "Frontend yig'ilmagan (frontend/ papkada npm run build)"}, status_code=404)

    return app


logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
app = create_app()
