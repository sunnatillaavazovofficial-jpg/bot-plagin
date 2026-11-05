from __future__ import annotations

import httpx

from app import tunnel
from app.db import SessionLocal
from app.models import User
from app.notify.bot import notify_bot

from .conftest import make_client


def metrics_client(state: dict) -> httpx.AsyncClient:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/quicktunnel"
        if state.get("down"):
            raise httpx.ConnectError("tunnel hali tayyor emas")
        return httpx.Response(200, json={"hostname": state["host"]})

    return httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://tunnel:2000")


async def test_tunnel_url_updates_bot_and_notifies_once(app, admin):
    async with SessionLocal() as s:
        user = await s.get(User, 1)
        user.tg_user_id = 777
        await s.commit()
    state = {"host": "", "down": True}
    http = metrics_client(state)
    notify_bot.sent.clear()
    notify_bot.calls.clear()

    # Tunnel hali tayyor emas yoki manzil bo'sh — hech narsa o'zgarmaydi
    assert await tunnel.check_once(http, "http://tunnel:2000") is None
    state.update(down=False, host="")
    assert await tunnel.check_once(http, "http://tunnel:2000") is None
    state["host"] = "evil host/<b>"
    assert await tunnel.check_once(http, "http://tunnel:2000") is None
    assert tunnel.public_url() == ""

    state["host"] = "olma-nok-uzum.trycloudflare.com"
    url = "https://olma-nok-uzum.trycloudflare.com"
    assert await tunnel.check_once(http, "http://tunnel:2000") == url
    assert tunnel.public_url() == url
    menu = [p for m, p in notify_bot.calls if m == "setChatMenuButton"]
    assert menu and menu[-1]["menu_button"]["web_app"]["url"] == url
    assert [(uid, url in text) for uid, text in notify_bot.sent] == [(777, True)]

    # O'sha manzil — takror yuborilmaydi
    assert await tunnel.check_once(http, "http://tunnel:2000") is None
    # Ilova qayta yondi (manzil xotirada yo'q), tunnel o'sha — tugma yangilanadi, xabar takrorlanmaydi
    tunnel.reset()
    assert await tunnel.check_once(http, "http://tunnel:2000") == url
    assert len(notify_bot.sent) == 1

    # Kompyuter qayta yoqildi — yangi manzil
    state["host"] = "yangi-manzil.trycloudflare.com"
    assert await tunnel.check_once(http, "http://tunnel:2000") == "https://yangi-manzil.trycloudflare.com"
    assert len(notify_bot.sent) == 2

    settings = (await admin.get("/api/settings")).json()
    assert settings["public_url"] == "https://yangi-manzil.trycloudflare.com"
    assert (await admin.put("/api/settings", json={"site_name": "Mediamax"})).status_code == 200
    robots = (await admin.get("/robots.txt")).text
    assert "Sitemap: https://yangi-manzil.trycloudflare.com/sitemap.xml" in robots
    await http.aclose()
    tunnel.reset()


async def test_cookie_secure_follows_https(app):
    http_client = make_client(app)
    resp = await http_client.post("/api/auth/login", json={"login": "admin", "password": "admin123"})
    assert "secure" not in resp.headers["set-cookie"].lower()
    await http_client.aclose()

    https_client = httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="https://test")
    resp = await https_client.post("/api/auth/login", json={"login": "admin", "password": "admin123"})
    assert "secure" in resp.headers["set-cookie"].lower()
    assert (await https_client.get("/api/auth/me")).status_code == 200
    await https_client.aclose()
