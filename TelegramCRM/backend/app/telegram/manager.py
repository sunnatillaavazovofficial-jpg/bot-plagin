"""Haqiqiy Telegram akkauntlar bilan ishlash (Telethon, MTProto)."""

from __future__ import annotations

import asyncio
import logging
import secrets
import time
from dataclasses import dataclass
from pathlib import Path

from sqlalchemy import func, select
from telethon import TelegramClient, events, errors, types, utils
from telethon.sessions import StringSession

from ..config import Settings
from ..db import SessionLocal
from ..models import Chat, Message, TgAccount, utcnow
from ..security import decrypt, encrypt
from ..serializers import account_dict
from ..ws_hub import hub
from . import store
from .base import ACCOUNT_COLORS, BaseTelegram, TelegramError, media_dir_for
from .store import ChatData, MsgData

log = logging.getLogger(__name__)

SERVICE_USER_ID = 777000  # Telegram xizmat xabarlari (kirish kodlari) — xavfsizlik uchun ko'rsatilmaydi
DIALOGS_LIMIT = 300
FIRST_SYNC_MESSAGES = 30
CATCHUP_MESSAGES = 100
PENDING_TTL = 15 * 60
WATCHDOG_INTERVAL = 300


@dataclass
class PendingLogin:
    client: TelegramClient
    phone: str
    phone_code_hash: str
    created: float


def _human_error(exc: Exception) -> str:
    if isinstance(exc, errors.FloodWaitError):
        return f"Telegram cheklov qo'ydi: {exc.seconds} soniyadan keyin qayta urinib ko'ring."
    if isinstance(exc, errors.PhoneNumberInvalidError):
        return "Telefon raqam noto'g'ri. Masalan: +998901234567"
    if isinstance(exc, errors.PhoneNumberBannedError):
        return "Bu raqam Telegram tomonidan bloklangan."
    if isinstance(exc, errors.PhoneCodeInvalidError):
        return "Kod noto'g'ri."
    if isinstance(exc, errors.PhoneCodeExpiredError):
        return "Kod eskirgan. Qaytadan boshlang."
    if isinstance(exc, errors.PasswordHashInvalidError):
        return "Ikki bosqichli parol noto'g'ri."
    if isinstance(exc, errors.ChatWriteForbiddenError):
        return "Bu chatga yozish taqiqlangan."
    if isinstance(exc, errors.UserIsBlockedError):
        return "Bu foydalanuvchi sizni bloklagan."
    if isinstance(exc, errors.PeerIdInvalidError):
        return "Chat topilmadi (Telegram'da o'chirilgan bo'lishi mumkin)."
    return f"Telegram xatosi: {exc}"


def _media_type(m) -> str | None:
    if m.media is None or isinstance(m.media, types.MessageMediaWebPage):
        return None
    if m.photo:
        return "photo"
    if m.sticker:
        return "sticker"
    if m.gif:
        return "gif"
    if m.voice:
        return "voice"
    if m.video or m.video_note:
        return "video"
    if m.audio:
        return "audio"
    if m.document:
        return "document"
    if m.geo:
        return "location"
    if m.contact:
        return "contact"
    return "other"


def msg_data(m, sender_name: str | None = None) -> MsgData:
    media_type = _media_type(m)
    text = m.message or ""
    if media_type == "contact" and m.contact:
        contact = m.contact
        text = f"{contact.first_name or ''} {contact.last_name or ''} {contact.phone_number or ''}".strip()
    if media_type == "location" and m.geo:
        text = f"https://maps.google.com/?q={m.geo.lat},{m.geo.long}"
    file = m.file if media_type in {"photo", "document", "video", "voice", "audio", "gif", "sticker"} else None
    return MsgData(
        tg_id=m.id,
        out=bool(m.out),
        text=text,
        date=m.date,
        media_type=media_type,
        media_name=(file.name if file else None),
        media_mime=(file.mime_type if file else None),
        media_size=(file.size if file else None),
        sender_name=sender_name,
        reply_to_tg_id=m.reply_to_msg_id,
    )


def chat_data(entity, input_peer=None) -> ChatData:
    access_hash = getattr(input_peer, "access_hash", None) or getattr(entity, "access_hash", None)
    if isinstance(entity, types.User):
        peer_type = "user"
        phone = f"+{entity.phone}" if entity.phone else None
    elif isinstance(entity, types.Chat):
        peer_type, phone = "chat", None
    else:
        peer_type, phone = "channel", None
    title = utils.get_display_name(entity) or ("O'chirilgan akkaunt" if getattr(entity, "deleted", False) else "")
    return ChatData(
        peer_id=utils.get_peer_id(entity),
        peer_type=peer_type,
        title=title,
        access_hash=access_hash,
        username=getattr(entity, "username", None),
        phone=phone,
    )


def input_peer(chat: Chat):
    real_id, peer_cls = utils.resolve_id(chat.peer_id)
    if peer_cls is types.PeerUser:
        return types.InputPeerUser(real_id, chat.access_hash or 0)
    if peer_cls is types.PeerChat:
        return types.InputPeerChat(real_id)
    return types.InputPeerChannel(real_id, chat.access_hash or 0)


class TelegramManager(BaseTelegram):
    def __init__(self, settings: Settings) -> None:
        self.settings = settings
        self.clients: dict[int, TelegramClient] = {}
        self.me_ids: dict[int, int] = {}
        self.include_groups: dict[int, bool] = {}
        self.pending: dict[str, PendingLogin] = {}
        self._tasks: set[asyncio.Task] = set()
        self._media_locks: dict[int, asyncio.Lock] = {}
        self._sync_running: set[int] = set()

    # ---------- umumiy ----------

    def _new_client(self, session: str = "") -> TelegramClient:
        return TelegramClient(
            StringSession(session),
            self.settings.tg_api_id,
            self.settings.tg_api_hash,
            device_model="TelegramCRM",
            system_version="Server",
            app_version="1.0",
            lang_code="uz",
            system_lang_code="uz",
            flood_sleep_threshold=120,
            catch_up=True,
        )

    def _spawn(self, coro) -> None:
        task = asyncio.create_task(coro)
        self._tasks.add(task)
        task.add_done_callback(self._tasks.discard)

    def is_connected(self, account_id: int) -> bool:
        client = self.clients.get(account_id)
        return bool(client and client.is_connected())

    def _client(self, account_id: int) -> TelegramClient:
        client = self.clients.get(account_id)
        if client is None:
            raise TelegramError("Akkaunt ulanmagan. Sozlamalarda akkauntni qayta ulang.")
        return client

    def _accept(self, account_id: int, entity) -> bool:
        if isinstance(entity, types.User):
            return not (
                entity.bot
                or entity.is_self
                or entity.id == SERVICE_USER_ID
                or entity.id == self.me_ids.get(account_id)
            )
        if not self.include_groups.get(account_id):
            return False
        if isinstance(entity, types.Chat):
            return not getattr(entity, "deactivated", False)
        if isinstance(entity, types.Channel):
            return bool(entity.megagroup)
        return False

    async def _set_status(self, account_id: int, status: str, error: str | None = None) -> None:
        async with SessionLocal() as s:
            acc = await s.get(TgAccount, account_id)
            if acc is None:
                return
            acc.status = status
            acc.last_error = error
            await s.commit()
            await hub.publish(
                {"type": "account.update", "account": account_dict(acc, self.is_connected(account_id))},
                account_id=account_id,
            )

    # ---------- ishga tushirish ----------

    async def start(self) -> None:
        async with SessionLocal() as s:
            accounts = (
                await s.scalars(select(TgAccount).where(TgAccount.status != "logged_out"))
            ).all()
        for acc in accounts:
            self._spawn(self._start_account(acc.id, acc.session_enc, acc.include_groups))
        self._spawn(self._watchdog())

    async def _start_account(self, account_id: int, session_enc: str | None, include_groups: bool) -> None:
        session = decrypt(session_enc) if session_enc else None
        if not session:
            await self._set_status(account_id, "error", "Sessiya o'qilmadi (SECRET_KEY o'zgarganmi?). Qayta ulang.")
            return
        client = self._new_client(session)
        try:
            await client.connect()
            if not await client.is_user_authorized():
                await client.disconnect()
                await self._set_status(
                    account_id, "error", "Sessiya bekor qilingan (telefondan chiqarilgan). Akkauntni qayta ulang."
                )
                return
            me = await client.get_me()
        except Exception as exc:  # noqa: BLE001
            log.exception("Akkaunt %s ulanmadi", account_id)
            await self._set_status(account_id, "error", _human_error(exc))
            return
        self._attach(account_id, client, include_groups, me.id)
        await self._set_status(account_id, "active")
        self._spawn(self._sync(account_id))

    def _attach(self, account_id: int, client: TelegramClient, include_groups: bool, me_id: int) -> None:
        old = self.clients.get(account_id)
        if old is not None and old is not client:
            self._spawn(old.disconnect())
        self.clients[account_id] = client
        self.me_ids[account_id] = me_id
        self.include_groups[account_id] = include_groups
        self._register_handlers(account_id, client)

    async def stop(self) -> None:
        for task in list(self._tasks):
            task.cancel()
        for client in list(self.clients.values()):
            try:
                await client.disconnect()
            except Exception:  # noqa: BLE001
                pass
        for pending in self.pending.values():
            try:
                await pending.client.disconnect()
            except Exception:  # noqa: BLE001
                pass
        self.clients.clear()
        self.pending.clear()

    async def _watchdog(self) -> None:
        while True:
            await asyncio.sleep(WATCHDOG_INTERVAL)
            for account_id, client in list(self.clients.items()):
                try:
                    if not client.is_connected():
                        await client.connect()
                    if not await client.is_user_authorized():
                        self.clients.pop(account_id, None)
                        await client.disconnect()
                        await self._set_status(
                            account_id, "error", "Sessiya bekor qilingan (telefondan chiqarilgan). Qayta ulang."
                        )
                except Exception:  # noqa: BLE001
                    log.warning("Watchdog: akkaunt %s tekshiruvida xato", account_id, exc_info=True)

    def account_settings_changed(self, account_id: int, include_groups: bool) -> None:
        self.include_groups[account_id] = include_groups

    # ---------- hodisalar ----------

    def _register_handlers(self, account_id: int, client: TelegramClient) -> None:
        async def on_new(event):
            try:
                await self._on_new(account_id, event)
            except Exception:  # noqa: BLE001
                log.exception("Yangi xabarni saqlashda xato")

        async def on_edit(event):
            try:
                async with SessionLocal() as s:
                    res = await store.mark_edited(s, account_id, event.chat_id, event.message.id, event.message.message)
                    await s.commit()
                if res:
                    await store.publish_message(res[0], res[1], "message.edit")
            except Exception:  # noqa: BLE001
                log.exception("Tahrirlangan xabarda xato")

        async def on_delete(event):
            try:
                async with SessionLocal() as s:
                    rows = await store.mark_deleted(s, account_id, list(event.deleted_ids), event.chat_id)
                    await s.commit()
                for chat, msg in rows:
                    await store.publish_message(chat, msg, "message.edit")
            except Exception:  # noqa: BLE001
                log.exception("O'chirilgan xabarda xato")

        async def on_read(event):
            try:
                await self._on_read(account_id, event)
            except Exception:  # noqa: BLE001
                log.exception("O'qilganlik hodisasida xato")

        client.add_event_handler(on_new, events.NewMessage())
        client.add_event_handler(on_edit, events.MessageEdited())
        client.add_event_handler(on_delete, events.MessageDeleted())
        client.add_event_handler(on_read, events.MessageRead(inbox=True))
        client.add_event_handler(on_read, events.MessageRead(inbox=False))

    async def _on_new(self, account_id: int, event) -> None:
        m = event.message
        if getattr(m, "action", None) is not None:
            return
        entity = await event.get_chat()
        if entity is None or not self._accept(account_id, entity):
            return
        sender_name = None
        if not event.is_private:
            sender = await event.get_sender()
            sender_name = utils.get_display_name(sender) if sender else None
        peer = await event.get_input_chat()
        async with SessionLocal() as s:
            chat = await store.upsert_chat(s, account_id, chat_data(entity, peer))
            msg, created = await store.save_message(s, chat, msg_data(m, sender_name), live=True)
            await s.commit()
        if created:
            await store.publish_message(chat, msg)

    async def _on_read(self, account_id: int, event) -> None:
        peer_id = event.chat_id
        if peer_id is None:
            return
        async with SessionLocal() as s:
            chat = await store.get_chat_by_peer(s, account_id, peer_id)
            if chat is None:
                return
            if event.inbox:
                remaining = await s.scalar(
                    select(func.count(Message.id)).where(
                        Message.chat_id == chat.id, Message.out.is_(False), Message.tg_id > (event.max_id or 0)
                    )
                )
                chat.unread_count = int(remaining or 0)
            else:
                await store.set_read_outbox(s, chat, event.max_id or 0)
            await s.commit()
        await store.publish_chat(chat)

    # ---------- sinxronlash ----------

    async def resync(self, account_id: int) -> None:
        self._client(account_id)
        self._spawn(self._sync(account_id))

    async def _sync(self, account_id: int) -> None:
        if account_id in self._sync_running:
            return
        self._sync_running.add(account_id)
        try:
            client = self._client(account_id)
            fetched = 0
            async for dialog in client.iter_dialogs(limit=DIALOGS_LIMIT):
                entity = dialog.entity
                if not self._accept(account_id, entity):
                    continue
                async with SessionLocal() as s:
                    chat = await store.upsert_chat(s, account_id, chat_data(entity, dialog.input_entity))
                    last = await store.max_tg_id(s, chat.id)
                    top = dialog.message.id if dialog.message else 0
                    if top > last:
                        limit = FIRST_SYNC_MESSAGES if last == 0 else CATCHUP_MESSAGES
                        msgs = await client.get_messages(dialog.input_entity, limit=limit, min_id=last)
                        for m in reversed(msgs):
                            if getattr(m, "action", None) is not None:
                                continue
                            sender = None
                            if chat.peer_type != "user" and m.sender is not None:
                                sender = utils.get_display_name(m.sender)
                            await store.save_message(s, chat, msg_data(m, sender), live=False)
                        if last == 0 and len(msgs) < limit:
                            chat.history_complete = True
                        fetched += 1
                    chat.unread_count = dialog.unread_count
                    await store.set_read_outbox(s, chat, dialog.dialog.read_outbox_max_id or 0)
                    await s.commit()
                if fetched and fetched % 20 == 0:
                    await asyncio.sleep(1)
            async with SessionLocal() as s:
                acc = await s.get(TgAccount, account_id)
                if acc is not None:
                    acc.synced_at = utcnow()
                    await s.commit()
            await hub.publish({"type": "chats.reload", "account_id": account_id}, account_id=account_id)
        except TelegramError:
            pass
        except Exception as exc:  # noqa: BLE001
            log.exception("Akkaunt %s sinxronlashda xato", account_id)
            await self._set_status(account_id, "active", f"Sinxronlashda xato: {_human_error(exc)}")
        finally:
            self._sync_running.discard(account_id)

    async def load_older(self, chat_id: int, limit: int = 50) -> int:
        async with SessionLocal() as s:
            chat = await s.get(Chat, chat_id)
            if chat is None or chat.history_complete:
                return 0
            client = self.clients.get(chat.account_id)
            if client is None:
                return 0
            oldest = await store.min_tg_id(s, chat_id)
            try:
                msgs = await client.get_messages(input_peer(chat), limit=limit, offset_id=oldest or 0)
            except Exception as exc:  # noqa: BLE001
                log.warning("Eski xabarlarni olishda xato: %s", exc)
                return 0
            count = 0
            for m in msgs:
                if getattr(m, "action", None) is not None:
                    continue
                sender = None
                if chat.peer_type != "user" and m.sender is not None:
                    sender = utils.get_display_name(m.sender)
                _, created = await store.save_message(s, chat, msg_data(m, sender), live=False)
                count += int(created)
            if len(msgs) < limit:
                chat.history_complete = True
            await s.commit()
            return count

    # ---------- kirish (login) ----------

    async def _cleanup_pending(self) -> None:
        now = time.monotonic()
        for key, pending in list(self.pending.items()):
            if now - pending.created > PENDING_TTL:
                self.pending.pop(key, None)
                try:
                    await pending.client.disconnect()
                except Exception:  # noqa: BLE001
                    pass

    async def login_start(self, phone: str) -> str:
        await self._cleanup_pending()
        phone = "+" + "".join(ch for ch in phone if ch.isdigit())
        if len(phone) < 8:
            raise TelegramError("Telefon raqam noto'g'ri. Masalan: +998901234567")
        client = self._new_client()
        try:
            await client.connect()
            sent = await client.send_code_request(phone)
        except Exception as exc:  # noqa: BLE001
            await client.disconnect()
            raise TelegramError(_human_error(exc)) from exc
        login_id = secrets.token_urlsafe(16)
        self.pending[login_id] = PendingLogin(client, phone, sent.phone_code_hash, time.monotonic())
        return login_id

    def _pending(self, login_id: str) -> PendingLogin:
        pending = self.pending.get(login_id)
        if pending is None:
            raise TelegramError("Kirish jarayoni topilmadi yoki eskirgan. Qaytadan boshlang.")
        return pending

    async def login_code(self, login_id: str, code: str) -> dict:
        pending = self._pending(login_id)
        code = "".join(ch for ch in code if ch.isdigit())
        try:
            await pending.client.sign_in(phone=pending.phone, code=code, phone_code_hash=pending.phone_code_hash)
        except errors.SessionPasswordNeededError:
            return {"status": "password"}
        except Exception as exc:  # noqa: BLE001
            raise TelegramError(_human_error(exc)) from exc
        return {"status": "ok", "account_id": await self._finish_login(login_id)}

    async def login_password(self, login_id: str, password: str) -> dict:
        pending = self._pending(login_id)
        try:
            await pending.client.sign_in(password=password)
        except Exception as exc:  # noqa: BLE001
            raise TelegramError(_human_error(exc)) from exc
        return {"status": "ok", "account_id": await self._finish_login(login_id)}

    async def _finish_login(self, login_id: str) -> int:
        pending = self.pending.pop(login_id)
        client = pending.client
        me = await client.get_me()
        session_str = client.session.save()
        async with SessionLocal() as s:
            acc = await s.scalar(select(TgAccount).where(TgAccount.tg_user_id == me.id))
            if acc is None:
                count = await s.scalar(select(func.count(TgAccount.id))) or 0
                acc = TgAccount(
                    phone=pending.phone,
                    tg_user_id=me.id,
                    name=utils.get_display_name(me) or pending.phone,
                    color=ACCOUNT_COLORS[count % len(ACCOUNT_COLORS)],
                    include_groups=False,
                )
                s.add(acc)
            acc.username = me.username
            acc.phone = pending.phone
            acc.session_enc = encrypt(session_str)
            acc.status = "active"
            acc.last_error = None
            await s.commit()
            account_id, include_groups = acc.id, acc.include_groups
            payload = account_dict(acc, True)
        self._attach(account_id, client, include_groups, me.id)
        await hub.publish({"type": "account.update", "account": payload}, account_id=account_id)
        self._spawn(self._sync(account_id))
        return account_id

    async def logout(self, account_id: int) -> None:
        client = self.clients.pop(account_id, None)
        if client is not None:
            try:
                await client.log_out()
            except Exception:  # noqa: BLE001
                log.warning("log_out xatosi", exc_info=True)
        async with SessionLocal() as s:
            acc = await s.get(TgAccount, account_id)
            if acc is not None:
                acc.session_enc = None
                acc.status = "logged_out"
                await s.commit()

    async def detach(self, account_id: int) -> None:
        client = self.clients.pop(account_id, None)
        if client is not None:
            await client.disconnect()

    # ---------- yuborish, o'qish, media ----------

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
        client = self._client(chat.account_id)
        peer = input_peer(chat)
        try:
            if file_path is not None:
                sent = await client.send_file(
                    peer, str(file_path), caption=text or None, force_document=as_document, reply_to=reply_to
                )
            else:
                sent = await client.send_message(peer, text, reply_to=reply_to, link_preview=False)
        except Exception as exc:  # noqa: BLE001
            raise TelegramError(_human_error(exc)) from exc
        data = msg_data(sent)
        if file_path is not None:
            data.media_path = str(file_path)
        async with SessionLocal() as s:
            db_chat = await s.get(Chat, chat.id)
            assert db_chat is not None
            msg, created = await store.save_message(s, db_chat, data, live=True, sent_by_user_id=user_id)
            await s.commit()
        await store.publish_message(db_chat, msg, "message.new" if created else "message.edit")
        return msg

    async def mark_read(self, chat: Chat) -> None:
        client = self.clients.get(chat.account_id)
        if client is None:
            return
        async with SessionLocal() as s:
            max_id = await store.max_tg_id(s, chat.id)
        if max_id:
            try:
                await client.send_read_acknowledge(input_peer(chat), max_id=max_id)
            except Exception:  # noqa: BLE001
                log.warning("O'qildi belgisini yuborib bo'lmadi", exc_info=True)

    async def download_media(self, message: Message) -> Path:
        if message.media_path and Path(message.media_path).exists():
            return Path(message.media_path)
        lock = self._media_locks.setdefault(message.id, asyncio.Lock())
        async with lock:
            async with SessionLocal() as s:
                msg = await s.get(Message, message.id)
                chat = await s.get(Chat, message.chat_id)
                assert msg is not None and chat is not None
                if msg.media_path and Path(msg.media_path).exists():
                    return Path(msg.media_path)
                client = self._client(chat.account_id)
                try:
                    tg_msg = await client.get_messages(input_peer(chat), ids=msg.tg_id)
                    if tg_msg is None or tg_msg.media is None:
                        raise TelegramError("Fayl Telegram'da topilmadi (o'chirilgan bo'lishi mumkin).")
                    target = media_dir_for(self.settings.media_dir, chat.account_id, chat.id)
                    path = await client.download_media(tg_msg, file=str(target) + "/")
                except TelegramError:
                    raise
                except Exception as exc:  # noqa: BLE001
                    raise TelegramError(_human_error(exc)) from exc
                if not path:
                    raise TelegramError("Faylni yuklab bo'lmadi.")
                msg.media_path = str(path)
                await s.commit()
                return Path(path)
