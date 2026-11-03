"""Brauzerlarga real vaqtda hodisalarni yuborish (WebSocket)."""

from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass, field

from fastapi import WebSocket

log = logging.getLogger(__name__)


@dataclass(eq=False)
class Connection:
    ws: WebSocket
    user_id: int
    role: str
    account_ids: set[int] | None  # None = barcha akkauntlar (admin)
    lock: asyncio.Lock = field(default_factory=asyncio.Lock)


class Hub:
    def __init__(self) -> None:
        self._conns: set[Connection] = set()

    def add(self, conn: Connection) -> None:
        self._conns.add(conn)

    def remove(self, conn: Connection) -> None:
        self._conns.discard(conn)

    def update_access(self, user_id: int, role: str, account_ids: set[int] | None) -> None:
        for conn in self._conns:
            if conn.user_id == user_id:
                conn.role = role
                conn.account_ids = account_ids

    def drop_user(self, user_id: int) -> None:
        for conn in list(self._conns):
            if conn.user_id == user_id:
                asyncio.create_task(self._close(conn))

    async def _close(self, conn: Connection) -> None:
        self.remove(conn)
        try:
            await conn.ws.close()
        except Exception:  # noqa: BLE001
            pass

    async def publish(self, event: dict, account_id: int | None = None) -> None:
        """account_id berilsa — faqat shu akkauntga ruxsati borlarga yuboriladi."""
        targets = [
            c
            for c in self._conns
            if account_id is None or c.account_ids is None or account_id in c.account_ids
        ]
        await asyncio.gather(*(self._send(c, event) for c in targets))

    async def _send(self, conn: Connection, event: dict) -> None:
        try:
            async with conn.lock:
                await asyncio.wait_for(conn.ws.send_json(event), timeout=10)
        except Exception:  # noqa: BLE001
            self.remove(conn)


hub = Hub()
