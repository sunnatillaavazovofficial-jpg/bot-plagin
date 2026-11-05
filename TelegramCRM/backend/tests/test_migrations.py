"""Eski baza (0001, payments jadvali bilan) yangi tuzilmaga to'g'ri ko'chishi."""

from __future__ import annotations

import sqlite3

from alembic import command

from app.config import get_settings
from app.migrate import alembic_config


def test_payments_migrate_to_transactions(tmp_path, monkeypatch):
    monkeypatch.setenv("DATA_DIR", str(tmp_path))
    monkeypatch.setenv("SECRET_KEY", "x" * 32)
    get_settings.cache_clear()
    try:
        cfg = alembic_config()
        command.upgrade(cfg, "0001")
        db = sqlite3.connect(get_settings().db_path)
        db.executescript(
            """
            INSERT INTO users (id, name, login, password_hash, role, is_active, created_at)
                VALUES (1, 'A', 'a', 'x', 'admin', 1, '2026-11-01 10:00:00');
            INSERT INTO clients (id, name, created_at) VALUES (1, 'Aziz', '2026-11-01 10:00:00');
            INSERT INTO order_statuses (id, name, color, position, is_done, is_cancel) VALUES (1, 'Yangi', '#000', 0, 0, 0);
            INSERT INTO orders (id, title, description, client_id, status_id, position, price, paid, created_at, updated_at)
                VALUES (1, 'Banner', '', 1, 1, 0, 900000, 500000, '2026-11-01 10:00:00', '2026-11-01 10:00:00');
            INSERT INTO payments (order_id, amount, method, created_at, created_by_id)
                VALUES (1, 600000, 'karta', '2026-11-01 20:30:00', 1), (1, -100000, 'naqd', '2026-11-02 08:00:00', 1);
            INSERT INTO tg_accounts (id, phone, name, color, status, include_groups, created_at)
                VALUES (1, '+1', 'Asosiy', '#000', 'active', 1, '2026-11-01 10:00:00');
            INSERT INTO chats (id, account_id, peer_id, peer_type, title, unread_count, last_message_out, read_outbox_max_id,
                               history_complete, created_at)
                VALUES (1, 1, -100, 'channel', 'Guruh', 0, 0, 0, 0, '2026-11-01 10:00:00');
            """
        )
        db.commit()
        db.close()

        command.upgrade(cfg, "head")
        db = sqlite3.connect(get_settings().db_path)
        rows = db.execute(
            "SELECT t.kind, t.amount, t.date, t.method, c.system_key, t.client_id, t.order_id "
            "FROM transactions t JOIN categories c ON c.id = t.category_id ORDER BY t.id"
        ).fetchall()
        # 20:30 UTC = ertasi kun 01:30 Toshkent vaqti
        assert rows == [
            ("kirim", 600000, "2026-11-02", "karta", "order_payment", 1, 1),
            ("chiqim", 100000, "2026-11-02", "naqd", "refund", 1, 1),
        ]
        assert db.execute("SELECT name FROM sqlite_master WHERE name = 'payments'").fetchone() is None
        assert db.execute("SELECT show_groups, show_private FROM tg_accounts").fetchone() == (1, 1)
        assert db.execute("SELECT kind, visible FROM chats").fetchone() == ("group", 1)
        db.close()
    finally:
        get_settings.cache_clear()
