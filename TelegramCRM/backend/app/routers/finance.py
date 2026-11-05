"""Hisob-kitob: kassa (kirim-chiqim), toifalar, qarz daftari va Excel. Admin va menejerlar uchun."""

from __future__ import annotations

import datetime as dt
import io
from collections import defaultdict
from datetime import date, timedelta

from fastapi import APIRouter, Depends, HTTPException, Query
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
from sqlalchemy import func, or_, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import admin_only, staff_manager
from ..finance import (
    KIND_LABELS,
    METHOD_LABELS,
    FinanceError,
    balances,
    create_transaction,
    debt_paid,
    delete_transaction,
    period_totals,
    recalc_order_paid,
    refresh_debt,
    repayment_kind,
    system_category,
    validate,
)
from ..models import PAY_METHODS, Category, Client, Debt, Order, OrderStatus, Transaction, User, utcnow
from ..serializers import category_dict, debt_dict, tx_dict
from ..timeutil import local_today
from ..ws_hub import hub

router = APIRouter(prefix="/api/finance", tags=["finance"])


def _err(exc: FinanceError) -> HTTPException:
    return HTTPException(400, str(exc))


async def _notify() -> None:
    await hub.publish({"type": "finance.update"})


def _period(date_from: date | None, date_to: date | None) -> tuple[date, date]:
    date_to = date_to or local_today()
    date_from = date_from or date_to.replace(day=1)
    if date_from > date_to or (date_to - date_from).days > 1100:
        raise HTTPException(400, "Davr noto'g'ri tanlangan")
    return date_from, date_to


async def _tx_rows(session: AsyncSession, txs: list[Transaction]) -> list[dict]:
    cats = {c.id: c for c in (await session.scalars(select(Category))).all()}
    client_ids = {t.client_id for t in txs if t.client_id}
    clients = {
        c.id: c.name for c in (await session.scalars(select(Client).where(Client.id.in_(client_ids or {0})))).all()
    }
    return [tx_dict(t, cats.get(t.category_id or 0), clients.get(t.client_id or 0)) for t in txs]


# ---------------- umumiy ko'rinish ----------------


@router.get("/summary")
async def summary(
    date_from: date | None = None,
    date_to: date | None = None,
    _: User = Depends(staff_manager),
    session: AsyncSession = Depends(get_session),
):
    date_from, date_to = _period(date_from, date_to)
    opening = await system_category(session, "opening")
    cats = {c.id: c for c in (await session.scalars(select(Category))).all()}

    rows = (
        await session.execute(
            select(Transaction.kind, Transaction.category_id, Transaction.date, func.sum(Transaction.amount))
            .where(Transaction.date >= date_from, Transaction.date <= date_to, Transaction.kind.in_(("kirim", "chiqim")))
            .group_by(Transaction.kind, Transaction.category_id, Transaction.date)
        )
    ).all()
    by_cat: dict[tuple[str, int | None], int] = defaultdict(int)
    days = [date_from + timedelta(days=i) for i in range((date_to - date_from).days + 1)]
    daily = {d: {"date": d.isoformat(), "kirim": 0, "chiqim": 0} for d in days}
    for kind, cat_id, day, total in rows:
        if opening is not None and cat_id == opening.id:
            continue
        by_cat[(kind, cat_id)] += int(total)
        if day in daily:
            daily[day][kind] += int(total)

    def cat_rows(kind: str) -> list[dict]:
        items = [
            {
                "category_id": cid,
                "name": cats[cid].name if cid in cats else "Toifasiz",
                "color": cats[cid].color if cid in cats else "#94a3b8",
                "amount": amount,
            }
            for (k, cid), amount in by_cat.items()
            if k == kind
        ]
        return sorted(items, key=lambda r: -r["amount"])

    debts = (await session.scalars(select(Debt).where(Debt.closed_at.is_(None)))).all()
    owed_to_us = owed_by_us = 0
    for d in debts:
        left = max(d.amount - await debt_paid(session, d), 0)
        if d.direction == "bizga":
            owed_to_us += left
        else:
            owed_by_us += left
    order_debt = await _order_debt_total(session)

    return {
        "date_from": date_from.isoformat(),
        "date_to": date_to.isoformat(),
        "balances": await balances(session),
        "totals": await period_totals(session, date_from, date_to),
        "kirim_by_category": cat_rows("kirim"),
        "chiqim_by_category": cat_rows("chiqim"),
        "daily": list(daily.values()),
        "debts": {"bizga": owed_to_us, "bizdan": owed_by_us, "zakazlar": order_debt},
    }


async def _order_debt_total(session: AsyncSession) -> int:
    value = await session.scalar(
        select(func.coalesce(func.sum(Order.price - Order.paid), 0))
        .join(OrderStatus, OrderStatus.id == Order.status_id)
        .where(OrderStatus.is_cancel.is_(False), Order.price > Order.paid)
    )
    return int(value or 0)


# ---------------- kassa yozuvlari ----------------


@router.get("/transactions")
async def list_transactions(
    date_from: date | None = None,
    date_to: date | None = None,
    kind: str | None = Query(None, pattern="^(kirim|chiqim|otkazma)$"),
    category_id: int | None = None,
    method: str | None = None,
    order_id: int | None = None,
    debt_id: int | None = None,
    q: str | None = None,
    limit: int = Query(100, le=1000),
    offset: int = 0,
    _: User = Depends(staff_manager),
    session: AsyncSession = Depends(get_session),
):
    query = select(Transaction).outerjoin(Client, Client.id == Transaction.client_id)
    if date_from:
        query = query.where(Transaction.date >= date_from)
    if date_to:
        query = query.where(Transaction.date <= date_to)
    if kind:
        query = query.where(Transaction.kind == kind)
    if category_id:
        query = query.where(Transaction.category_id == category_id)
    if method:
        query = query.where(or_(Transaction.method == method, Transaction.to_method == method))
    if order_id:
        query = query.where(Transaction.order_id == order_id)
    if debt_id:
        query = query.where(Transaction.debt_id == debt_id)
    if q:
        like = f"%{q.strip()}%"
        conds = [Transaction.note.ilike(like), Client.name.ilike(like)]
        if q.strip().lstrip("#").isdigit():
            conds.append(Transaction.order_id == int(q.strip().lstrip("#")))
        query = query.where(or_(*conds))
    total = await session.scalar(select(func.count()).select_from(query.subquery()))
    txs = (
        await session.scalars(query.order_by(Transaction.date.desc(), Transaction.id.desc()).limit(limit).offset(offset))
    ).all()
    return {"total": int(total or 0), "items": await _tx_rows(session, list(txs))}


class TxIn(BaseModel):
    kind: str
    amount: int
    method: str = "naqd"
    to_method: str | None = None
    date: dt.date | None = None  # «date» maydon nomi turini yashirmasligi uchun dt.date
    category_id: int | None = None
    note: str | None = None
    client_id: int | None = None
    order_id: int | None = None
    debt_id: int | None = None


@router.post("/transactions")
async def add_transaction(data: TxIn, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    try:
        tx = await create_transaction(
            session,
            kind=data.kind,
            amount=data.amount,
            method=data.method,
            to_method=data.to_method,
            day=data.date,
            category_id=data.category_id,
            note=data.note,
            client_id=data.client_id,
            order_id=data.order_id,
            debt_id=data.debt_id,
            user_id=user.id,
        )
    except FinanceError as exc:
        raise _err(exc) from exc
    await session.commit()
    await _notify()
    if tx.order_id:
        await hub.publish({"type": "order.update", "order_id": tx.order_id})
    return (await _tx_rows(session, [tx]))[0]


def _check_owner(user: User, tx: Transaction) -> None:
    if user.role != "admin" and tx.created_by_id != user.id:
        raise HTTPException(403, "Faqat o'zingiz kiritgan yozuvni o'zgartira olasiz")


@router.patch("/transactions/{tx_id}")
async def edit_transaction(
    tx_id: int, data: TxIn, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    tx = await session.get(Transaction, tx_id)
    if tx is None:
        raise HTTPException(404, "Yozuv topilmadi")
    _check_owner(user, tx)
    old_order, old_debt = tx.order_id, tx.debt_id
    tx.kind = data.kind
    tx.amount = data.amount
    tx.method = data.method
    tx.to_method = data.to_method
    tx.date = data.date or tx.date
    tx.category_id = data.category_id
    tx.note = (data.note or "").strip() or None
    tx.client_id = data.client_id
    tx.order_id = data.order_id
    tx.debt_id = data.debt_id
    try:
        await validate(session, tx)
    except FinanceError as exc:
        await session.rollback()
        raise _err(exc) from exc
    await session.flush()
    for oid in {old_order, tx.order_id}:
        await recalc_order_paid(session, oid)
    for did in {old_debt, tx.debt_id}:
        await refresh_debt(session, did)
    await session.commit()
    await _notify()
    return (await _tx_rows(session, [tx]))[0]


@router.delete("/transactions/{tx_id}")
async def remove_transaction(tx_id: int, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    tx = await session.get(Transaction, tx_id)
    if tx is None:
        raise HTTPException(404, "Yozuv topilmadi")
    _check_owner(user, tx)
    order_id = tx.order_id
    await delete_transaction(session, tx)
    await session.commit()
    await _notify()
    if order_id:
        await hub.publish({"type": "order.update", "order_id": order_id})
    return {"ok": True}


# ---------------- toifalar ----------------


@router.get("/categories")
async def list_categories(_: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    cats = (await session.scalars(select(Category).order_by(Category.kind, Category.position, Category.id))).all()
    used = dict(
        (await session.execute(select(Transaction.category_id, func.count(Transaction.id)).group_by(Transaction.category_id))).all()
    )
    return [{**category_dict(c), "used": int(used.get(c.id, 0))} for c in cats]


class CategoryIn(BaseModel):
    name: str
    kind: str = "chiqim"
    color: str = "#64748b"


@router.post("/categories")
async def create_category(data: CategoryIn, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    if data.kind not in ("kirim", "chiqim") or not data.name.strip():
        raise HTTPException(400, "Toifa nomi va turini kiriting")
    position = (await session.scalar(select(func.max(Category.position))) or 0) + 1
    cat = Category(name=data.name.strip()[:64], kind=data.kind, color=data.color, position=position)
    session.add(cat)
    await session.commit()
    await _notify()
    return category_dict(cat)


@router.patch("/categories/{cat_id}")
async def update_category(
    cat_id: int, data: CategoryIn, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    cat = await session.get(Category, cat_id)
    if cat is None:
        raise HTTPException(404, "Toifa topilmadi")
    if data.name.strip():
        cat.name = data.name.strip()[:64]
    cat.color = data.color
    await session.commit()
    await _notify()
    return category_dict(cat)


@router.delete("/categories/{cat_id}")
async def delete_category(cat_id: int, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    """Toifa o'chiriladi, uning yozuvlari «Toifasiz» bo'lib qoladi. Tizim toifalarini o'chirib bo'lmaydi."""
    cat = await session.get(Category, cat_id)
    if cat is None:
        raise HTTPException(404, "Toifa topilmadi")
    if cat.system_key:
        raise HTTPException(400, "Bu tizim toifasi — uni o'chirib bo'lmaydi (nomini o'zgartirish mumkin)")
    await session.execute(update(Transaction).where(Transaction.category_id == cat_id).values(category_id=None))
    await session.delete(cat)
    await session.commit()
    await _notify()
    return {"ok": True}


# ---------------- qarz daftari ----------------


async def _debt_rows(session: AsyncSession, debts: list[Debt]) -> list[dict]:
    return [debt_dict(d, await debt_paid(session, d)) for d in debts]


@router.get("/debts")
async def list_debts(
    status: str = Query("open", pattern="^(open|closed|all)$"),
    _: User = Depends(staff_manager),
    session: AsyncSession = Depends(get_session),
):
    query = select(Debt)
    if status == "open":
        query = query.where(Debt.closed_at.is_(None))
    elif status == "closed":
        query = query.where(Debt.closed_at.is_not(None))
    debts = (await session.scalars(query.order_by(Debt.due_date.is_(None), Debt.due_date, Debt.id.desc()))).all()
    orders = (
        await session.execute(
            select(Order, Client.name)
            .outerjoin(Client, Client.id == Order.client_id)
            .join(OrderStatus, OrderStatus.id == Order.status_id)
            .where(OrderStatus.is_cancel.is_(False), Order.price > Order.paid)
            .order_by(Order.created_at)
        )
    ).all()
    return {
        "debts": await _debt_rows(session, list(debts)),
        "orders": [
            {
                "order_id": o.id,
                "number": f"#{o.id:04d}",
                "title": o.title,
                "client_id": o.client_id,
                "client_name": name,
                "price": o.price,
                "paid": o.paid,
                "remaining": o.price - o.paid,
                "deadline": o.deadline.isoformat() if o.deadline else None,
            }
            for o, name in orders
        ],
    }


class DebtIn(BaseModel):
    direction: str
    person: str
    phone: str | None = None
    client_id: int | None = None
    amount: int
    note: str | None = None
    due_date: date | None = None
    with_cash: bool = False  # pul berildi (bizga) yoki olindi (bizdan) — kassaga ham yoziladi
    method: str = "naqd"


@router.post("/debts")
async def create_debt(data: DebtIn, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    if data.direction not in ("bizga", "bizdan"):
        raise HTTPException(400, "Qarz turini tanlang")
    if not data.person.strip() or data.amount <= 0:
        raise HTTPException(400, "Kim va qancha ekanini kiriting")
    debt = Debt(
        direction=data.direction,
        person=data.person.strip()[:200],
        phone=data.phone,
        client_id=data.client_id,
        amount=data.amount,
        note=data.note,
        due_date=data.due_date,
        created_by_id=user.id,
    )
    session.add(debt)
    await session.flush()
    if data.with_cash:
        # Qarz berdik — kassadan chiqim; qarz oldik — kassaga kirim (bu qaytarish hisoblanmaydi)
        try:
            await create_transaction(
                session,
                kind="chiqim" if data.direction == "bizga" else "kirim",
                amount=data.amount,
                method=data.method if data.method in PAY_METHODS else "naqd",
                note=f"Qarz: {debt.person}" + (f" — {data.note}" if data.note else ""),
                debt_id=debt.id,
                client_id=data.client_id,
                user_id=user.id,
            )
        except FinanceError as exc:
            raise _err(exc) from exc
    await session.commit()
    await _notify()
    return debt_dict(debt, 0)


class DebtPatch(BaseModel):
    person: str | None = None
    phone: str | None = None
    amount: int | None = None
    note: str | None = None
    due_date: date | None = None


@router.patch("/debts/{debt_id}")
async def update_debt(
    debt_id: int, data: DebtPatch, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    debt = await session.get(Debt, debt_id)
    if debt is None:
        raise HTTPException(404, "Qarz topilmadi")
    fields = data.model_fields_set
    if "person" in fields and data.person and data.person.strip():
        debt.person = data.person.strip()[:200]
    if "phone" in fields:
        debt.phone = data.phone
    if "amount" in fields and data.amount and data.amount > 0:
        debt.amount = data.amount
    if "note" in fields:
        debt.note = data.note
    if "due_date" in fields:
        debt.due_date = data.due_date
    await session.flush()
    await refresh_debt(session, debt.id)
    await session.commit()
    await _notify()
    return debt_dict(debt, await debt_paid(session, debt))


class DebtPayIn(BaseModel):
    amount: int
    method: str = "naqd"
    date: dt.date | None = None
    note: str | None = None


@router.post("/debts/{debt_id}/pay")
async def pay_debt(
    debt_id: int, data: DebtPayIn, user: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    """Qarz qaytarildi (bizga) yoki biz to'ladik (bizdan) — kassaga yoziladi, to'liq bo'lsa yopiladi."""
    debt = await session.get(Debt, debt_id)
    if debt is None:
        raise HTTPException(404, "Qarz topilmadi")
    try:
        await create_transaction(
            session,
            kind=repayment_kind(debt),
            amount=data.amount,
            method=data.method,
            day=data.date,
            note=data.note or f"Qarz qaytarildi: {debt.person}",
            debt_id=debt.id,
            client_id=debt.client_id,
            user_id=user.id,
        )
    except FinanceError as exc:
        raise _err(exc) from exc
    await session.commit()
    await _notify()
    return debt_dict(debt, await debt_paid(session, debt))


@router.delete("/debts/{debt_id}", dependencies=[Depends(admin_only)])
async def delete_debt(debt_id: int, session: AsyncSession = Depends(get_session)):
    """Qarz yozuvi o'chiriladi; kassadagi bog'liq yozuvlar qoladi."""
    debt = await session.get(Debt, debt_id)
    if debt is None:
        raise HTTPException(404, "Qarz topilmadi")
    await session.delete(debt)
    await session.commit()
    await _notify()
    return {"ok": True}


# ---------------- Excel ----------------


@router.get("/export.xlsx")
async def export_xlsx(
    date_from: date | None = None,
    date_to: date | None = None,
    _: User = Depends(staff_manager),
    session: AsyncSession = Depends(get_session),
):
    from openpyxl import Workbook
    from openpyxl.styles import Alignment, Font, PatternFill
    from openpyxl.utils import get_column_letter

    date_from, date_to = _period(date_from, date_to)
    txs = (
        await session.scalars(
            select(Transaction)
            .where(Transaction.date >= date_from, Transaction.date <= date_to)
            .order_by(Transaction.date, Transaction.id)
        )
    ).all()
    rows = await _tx_rows(session, list(txs))
    users = {u.id: u.name for u in (await session.scalars(select(User))).all()}
    money = "#,##0"
    bold = Font(bold=True)
    head_fill = PatternFill("solid", fgColor="E2E8F0")

    wb = Workbook()
    ws = wb.active
    ws.title = "Kirim-chiqim"
    ws.append([f"Kassa: {date_from.strftime('%d.%m.%Y')} — {date_to.strftime('%d.%m.%Y')}"])
    ws["A1"].font = Font(bold=True, size=13)
    header = ["Sana", "Turi", "Toifa", "Izoh", "Mijoz", "Zakaz", "Usul", "Kirim", "Chiqim", "Kim yozdi"]
    ws.append(header)
    for cell in ws[2]:
        cell.font = bold
        cell.fill = head_fill
    total_in = total_out = 0
    for r in rows:
        method = METHOD_LABELS.get(r["method"], r["method"])
        if r["kind"] == "otkazma":
            method = f"{method} → {METHOD_LABELS.get(r['to_method'] or '', r['to_method'] or '')}"
        income = r["amount"] if r["kind"] == "kirim" else None
        expense = r["amount"] if r["kind"] == "chiqim" else None
        total_in += income or 0
        total_out += expense or 0
        ws.append(
            [
                date.fromisoformat(r["date"]),
                KIND_LABELS[r["kind"]],
                r["category_name"] or "",
                r["note"] or "",
                r["client_name"] or "",
                r["order_number"] or "",
                method,
                income,
                expense,
                users.get(r["created_by_id"] or 0, ""),
            ]
        )
    ws.append([])
    ws.append(["Jami", "", "", "", "", "", "", total_in, total_out])
    ws.append(["Foyda (kirim − chiqim)", "", "", "", "", "", "", total_in - total_out])
    for row in ws.iter_rows(min_row=ws.max_row - 1, max_row=ws.max_row):
        for cell in row:
            cell.font = bold
    for row in ws.iter_rows(min_row=3):
        row[0].number_format = "DD.MM.YYYY"
        for cell in row[7:9]:
            cell.number_format = money
    for idx, width in enumerate([12, 10, 18, 40, 22, 9, 16, 15, 15, 16], start=1):
        ws.column_dimensions[get_column_letter(idx)].width = width
    ws.freeze_panes = "A3"

    ws2 = wb.create_sheet("Toifalar bo'yicha")
    ws2.append(["Turi", "Toifa", "Summa"])
    for cell in ws2[1]:
        cell.font = bold
        cell.fill = head_fill
    sums: dict[tuple[str, str], int] = defaultdict(int)
    for r in rows:
        if r["kind"] != "otkazma":
            sums[(KIND_LABELS[r["kind"]], r["category_name"] or "Toifasiz")] += r["amount"]
    for (kind, name), amount in sorted(sums.items(), key=lambda x: (x[0][0], -x[1])):
        ws2.append([kind, name, amount])
        ws2.cell(row=ws2.max_row, column=3).number_format = money
    for idx, width in enumerate([10, 26, 16], start=1):
        ws2.column_dimensions[get_column_letter(idx)].width = width

    ws3 = wb.create_sheet("Qarz daftari")
    ws3.append(["Turi", "Kim", "Telefon", "Summa", "Qaytarilgan", "Qoldiq", "Muddat", "Holat", "Izoh"])
    for cell in ws3[1]:
        cell.font = bold
        cell.fill = head_fill
    for d in (await session.scalars(select(Debt).order_by(Debt.closed_at.is_not(None), Debt.id))).all():
        paid = await debt_paid(session, d)
        ws3.append(
            [
                "Bizdan qarzdor" if d.direction == "bizga" else "Biz qarzdormiz",
                d.person,
                d.phone or "",
                d.amount,
                paid,
                max(d.amount - paid, 0),
                d.due_date,
                "Yopilgan" if d.closed_at else "Ochiq",
                d.note or "",
            ]
        )
    debt_orders = (
        await session.execute(
            select(Order, Client.name)
            .outerjoin(Client, Client.id == Order.client_id)
            .join(OrderStatus, OrderStatus.id == Order.status_id)
            .where(OrderStatus.is_cancel.is_(False), Order.price > Order.paid)
        )
    ).all()
    for o, name in debt_orders:
        ws3.append(
            [f"Zakaz #{o.id:04d}", name or o.title, "", o.price, o.paid, o.price - o.paid, o.deadline, "Ochiq", o.title]
        )
    for row in ws3.iter_rows(min_row=2):
        for cell in row[3:6]:
            cell.number_format = money
        row[6].number_format = "DD.MM.YYYY"
    for idx, width in enumerate([16, 24, 16, 14, 14, 14, 12, 10, 36], start=1):
        ws3.column_dimensions[get_column_letter(idx)].width = width
    for sheet in (ws, ws2, ws3):
        for row in sheet.iter_rows():
            for cell in row:
                cell.alignment = Alignment(vertical="top", wrap_text=cell.column_letter == "D" and sheet is ws)

    buf = io.BytesIO()
    wb.save(buf)
    buf.seek(0)
    name = f"kassa_{date_from.isoformat()}_{date_to.isoformat()}.xlsx"
    return StreamingResponse(
        buf,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f'attachment; filename="{name}"'},
    )


@router.get("/balance")
async def balance_now(_: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    today = local_today()
    return {
        "balances": await balances(session),
        "today": await period_totals(session, today, today),
        "updated_at": utcnow().isoformat(),
    }
