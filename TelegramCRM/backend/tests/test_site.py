"""Ochiq bosh sahifa: Google/Yandex uchun meta teglar, robots.txt, sitemap.xml va preset."""

from __future__ import annotations

import json
import re

from app.config import get_settings
from app.db import SessionLocal
from app.settings_store import get_all
from app.site import apply_preset

from .conftest import login, make_client

TEMPLATE = """<!doctype html>
<html lang="uz">
  <head>
    <title>TelegramCRM</title>
    <!--SEO_HEAD-->
  </head>
  <body>
    <div id="root"><!--SEO_BODY--></div>
  </body>
</html>"""

SITE = {
    "site_name": "Mediamax",
    "site_tagline_uz": "Tashqi reklama",
    "site_tagline_ru": "Наружная реклама",
    "site_about_uz": "Yoritish ustunlaridagi bannerlar",
    "site_about_ru": "Баннеры на опорах освещения",
    "site_services_uz": "Bannerlar\nKronshteynlar",
    "site_services_ru": "Баннеры\nКронштейны",
    "site_phone": "+998 77 740 80 80",
    "site_telegram": "@yusuf_mediamax",
}


def _index():
    static = get_settings().static_dir
    static.mkdir(parents=True, exist_ok=True)
    (static / "index.html").write_text(TEMPLATE, encoding="utf-8")


async def test_closed_until_configured(app):
    _index()
    anon = make_client(app)
    page = (await anon.get("/")).text
    assert 'name="robots" content="noindex, nofollow"' in page
    assert (await anon.get("/robots.txt")).text == "User-agent: *\nDisallow: /\n"
    assert (await anon.get("/sitemap.xml")).status_code == 404
    assert (await anon.get("/api/public/site")).json()["configured"] is False
    await anon.aclose()


async def test_public_page_for_search_engines(app, admin):
    _index()
    resp = await admin.put(
        "/api/settings",
        json={
            **SITE,
            "google_verification": '<meta name="google-site-verification" content="abc_DEF-123" />',
            "yandex_verification": "0123456789abcdef",
        },
    )
    assert resp.status_code == 200, resp.text
    assert resp.json()["site_telegram"] == "https://t.me/yusuf_mediamax"
    assert resp.json()["google_verification"] == "abc_DEF-123"

    anon = make_client(app)
    uz = (await anon.get("/")).text
    assert '<html lang="uz">' in uz
    assert "<title>Mediamax — Tashqi reklama</title>" in uz
    assert 'name="robots" content="index, follow"' in uz
    assert 'hreflang="ru" href="http://test/ru"' in uz
    assert 'name="google-site-verification" content="abc_DEF-123"' in uz
    assert 'name="yandex-verification" content="0123456789abcdef"' in uz
    assert "<li>Kronshteynlar</li>" in uz and 'href="tel:+998777408080"' in uz
    ld = json.loads(re.search(r'<script type="application/ld\+json">(.*?)</script>', uz).group(1))
    assert ld["name"] == "Mediamax" and ld["telephone"] == "+998 77 740 80 80"
    assert ld["sameAs"] == ["https://t.me/yusuf_mediamax"]

    ru = (await anon.get("/ru")).text
    assert '<html lang="ru">' in ru and "<title>Mediamax — Наружная реклама</title>" in ru
    assert "<li>Кронштейны</li>" in ru

    inner = await anon.get("/chats")
    assert 'content="noindex, nofollow"' in inner.text and inner.headers["x-robots-tag"] == "noindex, nofollow"
    assert "Mediamax</h1>" not in inner.text

    robots = (await anon.get("/robots.txt")).text
    assert "Allow: /$" in robots and "Disallow: /api/" in robots and "Disallow: /finance" in robots
    assert "Sitemap: http://test/sitemap.xml" in robots
    sitemap = await anon.get("/sitemap.xml")
    assert sitemap.headers["content-type"].startswith("application/xml")
    assert "<loc>http://test/</loc>" in sitemap.text and "<loc>http://test/ru</loc>" in sitemap.text

    public = (await anon.get("/api/public/site")).json()
    assert public["configured"] and public["services_ru"] == ["Баннеры", "Кронштейны"]
    assert "google_verification" not in json.dumps(public)
    await anon.aclose()


async def test_only_admin_edits_site(app, admin):
    await admin.post("/api/users", json={"name": "M", "login": "men", "password": "parol123", "role": "menejer"})
    manager = await login(app, "men", "parol123")
    assert (await manager.put("/api/settings", json={"site_name": "X"})).status_code == 403
    bad = await admin.put("/api/settings", json={"site_website": "javascript:alert(1)"})
    assert bad.status_code == 400
    await manager.aclose()


async def test_preset_fills_only_empty_fields(app, admin):
    await admin.put("/api/settings", json={"site_name": "Boshqa nom"})
    async with SessionLocal() as s:
        filled = await apply_preset(s, "media-max")
        await s.commit()
        values = await get_all(s)
    assert filled > 10
    assert values["site_name"] == "Boshqa nom"  # qo'lda yozilgani saqlanadi
    assert values["site_phone"] == "+998 77 740 80 80"
    assert "Баннеры на опорах освещения" in values["site_services_ru"]
    async with SessionLocal() as s:
        assert await apply_preset(s, "../etc/passwd") == 0
        assert await apply_preset(s, "yoq-preset") == 0
