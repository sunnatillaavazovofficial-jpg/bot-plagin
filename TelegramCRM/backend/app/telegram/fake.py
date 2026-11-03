"""Sinov rejimi (TG_FAKE=1): Telegram'ga ulanmasdan demo akkauntlar va xabarlar bilan ishlaydi."""

from __future__ import annotations

import asyncio
import logging
import random
import secrets
from datetime import timedelta
from pathlib import Path

from sqlalchemy import func, select

from ..config import Settings
from ..db import SessionLocal
from ..models import Chat, Message, TgAccount, utcnow
from ..serializers import account_dict
from ..ws_hub import hub
from . import store
from .base import ACCOUNT_COLORS, BaseTelegram, TelegramError, media_dir_for
from .store import ChatData, MsgData

log = logging.getLogger(__name__)

FAKE_CODE = "12345"
FAKE_PASSWORD = "parol"

DEMO = [
    (
        "Asosiy",
        "+998901112233",
        [
            ("Aziz Karimov", "+998935551122", [
                (False, "Assalomu alaykum, do'konimiz uchun hajmli harf kerak edi"),
                (True, "Va alaykum assalom! Yozuv va o'lchamini yuboring, hisoblab beramiz"),
                (False, "\"NUR MARKET\" 4 metr, oq yorug'lik bilan"),
                (False, "photo"),
            ]),
            ("Dilnoza", "+998977003344", [
                (False, "Salom, vizitkalar 1000 dona qancha bo'ladi?"),
                (True, "Salom! Ikki tomonlama, 350 g qog'ozda — 180 000 so'm"),
                (False, "Yaxshi, maket tayyor, ertaga olib kelaman"),
            ]),
            ("Bekzod (Bek Qurilish)", "+998901234567", [
                (False, "Banner 3x6 m, ertaga kerak bo'ladi, ulgurasizlarmi?"),
            ]),
        ],
    ),
    (
        "Do'kon",
        "+998994445566",
        [
            ("Sardor", "+998931112200", [
                (False, "Lightbox narxini bilsam bo'ladimi?"),
                (True, "1x0.5 m ikki tomonlama — 1 200 000 so'm, 5 kunda tayyor"),
                (False, "Rahmat, o'ylab ko'raman"),
            ]),
            ("Malika Rahimova", "+998909998877", [
                (False, "Kecha olgan stikerlarimiz juda chiroyli chiqdi, rahmat!"),
                (True, "Sizga ham rahmat! Yana kutib qolamiz 😊"),
            ]),
        ],
    ),
    (
        "Ish",
        "+998977778899",
        [
            ("Javohir (Grand Mebel)", "+998946667788", [
                (False, "Peshtaxta uchun maket tayyor bo'ldimi?"),
                (True, "Ha, hozir yuboraman"),
                (True, "document"),
                (False, "Zo'r, tasdiqlayman. Avans qancha?"),
            ]),
        ],
    ),
]

INCOMING = [
    "Assalomu alaykum, narxini bilsam bo'ladimi?",
    "Ishimiz qachon tayyor bo'ladi?",
    "Maketni ko'rdim, rangini biroz to'qroq qilsak",
    "Bugun kelib olib ketsam bo'ladimi?",
    "Avansni kartaga o'tkazdim",
    "Manzilingizni tashlab yuboring",
    "O'lchami 2x1 metr bo'ladi",
    "Rahmat, hammasi zo'r!",
]

SVG_TEMPLATE = """<svg xmlns='http://www.w3.org/2000/svg' width='640' height='360' viewBox='0 0 640 360'>
<rect width='640' height='360' fill='{bg}'/>
<text x='320' y='170' font-family='Arial' font-size='64' font-weight='bold' fill='#fff' text-anchor='middle'>{title}</text>
<text x='320' y='230' font-family='Arial' font-size='26' fill='#fff' text-anchor='middle' opacity='0.85'>demo rasm</text>
</svg>"""


class FakeTelegram(BaseTelegram):
    def __init__(self, settings: Settings) -> None:
        self.settings = settings
        self.pending: dict[str, dict] = {}
        self.connected: set[int] = set()
        self._task: asyncio.Task | None = None

    def is_connected(self, account_id: int) -> bool:
        return account_id in self.connected

    async def start(self) -> None:
        async with SessionLocal() as s:
            count = await s.scalar(select(func.count(TgAccount.id))) or 0
        if count == 0:
            await self._seed()
        async with SessionLocal() as s:
            ids = (await s.scalars(select(TgAccount.id).where(TgAccount.status == "active"))).all()
        self.connected = set(ids)
        if self.settings.fake_interval > 0:
            self._task = asyncio.create_task(self._simulate())

    async def stop(self) -> None:
        if self._task:
            self._task.cancel()

    async def _next_tg_id(self, s, chat_id: int) -> int:
        return await store.max_tg_id(s, chat_id) + 1

    def _demo_media(self, account_id: int, chat_id: int, kind: str, title: str) -> MsgData:
        folder = media_dir_for(self.settings.media_dir, account_id, chat_id)
        if kind == "photo":
            path = folder / f"demo-{secrets.token_hex(4)}.svg"
            path.write_text(SVG_TEMPLATE.format(bg=random.choice(ACCOUNT_COLORS), title=title[:14]))
            return MsgData(0, False, "", utcnow(), media_type="photo", media_mime="image/svg+xml",
                           media_name=path.name, media_size=path.stat().st_size, media_path=str(path))
        path = folder / f"maket-{secrets.token_hex(3)}.txt"
        path.write_text("Demo maket fayli\n")
        return MsgData(0, False, "", utcnow(), media_type="document", media_mime="text/plain",
                       media_name="maket.pdf", media_size=path.stat().st_size, media_path=str(path))

    async def _seed(self) -> None:
        start = utcnow() - timedelta(hours=30)
        peer = 1000
        async with SessionLocal() as s:
            for idx, (name, phone, chats) in enumerate(DEMO):
                acc = TgAccount(phone=phone, name=name, color=ACCOUNT_COLORS[idx], status="active",
                                tg_user_id=900 + idx, session_enc=None, include_groups=False)
                s.add(acc)
                await s.flush()
                for title, chat_phone, messages in chats:
                    peer += 1
                    chat = await store.upsert_chat(
                        s, acc.id, ChatData(peer_id=peer, peer_type="user", title=title, phone=chat_phone, access_hash=1)
                    )
                    chat.history_complete = True
                    for n, (out, text) in enumerate(messages):
                        start += timedelta(minutes=random.randint(7, 90))
                        if text in {"photo", "document"}:
                            data = self._demo_media(acc.id, chat.id, text, title.split()[0])
                            data.out = out
                        else:
                            data = MsgData(0, out, text, start)
                        data.tg_id = n + 1
                        data.date = start
                        await store.save_message(s, chat, data, live=True)
                    chat.read_outbox_max_id = len(messages)
            await s.commit()

    async def _simulate(self) -> None:
        while True:
            await asyncio.sleep(self.settings.fake_interval)
            try:
                async with SessionLocal() as s:
                    chat = await s.scalar(
                        select(Chat).where(Chat.account_id.in_(self.connected or {0})).order_by(func.random()).limit(1)
                    )
                    if chat is None:
                        continue
                    data = MsgData(await self._next_tg_id(s, chat.id), False, random.choice(INCOMING), utcnow())
                    msg, _ = await store.save_message(s, chat, data, live=True)
                    await s.commit()
                await store.publish_message(chat, msg)
            except asyncio.CancelledError:
                raise
            except Exception:  # noqa: BLE001
                log.exception("Demo xabar yaratishda xato")

    # ---------- kirish ----------

    async def login_start(self, phone: str) -> str:
        digits = "".join(ch for ch in phone if ch.isdigit())
        if len(digits) < 7:
            raise TelegramError("Telefon raqam noto'g'ri. Masalan: +998901234567")
        login_id = secrets.token_urlsafe(12)
        self.pending[login_id] = {"phone": "+" + digits, "password": digits.endswith("0")}
        return login_id

    async def login_code(self, login_id: str, code: str) -> dict:
        pending = self.pending.get(login_id)
        if pending is None:
            raise TelegramError("Kirish jarayoni topilmadi yoki eskirgan. Qaytadan boshlang.")
        if code.strip() != FAKE_CODE:
            raise TelegramError(f"Kod noto'g'ri. (Sinov rejimida kod: {FAKE_CODE})")
        if pending["password"]:
            return {"status": "password"}
        return {"status": "ok", "account_id": await self._finish(login_id)}

    async def login_password(self, login_id: str, password: str) -> dict:
        if login_id not in self.pending:
            raise TelegramError("Kirish jarayoni topilmadi yoki eskirgan. Qaytadan boshlang.")
        if password != FAKE_PASSWORD:
            raise TelegramError(f"Ikki bosqichli parol noto'g'ri. (Sinov rejimida: {FAKE_PASSWORD})")
        return {"status": "ok", "account_id": await self._finish(login_id)}

    async def _finish(self, login_id: str) -> int:
        pending = self.pending.pop(login_id)
        async with SessionLocal() as s:
            acc = await s.scalar(select(TgAccount).where(TgAccount.phone == pending["phone"]))
            if acc is None:
                count = await s.scalar(select(func.count(TgAccount.id))) or 0
                acc = TgAccount(phone=pending["phone"], name=f"Akkaunt {pending['phone'][-4:]}",
                                color=ACCOUNT_COLORS[count % len(ACCOUNT_COLORS)], tg_user_id=5000 + count)
                s.add(acc)
                await s.flush()
                chat = await store.upsert_chat(
                    s, acc.id, ChatData(peer_id=20000 + acc.id, peer_type="user", title="Yangi mijoz", phone="+998900000000")
                )
                chat.history_complete = True
                await store.save_message(s, chat, MsgData(1, False, "Salom! Bu yangi ulangan akkaunt.", utcnow()), live=True)
            acc.status = "active"
            acc.last_error = None
            acc.synced_at = utcnow()
            await s.commit()
            payload = account_dict(acc, True)
        self.connected.add(acc.id)
        await hub.publish({"type": "account.update", "account": payload}, account_id=acc.id)
        await hub.publish({"type": "chats.reload", "account_id": acc.id}, account_id=acc.id)
        return acc.id

    async def logout(self, account_id: int) -> None:
        self.connected.discard(account_id)
        async with SessionLocal() as s:
            acc = await s.get(TgAccount, account_id)
            if acc is not None:
                acc.status = "logged_out"
                await s.commit()

    async def detach(self, account_id: int) -> None:
        self.connected.discard(account_id)

    async def resync(self, account_id: int) -> None:
        await hub.publish({"type": "chats.reload", "account_id": account_id}, account_id=account_id)

    # ---------- xabarlar ----------

    async def send_message(
        self,
        chat: Chat,
        text: str,
        *,
        user_id: int,
        reply_to: int | None = None,
        file_path: Path | None = None,
        as_document: bool = False,
    ) -> Message:
        if chat.account_id not in self.connected:
            raise TelegramError("Akkaunt ulanmagan. Sozlamalarda akkauntni qayta ulang.")
        async with SessionLocal() as s:
            db_chat = await s.get(Chat, chat.id)
            assert db_chat is not None
            data = MsgData(await self._next_tg_id(s, chat.id), True, text, utcnow(), reply_to_tg_id=reply_to)
            if file_path is not None:
                mime = _guess_mime(file_path)
                data.media_type = "photo" if mime.startswith("image/") and not as_document else "document"
                data.media_name = file_path.name
                data.media_mime = mime
                data.media_size = file_path.stat().st_size
                data.media_path = str(file_path)
            msg, _ = await store.save_message(s, db_chat, data, live=True, sent_by_user_id=user_id)
            await s.commit()
        await store.publish_message(db_chat, msg)
        return msg

    async def mark_read(self, chat: Chat) -> None:
        return None

    async def download_media(self, message: Message) -> Path:
        if message.media_path and Path(message.media_path).exists():
            return Path(message.media_path)
        raise TelegramError("Fayl topilmadi.")

    async def load_older(self, chat_id: int, limit: int = 50) -> int:
        return 0


def _guess_mime(path: Path) -> str:
    import mimetypes

    return mimetypes.guess_type(path.name)[0] or "application/octet-stream"

