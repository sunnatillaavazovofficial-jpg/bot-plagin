from __future__ import annotations

from datetime import timedelta

from app.db import SessionLocal
from app.models import Chat, User, utcnow
from app.notify.bot import notify_bot
from app.notify.events import check_deadlines, check_unanswered, daily_summary
from app.telegram import store
from app.telegram.store import MsgData
from app.timeutil import local_today

from .conftest import login, make_client


async def test_login_required_and_wrong_password(app):
    client = make_client(app)
    assert (await client.get("/api/chats")).status_code == 401
    resp = await client.post("/api/auth/login", json={"login": "admin", "password": "xato"})
    assert resp.status_code == 401
    await client.aclose()


async def test_demo_accounts_and_chats(admin):
    accounts = (await admin.get("/api/accounts")).json()["accounts"]
    assert len(accounts) == 3
    chats = (await admin.get("/api/chats")).json()
    assert len(chats) == 11  # yashirin chatlar (Oila, Futbol yangiliklari) chiqmaydi
    assert {c["title"] for c in chats}.isdisjoint({"Oila", "Futbol yangiliklari"})
    private = (await admin.get("/api/chats", params={"kind": "private"})).json()
    assert len(private) == 6 and all(c["kind"] == "private" for c in private)
    unread = (await admin.get("/api/chats", params={"filter": "unread"})).json()
    assert unread and all(c["unread_count"] > 0 for c in unread)
    only_first = (await admin.get("/api/chats", params={"account_id": accounts[0]["id"]})).json()
    assert {c["account_id"] for c in only_first} == {accounts[0]["id"]}


async def test_send_message_resets_unread(admin):
    chat = (await admin.get("/api/chats", params={"filter": "unread"})).json()[0]
    resp = await admin.post(f"/api/chats/{chat['id']}/messages", data={"text": "Javob"})
    assert resp.status_code == 200, resp.text
    assert resp.json()["out"] is True
    after = (await admin.get(f"/api/chats/{chat['id']}")).json()
    assert after["unread_count"] == 0
    assert after["last_message_out"] is True


async def test_staff_sees_only_allowed_accounts(app, admin):
    resp = await admin.post(
        "/api/users",
        json={"name": "Menejer", "login": "men", "password": "parol123", "role": "menejer", "account_ids": [2]},
    )
    assert resp.status_code == 200, resp.text
    manager = await login(app, "men", "parol123")
    chats = (await manager.get("/api/chats")).json()
    assert chats and {c["account_id"] for c in chats} == {2}
    foreign = (await admin.get("/api/chats", params={"account_id": 1})).json()[0]
    assert (await manager.get(f"/api/chats/{foreign['id']}")).status_code == 403
    assert (await manager.get(f"/api/chats/{foreign['id']}/messages")).status_code == 403
    assert (await manager.post(f"/api/chats/{foreign['id']}/messages", data={"text": "x"})).status_code == 403
    assert (await manager.get("/api/reports/summary")).status_code == 403
    assert (await manager.post("/api/accounts/login/start", json={"phone": "+998901234567"})).status_code == 403
    await manager.aclose()


async def test_deactivated_user_is_locked_out(app, admin):
    uid = (
        await admin.post("/api/users", json={"name": "A", "login": "aa", "password": "parol123", "role": "menejer"})
    ).json()["id"]
    user = await login(app, "aa", "parol123")
    assert (await user.get("/api/auth/me")).status_code == 200
    await admin.patch(f"/api/users/{uid}", json={"is_active": False})
    assert (await user.get("/api/auth/me")).status_code == 401
    await user.aclose()


async def test_worker_sees_only_assigned_orders(app, admin):
    worker_id = (
        await admin.post("/api/users", json={"name": "Usta", "login": "usta", "password": "parol123", "role": "xodim"})
    ).json()["id"]
    mine = (await admin.post("/api/orders", json={"title": "Banner", "assignee_id": worker_id})).json()
    other = (await admin.post("/api/orders", json={"title": "Vizitka"})).json()
    worker = await login(app, "usta", "parol123")
    ids = [o["id"] for o in (await worker.get("/api/orders")).json()]
    assert ids == [mine["id"]]
    assert (await worker.get(f"/api/orders/{other['id']}")).status_code == 403
    assert (await worker.patch(f"/api/orders/{mine['id']}", json={"price": 1})).status_code == 403
    assert (await worker.patch(f"/api/orders/{mine['id']}", json={"status_id": 2})).status_code == 200
    assert (await worker.get("/api/clients")).status_code == 403
    await worker.aclose()


async def test_order_from_chat_creates_client_and_tracks_payments(admin):
    chat = (await admin.get("/api/chats")).json()[0]
    order = (
        await admin.post("/api/orders", json={"title": "Lightbox", "chat_id": chat["id"], "price": 1_200_000})
    ).json()
    assert order["client_id"] and order["account_id"] == chat["account_id"]
    await admin.post(f"/api/orders/{order['id']}/payments", json={"amount": 500_000, "method": "karta"})
    detail = (await admin.get(f"/api/orders/{order['id']}")).json()
    assert detail["paid"] == 500_000 and detail["debt"] == 700_000
    statuses = (await admin.get("/api/statuses")).json()
    done = next(s for s in statuses if s["is_done"])
    detail = (await admin.patch(f"/api/orders/{order['id']}", json={"status_id": done["id"]})).json()
    assert detail["closed_at"] is not None
    events = (await admin.get(f"/api/orders/{order['id']}")).json()["events"]
    assert [e["kind"] for e in events][:2] == ["status", "payment"]
    client = (await admin.get(f"/api/clients/{order['client_id']}")).json()
    assert client["debt"] == 700_000 and len(client["chats"]) == 1

    report = (await admin.get("/api/reports/summary")).json()
    assert report["kpi"]["revenue"] == 500_000
    assert report["kpi"]["orders_done"] == 1
    assert report["kpi"]["debt"] == 700_000


async def test_order_files_copied_from_chat_media(admin):
    for chat in (await admin.get("/api/chats")).json():
        msgs = (await admin.get(f"/api/chats/{chat['id']}/messages")).json()["messages"]
        media = [m for m in msgs if m["media_type"]]
        if media:
            order = (
                await admin.post(
                    "/api/orders", json={"title": "Maket", "chat_id": chat["id"], "message_ids": [media[0]["id"]]}
                )
            ).json()
            files = (await admin.get(f"/api/orders/{order['id']}")).json()["files"]
            assert len(files) == 1
            resp = await admin.get(f"/api/order-files/{files[0]['id']}")
            assert resp.status_code == 200
            assert "sandbox" in resp.headers["content-security-policy"]
            return
    raise AssertionError("demo media topilmadi")


async def test_status_in_use_cannot_be_removed(admin):
    statuses = (await admin.get("/api/statuses")).json()
    await admin.post("/api/orders", json={"title": "X", "status_id": statuses[0]["id"]})
    resp = await admin.put("/api/statuses", json=statuses[1:])
    assert resp.status_code == 400


async def test_store_handlers_dedupe_edit_delete(app):
    async with SessionLocal() as s:
        chat = await s.get(Chat, 1)
        before = chat.unread_count
        data = MsgData(tg_id=500, out=False, text="Yangi", date=utcnow())
        _, created = await store.save_message(s, chat, data, live=True)
        _, again = await store.save_message(s, chat, data, live=True)
        await s.commit()
        assert created and not again
        assert chat.unread_count == before + 1
        res = await store.mark_edited(s, chat.account_id, chat.peer_id, 500, "Tahrir")
        assert res and res[1].edited and res[1].text == "Tahrir"
        rows = await store.mark_deleted(s, chat.account_id, [500])
        assert len(rows) == 1 and rows[0][1].deleted
        await s.commit()


async def test_fake_login_flow(admin):
    start = (await admin.post("/api/accounts/login/start", json={"phone": "+998901234560"})).json()
    bad = await admin.post("/api/accounts/login/code", json={"login_id": start["login_id"], "code": "0"})
    assert bad.status_code == 400
    step = (await admin.post("/api/accounts/login/code", json={"login_id": start["login_id"], "code": "12345"})).json()
    assert step["status"] == "password"
    done = (
        await admin.post("/api/accounts/login/password", json={"login_id": start["login_id"], "password": "parol"})
    ).json()
    assert done["status"] == "ok"
    assert len((await admin.get("/api/accounts")).json()["accounts"]) == 4


async def test_notifications(app, admin):
    async with SessionLocal() as s:
        user = await s.get(User, 1)
        user.tg_user_id = 777
        chat = await s.get(Chat, 1)
        chat.last_message_out = False
        chat.last_incoming_at = utcnow() - timedelta(minutes=45)
        await s.commit()
    today = local_today()
    await admin.post("/api/orders", json={"title": "Ertangi", "deadline": (today + timedelta(days=1)).isoformat()})
    await admin.post("/api/orders", json={"title": "Kechikkan", "deadline": (today - timedelta(days=2)).isoformat()})
    notify_bot.sent.clear()
    await check_unanswered()
    await check_deadlines(today)
    await daily_summary(today)
    texts = "\n".join(t for uid, t in notify_bot.sent if uid == 777)
    assert "javob yo'q" in texts
    assert "Ertangi" in texts and "Kechikkan" in texts
    assert "Muddati o'tgan: 1 ta" in texts
    # Ikkinchi marta yuborilmaydi
    notify_bot.sent.clear()
    await check_unanswered()
    await check_deadlines(today)
    await daily_summary(today)
    assert notify_bot.sent == []
