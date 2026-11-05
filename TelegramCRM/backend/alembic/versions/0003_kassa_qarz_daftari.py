"""kassa (kirim-chiqim), toifalar, qarz daftari; zakaz to'lovlari kassaga ko'chiriladi

Revision ID: 0003
Revises: 0002
Create Date: 2026-11-05
"""
import sqlalchemy as sa
from alembic import op

import app.models

revision = "0003"
down_revision = "0002"
branch_labels = None
depends_on = None

# (nomi, turi, rangi, tizim kaliti)
DEFAULT_CATEGORIES = [
    ("Zakaz to'lovi", "kirim", "#2563eb", "order_payment"),
    ("Avans", "kirim", "#16a34a", None),
    ("Boshlang'ich qoldiq", "kirim", "#64748b", "opening"),
    ("Boshqa kirim", "kirim", "#0891b2", None),
    ("Material", "chiqim", "#ea580c", None),
    ("Ijara", "chiqim", "#7c3aed", None),
    ("Oylik", "chiqim", "#db2777", None),
    ("Transport", "chiqim", "#ca8a04", None),
    ("Reklama", "chiqim", "#0891b2", None),
    ("Kommunal", "chiqim", "#64748b", None),
    ("Qaytarilgan pul", "chiqim", "#dc2626", "refund"),
    ("Boshqa chiqim", "chiqim", "#94a3b8", None),
]


def upgrade() -> None:
    categories = op.create_table(
        "categories",
        sa.Column("id", sa.Integer(), nullable=False),
        sa.Column("name", sa.String(length=64), nullable=False),
        sa.Column("kind", sa.String(length=8), nullable=False),
        sa.Column("color", sa.String(length=16), nullable=False),
        sa.Column("position", sa.Integer(), nullable=False),
        sa.Column("system_key", sa.String(length=32), nullable=True),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("system_key"),
    )
    op.bulk_insert(
        categories,
        [
            {"name": n, "kind": k, "color": c, "position": i, "system_key": key}
            for i, (n, k, c, key) in enumerate(DEFAULT_CATEGORIES)
        ],
    )
    op.create_table(
        "debts",
        sa.Column("id", sa.Integer(), nullable=False),
        sa.Column("direction", sa.String(length=8), nullable=False),
        sa.Column("person", sa.String(length=200), nullable=False),
        sa.Column("phone", sa.String(length=64), nullable=True),
        sa.Column("client_id", sa.Integer(), nullable=True),
        sa.Column("amount", sa.BigInteger(), nullable=False),
        sa.Column("note", sa.Text(), nullable=True),
        sa.Column("due_date", sa.Date(), nullable=True),
        sa.Column("created_at", app.models.UTCDateTime(), nullable=False),
        sa.Column("closed_at", app.models.UTCDateTime(), nullable=True),
        sa.Column("created_by_id", sa.Integer(), nullable=True),
        sa.ForeignKeyConstraint(["client_id"], ["clients.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(["created_by_id"], ["users.id"], ondelete="SET NULL"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_table(
        "transactions",
        sa.Column("id", sa.Integer(), nullable=False),
        sa.Column("kind", sa.String(length=8), nullable=False),
        sa.Column("amount", sa.BigInteger(), nullable=False),
        sa.Column("date", sa.Date(), nullable=False),
        sa.Column("method", sa.String(length=16), nullable=False),
        sa.Column("to_method", sa.String(length=16), nullable=True),
        sa.Column("category_id", sa.Integer(), nullable=True),
        sa.Column("note", sa.Text(), nullable=True),
        sa.Column("client_id", sa.Integer(), nullable=True),
        sa.Column("order_id", sa.Integer(), nullable=True),
        sa.Column("debt_id", sa.Integer(), nullable=True),
        sa.Column("source", sa.String(length=8), nullable=False),
        sa.Column("created_by_id", sa.Integer(), nullable=True),
        sa.Column("created_at", app.models.UTCDateTime(), nullable=False),
        sa.ForeignKeyConstraint(["category_id"], ["categories.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(["client_id"], ["clients.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(["created_by_id"], ["users.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(["debt_id"], ["debts.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(["order_id"], ["orders.id"], ondelete="SET NULL"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_transactions_kind", "transactions", ["kind"])
    op.create_index("ix_transactions_date", "transactions", ["date"])
    op.create_index("ix_transactions_category_id", "transactions", ["category_id"])
    op.create_index("ix_transactions_order_id", "transactions", ["order_id"])
    op.create_index("ix_transactions_debt_id", "transactions", ["debt_id"])

    # Mavjud zakaz to'lovlari kassaga o'tadi (sana — Toshkent vaqti bo'yicha, UTC+5)
    op.execute(
        """
        INSERT INTO transactions (kind, amount, date, method, category_id, note, client_id, order_id,
                                  source, created_by_id, created_at)
        SELECT CASE WHEN p.amount >= 0 THEN 'kirim' ELSE 'chiqim' END,
               ABS(p.amount),
               date(p.created_at, '+5 hours'),
               p.method,
               CASE WHEN p.amount >= 0
                    THEN (SELECT id FROM categories WHERE system_key = 'order_payment')
                    ELSE (SELECT id FROM categories WHERE system_key = 'refund') END,
               p.note, o.client_id, p.order_id, 'order', p.created_by_id, p.created_at
        FROM payments p LEFT JOIN orders o ON o.id = p.order_id
        ORDER BY p.id
        """
    )
    op.drop_table("payments")


def downgrade() -> None:
    op.create_table(
        "payments",
        sa.Column("id", sa.Integer(), nullable=False),
        sa.Column("order_id", sa.Integer(), nullable=False),
        sa.Column("amount", sa.BigInteger(), nullable=False),
        sa.Column("method", sa.String(length=32), nullable=False),
        sa.Column("note", sa.Text(), nullable=True),
        sa.Column("created_at", app.models.UTCDateTime(), nullable=False),
        sa.Column("created_by_id", sa.Integer(), nullable=True),
        sa.ForeignKeyConstraint(["created_by_id"], ["users.id"], ondelete="SET NULL"),
        sa.ForeignKeyConstraint(["order_id"], ["orders.id"], ondelete="CASCADE"),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_payments_order_id", "payments", ["order_id"])
    op.create_index("ix_payments_created_at", "payments", ["created_at"])
    op.execute(
        """
        INSERT INTO payments (order_id, amount, method, note, created_at, created_by_id)
        SELECT order_id, CASE WHEN kind = 'kirim' THEN amount ELSE -amount END, method, note, created_at, created_by_id
        FROM transactions WHERE order_id IS NOT NULL AND kind IN ('kirim', 'chiqim')
        """
    )
    op.drop_table("transactions")
    op.drop_table("debts")
    op.drop_table("categories")
