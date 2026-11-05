"""Haqiqiy Telegram rejimi (Telethon) uchun testlar — tarmoqsiz, soxta hodisalar bilan."""

from __future__ import annotations

import json
from datetime import UTC, datetime

from sqlalchemy import select
from telethon import errors, types

from app.config import get_settings
from app.db import SessionLocal
from app.models import Chat, Message, TgAccount
from app.telegram import manager as mgr
from app.telegram.manager import TelegramManager, chat_data, classify, extract_buttons, input_peer, msg_data


def make_user(uid=555, **kw):
    return types.User(id=uid, access_hash=987654321, first_name="Ali", last_name="Valiyev", phone="998901234567", **kw)


def make_msg(mid=10, text="Salom", out=False, media=None, reply_to=None):
    return types.Message(
        id=mid,
        peer_id=types.PeerUser(555),
        date=datetime(2026, 11, 3, 9, 0, tzinfo=UTC),
        message=text,
        out=out,
        media=media,
        reply_to=types.MessageReplyHeader(reply_to_msg_id=reply_to) if reply_to else None,
    )


class FakeEvent:
    def __init__(self, message, entity):
        self.message = message
        self._entity = entity
        self.is_private = isinstance(entity, types.User)
        self.chat_id = 555

    async def get_chat(self):
        return self._entity

    async def get_input_chat(self):
        return types.InputPeerUser(self._entity.id, self._entity.access_hash)

    async def get_sender(self):
        return self._entity


def test_chat_data_and_input_peer():
    data = chat_data(make_user())
    assert data.peer_id == 555 and data.peer_type == "user"
    assert data.title == "Ali Valiyev" and data.phone == "+998901234567"
    chat = Chat(account_id=1, peer_id=data.peer_id, peer_type="user", access_hash=data.access_hash)
    peer = input_peer(chat)
    assert isinstance(peer, types.InputPeerUser) and peer.access_hash == 987654321
    group = types.Channel(id=77, title="Guruh", photo=types.ChatPhotoEmpty(), date=None, access_hash=11, megagroup=True)
    gdata = chat_data(group)
    assert gdata.peer_type == "channel" and gdata.peer_id == -1000000000077
    assert isinstance(input_peer(Chat(account_id=1, peer_id=gdata.peer_id, access_hash=11)), types.InputPeerChannel)


def test_msg_data_text_and_media():
    d = msg_data(make_msg(text="Narxi qancha?", reply_to=3))
    assert d.text == "Narxi qancha?" and d.media_type is None and d.reply_to_tg_id == 3
    photo = types.MessageMediaPhoto(photo=types.Photo(id=1, access_hash=1, file_reference=b"", date=None, sizes=[], dc_id=2))
    assert msg_data(make_msg(media=photo, text="")).media_type == "photo"
    doc = types.MessageMediaDocument(
        document=types.Document(
            id=2, access_hash=1, file_reference=b"", date=None, mime_type="application/pdf", size=1234, dc_id=2,
            attributes=[types.DocumentAttributeFilename("maket.pdf")],
        )
    )
    d = msg_data(make_msg(media=doc))
    assert d.media_type == "document" and d.media_name == "maket.pdf" and d.media_size == 1234
    geo = types.MessageMediaGeo(geo=types.GeoPoint(long=69.24, lat=41.31, access_hash=0))
    assert "maps.google.com" in msg_data(make_msg(media=geo, text="")).text


async def test_accept_rules(app):
    m = TelegramManager(get_settings())
    m.me_ids[1] = 999
    assert m._accept(1, make_user())
    assert m._accept(1, make_user(bot=True))  # botlar ham olinadi
    assert not m._accept(1, make_user(uid=777000))  # kirish kodlari keladigan xizmat chati
    assert not m._accept(1, make_user(uid=999))  # Saqlangan xabarlar
    group = types.Chat(id=5, title="G", photo=types.ChatPhotoEmpty(), participants_count=3, date=None, version=1)
    assert m._accept(1, group)
    left = types.Chat(id=6, title="G", photo=types.ChatPhotoEmpty(), participants_count=3, date=None, version=1, left=True)
    assert not m._accept(1, left)
    assert m._accept(1, make_channel(broadcast=True))
    assert not m._accept(1, make_channel(broadcast=True, left=True))


def make_channel(**kw):
    return types.Channel(id=77, title="Kanal", photo=types.ChatPhotoEmpty(), date=None, access_hash=11, **kw)


def test_classify_kinds():
    assert classify(make_user()) == ("private", True)
    assert classify(make_user(bot=True)) == ("bot", True)
    group = types.Chat(id=5, title="G", photo=types.ChatPhotoEmpty(), participants_count=3, date=None, version=1)
    assert classify(group) == ("group", True)
    assert classify(make_channel(megagroup=True)) == ("group", True)
    # Obuna bo'lingan kanal — yozib bo'lmaydi; o'z kanalimiz — yozsa bo'ladi
    assert classify(make_channel(broadcast=True)) == ("channel", False)
    assert classify(make_channel(broadcast=True, creator=True)) == ("channel", True)
    rights = types.ChatAdminRights(post_messages=True)
    assert classify(make_channel(broadcast=True, admin_rights=rights)) == ("channel", True)
    banned = types.ChatBannedRights(until_date=None, send_messages=True)
    assert classify(make_channel(megagroup=True, default_banned_rights=banned)) == ("group", False)


def test_extract_buttons():
    markup = types.ReplyInlineMarkup(
        rows=[
            types.KeyboardButtonRow(
                buttons=[
                    types.KeyboardButton(text="Balans", type=types.InlineButtonTypeCallback(data=b"b")),
                    types.KeyboardButton(text="Sayt", type=types.InlineButtonTypeUrl(url="https://example.com")),
                    types.KeyboardButton(text="Yomon", type=types.InlineButtonTypeUrl(url="javascript:alert(1)")),
                ]
            )
        ]
    )
    rows = json.loads(extract_buttons(markup))
    assert rows == [[
        {"text": "Balans", "kind": "callback"},
        {"text": "Sayt", "kind": "url", "url": "https://example.com"},
        {"text": "Yomon", "kind": "other"},
    ]]
    reply = types.ReplyKeyboardMarkup(
        rows=[types.KeyboardButtonRow(buttons=[types.KeyboardButton(text="Menyu", type=types.ButtonTypeDefault())])]
    )
    assert json.loads(extract_buttons(reply)) == [[{"text": "Menyu", "kind": "reply"}]]
    assert extract_buttons(None) is None


def test_human_errors():
    assert "soniyadan" in mgr._human_error(errors.FloodWaitError(request=None, capture=30))
    assert "Kod noto'g'ri" in mgr._human_error(errors.PhoneCodeInvalidError(request=None))


async def test_on_new_message_saved(app):
    async with SessionLocal() as s:
        acc = TgAccount(phone="+1", name="Real", tg_user_id=999, status="active")
        s.add(acc)
        await s.commit()
        account_id = acc.id
    m = TelegramManager(get_settings())
    m.me_ids[account_id] = 999
    await m._on_new(account_id, FakeEvent(make_msg(mid=42, text="Assalomu alaykum"), make_user()))
    await m._on_new(account_id, FakeEvent(make_msg(mid=42, text="Assalomu alaykum"), make_user()))  # takror
    await m._on_new(account_id, FakeEvent(make_msg(mid=43, text="x"), make_user(uid=777000)))  # xizmat xabari
    async with SessionLocal() as s:
        chats = (await s.scalars(select(Chat).where(Chat.account_id == account_id))).all()
        assert len(chats) == 1
        assert chats[0].title == "Ali Valiyev" and chats[0].unread_count == 1 and chats[0].access_hash == 987654321
        msgs = (await s.scalars(select(Message).where(Message.chat_id == chats[0].id))).all()
        assert [x.tg_id for x in msgs] == [42]
