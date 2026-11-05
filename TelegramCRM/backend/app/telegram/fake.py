"""Sinov rejimi (TG_FAKE=1): Telegram'ga ulanmasdan demo akkauntlar va xabarlar bilan ishlaydi."""

from __future__ import annotations

import asyncio
import json
import logging
import random
import secrets
from datetime import timedelta
from pathlib import Path

from sqlalchemy import func, select

from ..config import Settings
from ..db import SessionLocal
from ..models import Chat, Folder, Message, TgAccount, utcnow
from ..serializers import account_dict
from ..ws_hub import hub
from . import store
from .base import ACCOUNT_COLORS, BaseTelegram, TelegramError, media_dir_for
from .store import ChatData, MsgData

log = logging.getLogger(__name__)

FAKE_CODE = "12345"
FAKE_PASSWORD = "parol"

# (nomi, telefon, tur, ko'rinadimi, xabarlar). Xabar: (chiquvchimi, matn[, yuboruvchi]) yoki maxsus "photo"/"document"/"buttons"
DEMO = [
    (
        "Asosiy",
        "+998901112233",
        [
            ("Aziz Karimov", "+998935551122", "private", True, [
                (False, "Assalomu alaykum, do'konimiz uchun hajmli harf kerak edi"),
                (True, "Va alaykum assalom! Yozuv va o'lchamini yuboring, hisoblab beramiz"),
                (False, "\"NUR MARKET\" 4 metr, oq yorug'lik bilan"),
                (False, "photo"),
            ]),
            ("Dilnoza", "+998977003344", "private", True, [
                (False, "Salom, vizitkalar 1000 dona qancha bo'ladi?"),
                (True, "Salom! Ikki tomonlama, 350 g qog'ozda — 180 000 so'm"),
                (False, "Yaxshi, maket tayyor, ertaga olib kelaman"),
            ]),
            ("Bekzod (Bek Qurilish)", "+998901234567", "private", True, [
                (False, "Banner 3x6 m, ertaga kerak bo'ladi, ulgurasizlarmi?"),
            ]),
            ("Reklama ustalari", None, "group", True, [
                (False, "Kimda 3 mm oq akril bor?", "Jasur"),
                (False, "Bizda bor, 1 list 450 ming", "Sherzod"),
                (True, "Menga 2 list kerak, ertaga olib ketaman"),
                (False, "Kelishdik 👍", "Sherzod"),
            ]),
            ("To'lov boti", None, "bot", True, [
                (True, "/start"),
                (False, "buttons"),
            ]),
            ("Oila", None, "group", False, [
                (False, "Kechqurun hammamiz uyda bo'lamizmi?", "Onam"),
            ]),
        ],
    ),
    (
        "Do'kon",
        "+998994445566",
        [
            ("Sardor", "+998931112200", "private", True, [
                (False, "Lightbox narxini bilsam bo'ladimi?"),
                (True, "1x0.5 m ikki tomonlama — 1 200 000 so'm, 5 kunda tayyor"),
                (False, "Rahmat, o'ylab ko'raman"),
            ]),
            ("Malika Rahimova", "+998909998877", "private", True, [
                (False, "Kecha olgan stikerlarimiz juda chiroyli chiqdi, rahmat!"),
                (True, "Sizga ham rahmat! Yana kutib qolamiz 😊"),
            ]),
            ("Reklama materiallari (narxlar)", None, "channel", True, [
                (False, "Bugungi narxlar: akril 3 mm — 450 000, PVX 5 mm — 280 000, LED modul — 3 500 dona"),
                (False, "Yangi partiya: oq va sutrang akril keldi"),
            ]),
            ("Do'kon kanali", None, "channel_own", True, [
                (True, "Yangi ish: «NUR MARKET» hajmli harflar o'rnatildi ✅"),
            ]),
            ("Futbol yangiliklari", None, "channel", False, [
                (False, "Bugun kechqurun o'yin bor"),
            ]),
        ],
    ),
    (
        "Ish",
        "+998977778899",
        [
            ("Javohir (Grand Mebel)", "+998946667788", "private", True, [
                (False, "Peshtaxta uchun maket tayyor bo'ldimi?"),
                (True, "Ha, hozir yuboraman"),
                (True, "document"),
                (False, "Zo'r, tasdiqlayman. Avans qancha?"),
            ]),
            ("Ustalar guruhi", None, "group", True, [
                (False, "Ertaga Chilonzorda montaj, 9:00 da yig'ilamiz", "Brigadir"),
                (False, "Narvon kimda?", "Otabek"),
            ]),
        ],
    ),
]

DEMO_FOLDERS = [("Mijozlar", "#2563eb"), ("Ta'minotchilar", "#ea580c")]
DEMO_IN_FOLDER = {"Aziz Karimov": 0, "Dilnoza": 0, "Sardor": 0, "Reklama ustalari": 1, "Reklama materiallari (narxlar)": 1}

BOT_BUTTONS = json.dumps(
    [
        [{"text": "💰 Balans", "kind": "callback"}, {"text": "📄 Oxirgi to'lovlar", "kind": "callback"}],
        [{"text": "Saytga o'tish", "kind": "url", "url": "https://example.com"}],
    ],
    ensure_ascii=False,
)

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
            folders = []
            for pos, (fname, fcolor) in enumerate(DEMO_FOLDERS):
                folder = Folder(name=fname, color=fcolor, position=pos)
                s.add(folder)
                folders.append(folder)
            for idx, (name, phone, chats) in enumerate(DEMO):
                acc = TgAccount(phone=phone, name=name, color=ACCOUNT_COLORS[idx], status="active",
                                tg_user_id=900 + idx, session_enc=None)
                s.add(acc)
                await s.flush()
                for title, chat_phone, kind, visible, messages in chats:
                    peer += 1
                    own_channel = kind == "channel_own"
                    kind = "channel" if own_channel else kind
                    chat = await store.upsert_chat(
                        s,
                        acc.id,
                        ChatData(
                            peer_id=peer if kind in ("private", "bot") else -peer,
                            peer_type="user" if kind in ("private", "bot") else "channel",
                            title=title,
                            phone=chat_phone,
                            access_hash=1,
                            kind=kind,
                            can_send=kind != "channel" or own_channel,
                        ),
                    )
                    chat.visible = visible
                    chat.history_complete = True
                    if title in DEMO_IN_FOLDER:
                        chat.folder_id = folders[DEMO_IN_FOLDER[title]].id
                    for n, item in enumerate(messages):
                        out, text = item[0], item[1]
                        start += timedelta(minutes=random.randint(7, 90))
                        if text in {"photo", "document"}:
                            data = self._demo_media(acc.id, chat.id, text, title.split()[0])
                            data.out = out
                        elif text == "buttons":
                            data = MsgData(0, out, "Assalomu alaykum! Hisobingiz: 1 250 000 so'm.\nKerakli bo'limni tanlang:", start,
                                           buttons=BOT_BUTTONS)
                        else:
                            data = MsgData(0, out, text, start, sender_name=item[2] if len(item) > 2 else None)
                        data.tg_id = n + 1
                        data.date = start
                        if chat.visible:
                            await store.save_message(s, chat, data, live=True)
                        else:
                            store.apply_preview(chat, data, live=True)
                    chat.read_outbox_max_id = len(messages)
                    chat.muted = kind == "channel"
            await s.commit()
        if self.settings.demo_finance:
            await seed_demo_finance()

    async def _simulate(self) -> None:
        while True:
            await asyncio.sleep(self.settings.fake_interval)
            try:
                async with SessionLocal() as s:
                    chat = await s.scalar(
                        select(Chat)
                        .where(
                            Chat.account_id.in_(self.connected or {0}),
                            Chat.visible.is_(True),
                            Chat.kind.in_(("private", "group")),
                        )
                        .order_by(func.random())
                        .limit(1)
                    )
                    if chat is None:
                        continue
                    sender = random.choice(["Jasur", "Sherzod", "Otabek"]) if chat.kind == "group" else None
                    data = MsgData(await self._next_tg_id(s, chat.id), False, random.choice(INCOMING), utcnow(),
                                   sender_name=sender)
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
                                color=ACCOUNT_COLORS[count % len(ACCOUNT_COLORS)], tg_user_id=5000 + count,
                                show_private=True, show_groups=False, show_channels=False, show_bots=False)
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
        if not chat.can_send:
            raise TelegramError("Bu chatga yozish huquqi yo'q.")
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

    async def click_button(self, message: Message, row: int, col: int) -> str | None:
        rows = json.loads(message.buttons or "[]")
        try:
            button = rows[row][col]
        except (IndexError, TypeError) as exc:
            raise TelegramError("Tugma topilmadi.") from exc
        reply = {
            "💰 Balans": "Balansingiz: 1 250 000 so'm",
            "📄 Oxirgi to'lovlar": "Oxirgi to'lovlar:\n• 03.11 — 500 000 so'm\n• 01.11 — 750 000 so'm",
        }.get(button["text"], f"«{button['text']}» bo'limi")
        async with SessionLocal() as s:
            chat = await s.get(Chat, message.chat_id)
            assert chat is not None
            data = MsgData(await self._next_tg_id(s, chat.id), False, reply, utcnow())
            msg, _ = await store.save_message(s, chat, data, live=True)
            await s.commit()
        await store.publish_message(chat, msg)
        return None


def _guess_mime(path: Path) -> str:
    import mimetypes

    return mimetypes.guess_type(path.name)[0] or "application/octet-stream"


async def seed_demo_finance() -> None:
    """Demo kassa: boshlang'ich qoldiq, bir necha kirim-chiqim va qarzlar."""
    from sqlalchemy import select as _select

    from ..finance import create_transaction
    from ..models import Category, Debt, Transaction
    from ..timeutil import local_today

    async with SessionLocal() as s:
        if await s.scalar(_select(func.count(Transaction.id))):
            return
        cats = {c.name: c.id for c in (await s.scalars(_select(Category))).all()}
        today = local_today()
        entries = [
            (20, "kirim", 6_000_000, "naqd", "Boshlang'ich qoldiq", "Oy boshidagi kassa"),
            (18, "chiqim", 1_350_000, "naqd", "Material", "Akril 3 mm, 3 list"),
            (15, "chiqim", 4_000_000, "karta", "Ijara", "Ustaxona ijarasi"),
            (12, "kirim", 2_500_000, "karta", "Avans", "Grand Mebel — peshtaxta avansi"),
            (9, "chiqim", 380_000, "naqd", "Transport", "Montajga Damas"),
            (6, "kirim", 1_200_000, "naqd", "Boshqa kirim", "Vizitka va stikerlar"),
            (4, "chiqim", 640_000, "naqd", "Material", "LED modul 180 dona"),
            (2, "chiqim", 3_000_000, "naqd", "Oylik", "Ustalarga avans"),
            (1, "kirim", 800_000, "karta", "Avans", "Bek Qurilish — banner"),
        ]
        for days_ago, kind, amount, method, cat, note in entries:
            await create_transaction(
                s,
                kind=kind,
                amount=amount,
                method=method,
                day=today - timedelta(days=days_ago),
                category_id=cats.get(cat),
                note=note,
                user_id=1,
            )
        await create_transaction(s, kind="otkazma", amount=1_000_000, method="naqd", to_method="karta",
                                 day=today - timedelta(days=3), note="Kartaga o'tkazildi", user_id=1)
        s.add(Debt(direction="bizga", person="Anvar aka (Chilonzor)", phone="+998901112244", amount=1_500_000,
                   note="Stend uchun nasiya", due_date=today + timedelta(days=5), created_by_id=1))
        s.add(Debt(direction="bizdan", person="Akril ta'minotchi", phone="+998935553311", amount=2_400_000,
                   note="Akril va PVX nasiyaga olindi", due_date=today - timedelta(days=1), created_by_id=1))
        await s.commit()
