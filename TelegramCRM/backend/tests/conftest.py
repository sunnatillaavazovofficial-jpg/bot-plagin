from __future__ import annotations

import httpx
import pytest

from app.config import get_settings


@pytest.fixture
async def app(tmp_path, monkeypatch):
    monkeypatch.setenv("DATA_DIR", str(tmp_path / "data"))
    monkeypatch.setenv("TG_FAKE", "1")
    monkeypatch.setenv("FAKE_INTERVAL", "0")
    monkeypatch.setenv("SECRET_KEY", "test-secret")
    monkeypatch.setenv("ADMIN_PASSWORD", "admin123")
    monkeypatch.setenv("BOT_TOKEN", "")
    monkeypatch.setenv("STATIC_DIR", str(tmp_path / "static"))
    get_settings.cache_clear()
    from app.main import create_app

    application = create_app()
    async with application.router.lifespan_context(application):
        yield application
    get_settings.cache_clear()


def make_client(app) -> httpx.AsyncClient:
    return httpx.AsyncClient(transport=httpx.ASGITransport(app=app), base_url="http://test")


async def login(app, login: str = "admin", password: str = "admin123") -> httpx.AsyncClient:
    client = make_client(app)
    resp = await client.post("/api/auth/login", json={"login": login, "password": password})
    assert resp.status_code == 200, resp.text
    return client


@pytest.fixture
async def admin(app):
    client = await login(app)
    yield client
    await client.aclose()
