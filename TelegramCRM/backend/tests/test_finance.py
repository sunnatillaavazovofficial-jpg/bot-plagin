"""Kassa (kirim-chiqim), qarz daftari, Excel va ruxsatlar."""

from __future__ import annotations

import io

from openpyxl import load_workbook

from .conftest import login


async def _cats(client):
    return {c["name"]: c for c in (await client.get("/api/finance/categories")).json()}


async def test_balances_and_period_totals(admin):
    cats = await _cats(admin)
    async def add(**body):
        resp = await admin.post("/api/finance/transactions", json=body)
        assert resp.status_code == 200, resp.text
        return resp.json()

    await add(kind="kirim", amount=1_000_000, method="naqd", category_id=cats["Boshlang'ich qoldiq"]["id"])
    await add(kind="chiqim", amount=200_000, method="naqd", category_id=cats["Material"]["id"], note="akril")
    await add(kind="otkazma", amount=300_000, method="naqd", to_method="karta")
    await add(kind="kirim", amount=500_000, method="karta", category_id=cats["Avans"]["id"])
    data = (await admin.get("/api/finance/summary")).json()
    assert data["balances"]["naqd"] == 500_000
    assert data["balances"]["karta"] == 800_000
    assert data["balances"]["jami"] == 1_300_000
    # Boshlang'ich qoldiq va o'tkazma daromad hisoblanmaydi
    assert data["totals"] == {"kirim": 500_000, "chiqim": 200_000, "foyda": 300_000}
    assert data["chiqim_by_category"][0]["name"] == "Material"
    found = (await admin.get("/api/finance/transactions", params={"q": "akril"})).json()
    assert found["total"] == 1


async def test_validation(admin):
    cats = await _cats(admin)
    bad = await admin.post(
        "/api/finance/transactions", json={"kind": "chiqim", "amount": 1000, "category_id": cats["Avans"]["id"]}
    )
    assert bad.status_code == 400
    assert (await admin.post("/api/finance/transactions", json={"kind": "kirim", "amount": 0})).status_code == 400
    same = await admin.post(
        "/api/finance/transactions", json={"kind": "otkazma", "amount": 10, "method": "naqd", "to_method": "naqd"}
    )
    assert same.status_code == 400
    # Toifasiz kiritilsa — «Boshqa ...» toifasi
    tx = (await admin.post("/api/finance/transactions", json={"kind": "chiqim", "amount": 5000})).json()
    assert tx["category_name"] == "Boshqa chiqim"


async def test_order_payments_go_to_ledger(admin):
    order = (await admin.post("/api/orders", json={"title": "Banner", "price": 900_000})).json()
    await admin.post(f"/api/orders/{order['id']}/payments", json={"amount": 600_000, "method": "karta"})
    await admin.post(f"/api/orders/{order['id']}/payments", json={"amount": -100_000, "method": "naqd"})
    detail = (await admin.get(f"/api/orders/{order['id']}")).json()
    assert detail["paid"] == 500_000 and [p["amount"] for p in detail["payments"]] == [600_000, -100_000]
    txs = (await admin.get("/api/finance/transactions", params={"order_id": order["id"]})).json()["items"]
    assert {(t["kind"], t["category_name"]) for t in txs} == {("kirim", "Zakaz to'lovi"), ("chiqim", "Qaytarilgan pul")}
    # Kassadan o'chirilsa zakaz to'lovi ham qayta hisoblanadi
    refund = next(t for t in txs if t["kind"] == "chiqim")
    await admin.delete(f"/api/finance/transactions/{refund['id']}")
    assert (await admin.get(f"/api/orders/{order['id']}")).json()["paid"] == 600_000
    # Kassada zakazga bog'lab kirim yozish
    await admin.post("/api/finance/transactions", json={"kind": "kirim", "amount": 300_000, "order_id": order["id"]})
    detail = (await admin.get(f"/api/orders/{order['id']}")).json()
    assert detail["paid"] == 900_000 and detail["debt"] == 0
    summary = (await admin.get("/api/reports/summary")).json()
    assert summary["kpi"]["revenue"] == 900_000


async def test_permissions(app, admin):
    await admin.post("/api/users", json={"name": "M", "login": "men", "password": "parol123", "role": "menejer"})
    await admin.post("/api/users", json={"name": "X", "login": "xod", "password": "parol123", "role": "xodim"})
    admin_tx = (await admin.post("/api/finance/transactions", json={"kind": "kirim", "amount": 1000})).json()
    manager = await login(app, "men", "parol123")
    own = (await manager.post("/api/finance/transactions", json={"kind": "chiqim", "amount": 500})).json()
    assert (await manager.get("/api/finance/summary")).status_code == 200
    assert (await manager.delete(f"/api/finance/transactions/{admin_tx['id']}")).status_code == 403
    assert (await manager.delete(f"/api/finance/transactions/{own['id']}")).status_code == 200
    worker = await login(app, "xod", "parol123")
    assert (await worker.get("/api/finance/summary")).status_code == 403
    assert (await worker.post("/api/finance/transactions", json={"kind": "kirim", "amount": 1})).status_code == 403
    await manager.aclose()
    await worker.aclose()


async def test_debt_book(admin):
    debt = (
        await admin.post(
            "/api/finance/debts",
            json={"direction": "bizga", "person": "Anvar", "amount": 1_000_000, "with_cash": True},
        )
    ).json()
    summary = (await admin.get("/api/finance/summary")).json()
    assert summary["balances"]["naqd"] == -1_000_000  # qarz berildi — kassadan chiqdi
    assert summary["debts"]["bizga"] == 1_000_000
    part = (await admin.post(f"/api/finance/debts/{debt['id']}/pay", json={"amount": 400_000})).json()
    assert part["remaining"] == 600_000 and part["closed_at"] is None
    done = (await admin.post(f"/api/finance/debts/{debt['id']}/pay", json={"amount": 600_000, "method": "karta"})).json()
    assert done["remaining"] == 0 and done["closed_at"] is not None
    assert (await admin.get("/api/finance/debts")).json()["debts"] == []
    closed = (await admin.get("/api/finance/debts", params={"status": "closed"})).json()["debts"]
    assert closed[0]["paid"] == 1_000_000

    ours = (await admin.post("/api/finance/debts", json={"direction": "bizdan", "person": "Ta'minotchi", "amount": 250_000})).json()
    await admin.post(f"/api/finance/debts/{ours['id']}/pay", json={"amount": 100_000})
    summary = (await admin.get("/api/finance/summary")).json()
    assert summary["debts"]["bizdan"] == 150_000
    # Zakaz qarzlari ham qarz daftarida
    await admin.post("/api/orders", json={"title": "Lightbox", "price": 700_000})
    orders = (await admin.get("/api/finance/debts")).json()["orders"]
    assert any(o["title"] == "Lightbox" and o["remaining"] == 700_000 for o in orders)


async def test_categories(admin):
    cats = await _cats(admin)
    order_cat = cats["Zakaz to'lovi"]["id"]
    assert (await admin.delete(f"/api/finance/categories/{order_cat}")).status_code == 400
    new = (await admin.post("/api/finance/categories", json={"name": "Bo'yoq", "kind": "chiqim"})).json()
    tx = (await admin.post("/api/finance/transactions", json={"kind": "chiqim", "amount": 1, "category_id": new["id"]})).json()
    assert tx["category_name"] == "Bo'yoq"
    await admin.delete(f"/api/finance/categories/{new['id']}")
    items = (await admin.get("/api/finance/transactions")).json()["items"]
    assert next(t for t in items if t["id"] == tx["id"])["category_id"] is None


async def test_excel_export(admin):
    cats = await _cats(admin)
    await admin.post("/api/finance/transactions", json={"kind": "kirim", "amount": 750_000, "category_id": cats["Avans"]["id"], "note": "Aziz"})
    await admin.post("/api/finance/transactions", json={"kind": "chiqim", "amount": 250_000, "category_id": cats["Ijara"]["id"]})
    await admin.post("/api/finance/debts", json={"direction": "bizdan", "person": "Bank", "amount": 5_000_000})
    resp = await admin.get("/api/finance/export.xlsx")
    assert resp.status_code == 200
    assert "kassa_" in resp.headers["content-disposition"]
    wb = load_workbook(io.BytesIO(resp.content))
    assert wb.sheetnames == ["Kirim-chiqim", "Toifalar bo'yicha", "Qarz daftari"]
    ws = wb["Kirim-chiqim"]
    values = [[c.value for c in row] for row in ws.iter_rows()]
    assert any(row[0] == "Foyda (kirim − chiqim)" and row[7] == 500_000 for row in values)
    assert any(row[1] == "Bank" for row in wb["Qarz daftari"].iter_rows(values_only=True))


async def test_demo_finance_seed(app):
    from app.telegram.fake import seed_demo_finance

    from .conftest import login as _login

    await seed_demo_finance()
    admin = await _login(app)
    data = (await admin.get("/api/finance/summary")).json()
    # 6 000 000 + 2 500 000 + 1 200 000 + 800 000 − (1 350 000 + 4 000 000 + 380 000 + 640 000 + 3 000 000)
    assert data["balances"]["jami"] == 1_130_000
    assert data["debts"]["bizga"] == 1_500_000 and data["debts"]["bizdan"] == 2_400_000
    await admin.aclose()
