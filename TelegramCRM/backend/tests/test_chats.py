"""Chat turlari, tanlash (ko'rsatish/yashirish), papkalar va bot tugmalari."""

from __future__ import annotations

from sqlalchemy import select

from app.config import get_settings
from app.db import SessionLocal
from app.models import Chat, Message, TgAccount
from app.telegram.manager import TelegramManager

from .conftest import login
from .test_manager import FakeEvent, make_msg, make_user


async def _chat_by_title(admin, title):
    picker = (await admin.get("/api/chats/picker", params={"q": title})).json()
    return next(c for c in picker["chats"] if c["title"] == title)


async def test_picker_lists_hidden_and_counts(admin):
    data = (await admin.get("/api/chats/picker")).json()
    titles = {c["title"]: c for c in data["chats"]}
    assert not titles["Oila"]["visible"] and not titles["Futbol yangiliklari"]["visible"]
    assert data["counts"]["channel"] == {"visible": 2, "hidden": 1}
    only_hidden = (await admin.get("/api/chats/picker", params={"visible": "false"})).json()
    assert {c["title"] for c in only_hidden["chats"]} == {"Oila", "Futbol yangiliklari"}


async def test_show_and_hide_chat(admin):
    oila = await _chat_by_title(admin, "Oila")
    resp = await admin.patch(f"/api/chats/{oila['id']}", json={"visible": True})
    assert resp.status_code == 200 and resp.json()["visible"] is True
    assert "Oila" in {c["title"] for c in (await admin.get("/api/chats")).json()}
    await admin.patch(f"/api/chats/{oila['id']}", json={"visible": False})
    assert "Oila" not in {c["title"] for c in (await admin.get("/api/chats")).json()}


async def test_bulk_and_folders(admin):
    folder = (await admin.post("/api/folders", json={"name": "Hamkorlar", "color": "#16a34a"})).json()
    chats = (await admin.get("/api/chats", params={"kind": "private"})).json()[:2]
    ids = [c["id"] for c in chats]
    resp = await admin.post("/api/chats/bulk", json={"chat_ids": ids, "folder_id": folder["id"], "muted": True})
    assert resp.json()["updated"] == 2
    in_folder = (await admin.get("/api/chats", params={"folder_id": folder["id"]})).json()
    assert sorted(c["id"] for c in in_folder) == sorted(ids) and all(c["muted"] for c in in_folder)
    folders = {f["name"]: f for f in (await admin.get("/api/folders")).json()}
    assert folders["Hamkorlar"]["chats"] == 2
    # Papka o'chirilsa chatlar papkasiz qoladi
    await admin.delete(f"/api/folders/{folder['id']}")
    no_folder = (await admin.get("/api/chats", params={"folder_id": 0})).json()
    assert set(ids) <= {c["id"] for c in no_folder}


async def test_muted_chats_not_counted_and_not_waiting(admin):
    before = (await admin.get("/api/chats/counts")).json()
    unread = (await admin.get("/api/chats", params={"filter": "unread", "kind": "private"})).json()[0]
    await admin.patch(f"/api/chats/{unread['id']}", json={"muted": True})
    after = (await admin.get("/api/chats/counts")).json()
    assert after["unread"] == before["unread"] - 1
    waiting = (await admin.get("/api/chats", params={"filter": "unanswered"})).json()
    assert all(c["kind"] == "private" and not c["muted"] for c in waiting)


async def test_cannot_write_to_foreign_channel(admin):
    channel = await _chat_by_title(admin, "Reklama materiallari (narxlar)")
    assert channel["kind"] == "channel" and channel["can_send"] is False
    resp = await admin.post(f"/api/chats/{channel['id']}/messages", data={"text": "salom"})
    assert resp.status_code == 400
    own = await _chat_by_title(admin, "Do'kon kanali")
    assert (await admin.post(f"/api/chats/{own['id']}/messages", data={"text": "Yangi post"})).status_code == 200


async def test_bot_buttons_click(admin):
    bot = await _chat_by_title(admin, "To'lov boti")
    msgs = (await admin.get(f"/api/chats/{bot['id']}/messages")).json()["messages"]
    with_buttons = next(m for m in msgs if m["buttons"])
    assert with_buttons["buttons"][0][0] == {"text": "💰 Balans", "kind": "callback"}
    resp = await admin.post(f"/api/messages/{with_buttons['id']}/click", json={"row": 0, "col": 0})
    assert resp.status_code == 200
    msgs = (await admin.get(f"/api/chats/{bot['id']}/messages")).json()["messages"]
    assert "Balansingiz" in msgs[-1]["text"]


async def test_worker_cannot_change_visibility(app, admin):
    await admin.post(
        "/api/users",
        json={"name": "Usta", "login": "usta", "password": "parol123", "role": "xodim", "account_ids": [1]},
    )
    worker = await login(app, "usta", "parol123")
    chat = (await worker.get("/api/chats")).json()[0]
    assert (await worker.patch(f"/api/chats/{chat['id']}", json={"visible": False})).status_code == 403
    assert (await worker.get("/api/chats/picker")).status_code == 403
    await worker.aclose()


async def test_hidden_chat_messages_not_stored(app):
    async with SessionLocal() as s:
        acc = TgAccount(phone="+1", name="Real", tg_user_id=999, status="active", show_private=False)
        s.add(acc)
        await s.commit()
        account_id = acc.id
    m = TelegramManager(get_settings())
    m.me_ids[account_id] = 999
    await m._on_new(account_id, FakeEvent(make_msg(mid=7, text="Yashirin xabar"), make_user()))
    async with SessionLocal() as s:
        chat = await s.scalar(select(Chat).where(Chat.account_id == account_id))
        assert chat is not None and chat.visible is False
        assert chat.last_message_text == "Yashirin xabar" and chat.unread_count == 1
        assert (await s.scalars(select(Message).where(Message.chat_id == chat.id))).all() == []
