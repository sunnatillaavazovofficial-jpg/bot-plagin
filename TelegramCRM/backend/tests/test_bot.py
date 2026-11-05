"""Bot orqali kassaga yozish, buyruqlar va Telegram Mini App orqali kirish."""

from __future__ import annotations

import hashlib
import hmac
import json
import time
from urllib.parse import urlencode

import pytest
from sqlalchemy import select

from app.config import get_settings
from app.db import SessionLocal
from app.models import Transaction, User
from app.notify.bot import notify_bot
from app.notify.finance_bot import parse_entry
from app.security import verify_telegram_init_data

from .conftest import make_client

CATS = ["Material", "Ijara", "Avans", "Oylik", "Transport"]


@pytest.mark.parametrize(
    "text, kind, amount, method, order_id, category",
    [
        ("+500000 Aziz avans", "kirim", 500_000, "naqd", None, "Avans"),
        ("+500 000 Aziz", "kirim", 500_000, "naqd", None, None),
        ("-80 ming bo'yoq karta", "chiqim", 80_000, "karta", None, "Material"),
        ("+1.5mln click #12", "kirim", 1_500_000, "karta", 12, None),
        ("-80k taksi", "chiqim", 80_000, "naqd", None, "Transport"),
        ("−1 200 000 ijara", "chiqim", 1_200_000, "naqd", None, "Ijara"),
        ("+500000 2 ta banner", "kirim", 500_000, "naqd", None, None),
    ],
)
def test_parse_entry(text, kind, amount, method, order_id, category):
    p = parse_entry(text, CATS)
    assert p is not None
    assert (p.kind, p.amount, p.method, p.order_id, p.category_name) == (kind, amount, method, order_id, category)


@pytest.mark.parametrize("text", ["salom", "+1.5 abc", "-0", "+998901234567", "500000"])
def test_parse_entry_rejects(text):
    assert parse_entry(text, CATS) is None


def msg(text, tg_id=777, chat_id=777):
    return {"message": {"text": text, "chat": {"id": chat_id, "type": "private"}, "from": {"id": tg_id}}}


async def _link(user_id: int, tg_id: int) -> None:
    async with SessionLocal() as s:
        user = await s.get(User, user_id)
        user.tg_user_id = tg_id
        await s.commit()


def last_reply(chat_id=777) -> str:
    return [t for cid, t in notify_bot.sent if cid == chat_id][-1]


async def test_bot_entry_and_buttons(app, admin):
    await _link(1, 777)
    notify_bot.sent.clear()
    await notify_bot.handle_update(msg("+500000 Aziz avans"))
    assert "✅ Yozildi" in last_reply() and "500 000 so'm" in last_reply()
    async with SessionLocal() as s:
        tx = (await s.scalars(select(Transaction).where(Transaction.source == "bot"))).one()
        assert (tx.kind, tx.amount, tx.method, tx.note) == ("kirim", 500_000, "naqd", "Aziz avans")
    items = (await admin.get("/api/finance/transactions")).json()["items"]
    assert items[0]["category_name"] == "Avans" and items[0]["source"] == "bot"

    def cb(data):
        return {"callback_query": {"id": "q1", "data": data, "from": {"id": 777},
                                   "message": {"message_id": 5, "chat": {"id": 777}}}}

    notify_bot.calls.clear()
    await notify_bot.handle_update(cb(f"tm:{tx.id}"))
    async with SessionLocal() as s:
        assert (await s.get(Transaction, tx.id)).method == "karta"
    assert any(m == "editMessageText" for m, _ in notify_bot.calls)
    await notify_bot.handle_update(cb(f"tc:{tx.id}"))
    keyboard = [m for m in notify_bot.calls if m[0] == "editMessageText"][-1][1]["reply_markup"]["inline_keyboard"]
    other = next(b for row in keyboard for b in row if b["text"] == "Boshqa kirim")
    await notify_bot.handle_update(cb(other["callback_data"]))
    items = (await admin.get("/api/finance/transactions")).json()["items"]
    assert items[0]["category_name"] == "Boshqa kirim"
    await notify_bot.handle_update(cb(f"td:{tx.id}"))
    assert (await admin.get("/api/finance/transactions")).json()["total"] == 0


async def test_bot_order_payment_and_commands(app, admin):
    await _link(1, 777)
    order = (await admin.post("/api/orders", json={"title": "Banner", "price": 1_000_000})).json()
    await notify_bot.handle_update(msg(f"+300000 karta #{order['id']}"))
    detail = (await admin.get(f"/api/orders/{order['id']}")).json()
    assert detail["paid"] == 300_000
    assert any("(bot)" in e["text"] for e in detail["events"])
    await notify_bot.handle_update(msg("-50000 taksi"))
    await notify_bot.handle_update(msg("/balans"))
    assert "Kassa qoldig'i: 250 000 so'm" in last_reply()
    await notify_bot.handle_update(msg("/bugun"))
    assert "Kirim: 300 000 so'm" in last_reply() and "Chiqim: 50 000 so'm" in last_reply()
    await notify_bot.handle_update(msg("/yordam"))
    assert "Kassaga yozish" in last_reply()
    await notify_bot.handle_update(msg("+998901234567"))
    assert "tushunmadim" in last_reply()


async def test_bot_refuses_unlinked_and_workers(app, admin):
    notify_bot.sent.clear()
    await notify_bot.handle_update(msg("+1000 test", tg_id=555, chat_id=555))
    assert "Botni ulash" in last_reply(555)
    resp = await admin.post("/api/users", json={"name": "U", "login": "u1", "password": "parol123", "role": "xodim"})
    await _link(resp.json()["id"], 556)
    await notify_bot.handle_update(msg("+1000 test", tg_id=556, chat_id=556))
    assert "faqat admin va menejerlar" in last_reply(556)
    assert (await admin.get("/api/finance/transactions")).json()["total"] == 0


def _init_data(token: str, user_id: int, auth_date: int | None = None, tamper: bool = False) -> str:
    fields = {
        "auth_date": str(auth_date or int(time.time())),
        "query_id": "AAH",
        "user": json.dumps({"id": user_id, "first_name": "Ali"}, separators=(",", ":")),
    }
    check = "\n".join(f"{k}={v}" for k, v in sorted(fields.items()))
    secret = hmac.new(b"WebAppData", token.encode(), hashlib.sha256).digest()
    fields["hash"] = hmac.new(secret, check.encode(), hashlib.sha256).hexdigest()
    if tamper:
        fields["user"] = fields["user"].replace(str(user_id), "1")
    return urlencode(fields)


def test_verify_init_data():
    token = "123:ABC"
    assert verify_telegram_init_data(_init_data(token, 42), token)["id"] == 42
    assert verify_telegram_init_data(_init_data(token, 42, tamper=True), token) is None
    assert verify_telegram_init_data(_init_data("999:XYZ", 42), token) is None
    assert verify_telegram_init_data(_init_data(token, 42, auth_date=int(time.time()) - 200_000), token) is None
    assert verify_telegram_init_data("", token) is None


async def test_mini_app_login(app, monkeypatch):
    import dataclasses

    import app.routers.auth as auth_router

    settings = dataclasses.replace(get_settings(), bot_token="123:ABC")
    monkeypatch.setattr(auth_router, "get_settings", lambda: settings)
    await _link(1, 4242)
    client = make_client(app)
    bad = await client.post("/api/auth/telegram", json={"init_data": _init_data("123:ABC", 9999)})
    assert bad.status_code == 403
    ok = await client.post("/api/auth/telegram", json={"init_data": _init_data("123:ABC", 4242)})
    assert ok.status_code == 200
    assert (await client.get("/api/auth/me")).json()["login"] == "admin"
    await client.aclose()


async def test_admin_tg_id_prelinks_admin(tmp_path, monkeypatch):
    """ADMIN_TG_ID berilsa admin oldindan bog'lanadi va bot unga darrov javob beradi."""
    monkeypatch.setenv("DATA_DIR", str(tmp_path / "data"))
    monkeypatch.setenv("TG_FAKE", "1")
    monkeypatch.setenv("FAKE_INTERVAL", "0")
    monkeypatch.setenv("DEMO_FINANCE", "0")
    monkeypatch.setenv("SECRET_KEY", "test-secret")
    monkeypatch.setenv("ADMIN_PASSWORD", "admin123")
    monkeypatch.setenv("ADMIN_TG_ID", "6000000001")
    monkeypatch.setenv("STATIC_DIR", str(tmp_path / "static"))
    get_settings.cache_clear()
    from app.main import create_app

    application = create_app()
    try:
        async with application.router.lifespan_context(application):
            async with SessionLocal() as s:
                admin = await s.get(User, 1)
                assert admin.tg_user_id == 6000000001
            notify_bot.sent.clear()
            await notify_bot.handle_update(msg("/balans", tg_id=6000000001, chat_id=6000000001))
            assert "Kassa qoldig'i" in last_reply(6000000001)
        # Qayta ishga tushganda takror bog'lanmaydi va xato bermaydi
        async with application.router.lifespan_context(application):
            async with SessionLocal() as s:
                linked = (await s.scalars(select(User).where(User.tg_user_id == 6000000001))).all()
                assert len(linked) == 1
    finally:
        get_settings.cache_clear()
