"""Kassa mantig'i: yozuvlar, qoldiqlar, zakaz to'lovlari va qarzlar. Sayt, bot va hisobotlar shu yerdan foydalanadi."""

from __future__ import annotations

from datetime import date

from sqlalchemy import case, func, select
from sqlalchemy.ext.asyncio import AsyncSession

from .models import PAY_METHODS, TX_KINDS, Category, Debt, Order, Transaction, utcnow
from .timeutil import local_today

METHOD_LABELS = {"naqd": "Naqd", "karta": "Karta", "o'tkazma": "Bank o'tkazmasi", "boshqa": "Boshqa"}
KIND_LABELS = {"kirim": "Kirim", "chiqim": "Chiqim", "otkazma": "O'tkazma"}


class FinanceError(ValueError):
    """Foydalanuvchiga ko'rsatiladigan xato (o'zbekcha)."""


def fmt_sum(value: int) -> str:
    return f"{value:,}".replace(",", " ") + " so'm"


async def system_category(session: AsyncSession, key: str) -> Category | None:
    return await session.scalar(select(Category).where(Category.system_key == key))


async def default_category(session: AsyncSession, kind: str) -> Category | None:
    """«Boshqa kirim» / «Boshqa chiqim» yoki shu turdagi birinchi toifa."""
    name = "Boshqa kirim" if kind == "kirim" else "Boshqa chiqim"
    cat = await session.scalar(select(Category).where(Category.kind == kind, Category.name == name))
    if cat is None:
        cat = await session.scalar(
            select(Category).where(Category.kind == kind, Category.system_key.is_(None)).order_by(Category.position)
        )
    return cat


async def recalc_order_paid(session: AsyncSession, order_id: int | None) -> None:
    if not order_id:
        return
    order = await session.get(Order, order_id)
    if order is None:
        return
    total = await session.scalar(
        select(
            func.coalesce(
                func.sum(case((Transaction.kind == "kirim", Transaction.amount), else_=-Transaction.amount)), 0
            )
        ).where(Transaction.order_id == order_id, Transaction.kind.in_(("kirim", "chiqim")))
    )
    order.paid = int(total or 0)


def repayment_kind(debt: Debt) -> str:
    """Bizga qarz qaytarilsa — kirim; biz qarzni to'lasak — chiqim."""
    return "kirim" if debt.direction == "bizga" else "chiqim"


async def debt_paid(session: AsyncSession, debt: Debt) -> int:
    total = await session.scalar(
        select(func.coalesce(func.sum(Transaction.amount), 0)).where(
            Transaction.debt_id == debt.id, Transaction.kind == repayment_kind(debt)
        )
    )
    return int(total or 0)


async def refresh_debt(session: AsyncSession, debt_id: int | None) -> None:
    if not debt_id:
        return
    debt = await session.get(Debt, debt_id)
    if debt is None:
        return
    paid = await debt_paid(session, debt)
    if paid >= debt.amount and debt.closed_at is None:
        debt.closed_at = utcnow()
    elif paid < debt.amount and debt.closed_at is not None:
        debt.closed_at = None


async def validate(session: AsyncSession, tx: Transaction) -> None:
    if tx.kind not in TX_KINDS:
        raise FinanceError("Yozuv turi noto'g'ri")
    if not tx.amount or tx.amount <= 0:
        raise FinanceError("Summani kiriting (0 dan katta)")
    if tx.amount > 10**13:
        raise FinanceError("Summa juda katta")
    if tx.method not in PAY_METHODS:
        raise FinanceError("To'lov usuli noto'g'ri")
    if tx.kind == "otkazma":
        if tx.to_method not in PAY_METHODS or tx.to_method == tx.method:
            raise FinanceError("O'tkazma uchun qayerdan va qayerga ekanini tanlang (masalan naqd → karta)")
        tx.category_id = None
        tx.order_id = None
        tx.debt_id = None
    else:
        tx.to_method = None
        if tx.category_id is not None:
            cat = await session.get(Category, tx.category_id)
            if cat is None:
                raise FinanceError("Toifa topilmadi")
            if cat.kind != tx.kind:
                raise FinanceError("Toifa yozuv turiga mos emas")
    if tx.order_id is not None:
        order = await session.get(Order, tx.order_id)
        if order is None:
            raise FinanceError("Zakaz topilmadi")
        if tx.client_id is None:
            tx.client_id = order.client_id
    if tx.debt_id is not None and await session.get(Debt, tx.debt_id) is None:
        raise FinanceError("Qarz yozuvi topilmadi")


async def create_transaction(
    session: AsyncSession,
    *,
    kind: str,
    amount: int,
    method: str = "naqd",
    user_id: int | None,
    day: date | None = None,
    category_id: int | None = None,
    note: str | None = None,
    order_id: int | None = None,
    debt_id: int | None = None,
    client_id: int | None = None,
    to_method: str | None = None,
    source: str = "site",
) -> Transaction:
    tx = Transaction(
        kind=kind,
        amount=amount,
        method=method,
        to_method=to_method,
        date=day or local_today(),
        category_id=category_id,
        note=(note or "").strip() or None,
        order_id=order_id,
        debt_id=debt_id,
        client_id=client_id,
        source=source,
        created_by_id=user_id,
    )
    if tx.kind != "otkazma" and tx.category_id is None:
        if order_id is not None:
            key = "order_payment" if kind == "kirim" else "refund"
            cat = await system_category(session, key)
        else:
            cat = await default_category(session, kind)
        tx.category_id = cat.id if cat else None
    await validate(session, tx)
    session.add(tx)
    await session.flush()
    await recalc_order_paid(session, tx.order_id)
    await refresh_debt(session, tx.debt_id)
    return tx


async def delete_transaction(session: AsyncSession, tx: Transaction) -> None:
    order_id, debt_id = tx.order_id, tx.debt_id
    await session.delete(tx)
    await session.flush()
    await recalc_order_paid(session, order_id)
    await refresh_debt(session, debt_id)


async def balances(session: AsyncSession, until: date | None = None) -> dict[str, int]:
    """Kassa qoldig'i usullar bo'yicha (butun vaqt yoki until sanasigacha)."""
    result = {m: 0 for m in PAY_METHODS}
    query = select(Transaction.kind, Transaction.method, Transaction.to_method, func.sum(Transaction.amount)).group_by(
        Transaction.kind, Transaction.method, Transaction.to_method
    )
    if until is not None:
        query = query.where(Transaction.date <= until)
    for kind, method, to_method, total in (await session.execute(query)).all():
        total = int(total or 0)
        if kind == "kirim":
            result[method] = result.get(method, 0) + total
        elif kind == "chiqim":
            result[method] = result.get(method, 0) - total
        else:
            result[method] = result.get(method, 0) - total
            if to_method:
                result[to_method] = result.get(to_method, 0) + total
    result["jami"] = sum(v for k, v in result.items() if k in PAY_METHODS)
    return result


async def period_totals(session: AsyncSession, date_from: date, date_to: date) -> dict[str, int]:
    """Davr kirimi, chiqimi va foydasi. Boshlang'ich qoldiq va o'tkazmalar daromad hisoblanmaydi."""
    opening = await system_category(session, "opening")
    query = select(Transaction.kind, func.coalesce(func.sum(Transaction.amount), 0)).where(
        Transaction.date >= date_from, Transaction.date <= date_to, Transaction.kind.in_(("kirim", "chiqim"))
    )
    if opening is not None:
        query = query.where((Transaction.category_id.is_(None)) | (Transaction.category_id != opening.id))
    rows = dict((await session.execute(query.group_by(Transaction.kind))).all())
    kirim, chiqim = int(rows.get("kirim", 0)), int(rows.get("chiqim", 0))
    return {"kirim": kirim, "chiqim": chiqim, "foyda": kirim - chiqim}
