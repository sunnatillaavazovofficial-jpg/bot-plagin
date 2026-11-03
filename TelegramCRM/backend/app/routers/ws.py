from __future__ import annotations

from urllib.parse import urlparse

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from ..db import SessionLocal
from ..deps import allowed_accounts, user_from_token
from ..security import TOKEN_COOKIE
from ..ws_hub import Connection, hub

router = APIRouter()


@router.websocket("/ws")
async def websocket(ws: WebSocket):
    origin = ws.headers.get("origin")
    if origin and urlparse(origin).netloc != ws.headers.get("host"):
        await ws.close(code=4403)
        return
    async with SessionLocal() as session:
        user = await user_from_token(session, ws.cookies.get(TOKEN_COOKIE))
        if user is None:
            await ws.close(code=4401)
            return
        allowed = await allowed_accounts(session, user)
    await ws.accept()
    conn = Connection(ws=ws, user_id=user.id, role=user.role, account_ids=allowed)
    hub.add(conn)
    try:
        while True:
            text = await ws.receive_text()
            if text == "ping":
                async with conn.lock:
                    await ws.send_text("pong")
    except WebSocketDisconnect:
        pass
    finally:
        hub.remove(conn)
