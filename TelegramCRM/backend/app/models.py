"""Bazadagi jadvallar."""

from __future__ import annotations

from datetime import UTC, date, datetime

from sqlalchemy import (
    BigInteger,
    Boolean,
    Date,
    DateTime,
    ForeignKey,
    Index,
    Integer,
    String,
    Text,
    TypeDecorator,
    UniqueConstraint,
)
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


def utcnow() -> datetime:
    return datetime.now(UTC)


class UTCDateTime(TypeDecorator):
    """SQLite vaqt zonasini saqlamaydi: UTC da yozamiz va UTC sifatida qaytaramiz."""

    impl = DateTime
    cache_ok = True

    def process_bind_param(self, value, dialect):
        if value is None:
            return None
        if value.tzinfo is not None:
            value = value.astimezone(UTC).replace(tzinfo=None)
        return value

    def process_result_value(self, value, dialect):
        if value is None:
            return None
        return value.replace(tzinfo=UTC)


class Base(DeclarativeBase):
    pass


ROLES = ("admin", "menejer", "xodim")


class User(Base):
    __tablename__ = "users"

    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(120))
    login: Mapped[str] = mapped_column(String(64), unique=True)
    password_hash: Mapped[str] = mapped_column(String(255))
    role: Mapped[str] = mapped_column(String(16), default="xodim")
    is_active: Mapped[bool] = mapped_column(Boolean, default=True)
    tg_user_id: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    tg_username: Mapped[str | None] = mapped_column(String(64), nullable=True)
    link_code: Mapped[str | None] = mapped_column(String(32), nullable=True, unique=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class TgAccount(Base):
    __tablename__ = "tg_accounts"

    id: Mapped[int] = mapped_column(primary_key=True)
    phone: Mapped[str] = mapped_column(String(32))
    tg_user_id: Mapped[int | None] = mapped_column(BigInteger, nullable=True, index=True)
    name: Mapped[str] = mapped_column(String(120))
    username: Mapped[str | None] = mapped_column(String(64), nullable=True)
    color: Mapped[str] = mapped_column(String(16), default="#2563eb")
    session_enc: Mapped[str | None] = mapped_column(Text, nullable=True)
    status: Mapped[str] = mapped_column(String(16), default="active")  # active | error | logged_out
    last_error: Mapped[str | None] = mapped_column(Text, nullable=True)
    # Yangi paydo bo'lgan chatlar saytda avtomatik ko'rinsinmi (tur bo'yicha)
    show_private: Mapped[bool] = mapped_column(Boolean, default=True)
    show_groups: Mapped[bool] = mapped_column(Boolean, default=False)
    show_channels: Mapped[bool] = mapped_column(Boolean, default=False)
    show_bots: Mapped[bool] = mapped_column(Boolean, default=False)
    synced_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class AccountAccess(Base):
    __tablename__ = "account_access"

    user_id: Mapped[int] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), primary_key=True)
    account_id: Mapped[int] = mapped_column(
        ForeignKey("tg_accounts.id", ondelete="CASCADE"), primary_key=True
    )


class Client(Base):
    __tablename__ = "clients"

    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(200))
    phone: Mapped[str | None] = mapped_column(String(64), nullable=True)
    company: Mapped[str | None] = mapped_column(String(200), nullable=True)
    notes: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Folder(Base):
    __tablename__ = "folders"

    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(64))
    color: Mapped[str] = mapped_column(String(16), default="#64748b")
    position: Mapped[int] = mapped_column(Integer, default=0)


CHAT_KINDS = ("private", "bot", "group", "channel")


class Chat(Base):
    __tablename__ = "chats"
    __table_args__ = (UniqueConstraint("account_id", "peer_id", name="uq_chat_peer"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    account_id: Mapped[int] = mapped_column(ForeignKey("tg_accounts.id", ondelete="CASCADE"), index=True)
    peer_id: Mapped[int] = mapped_column(BigInteger)
    peer_type: Mapped[str] = mapped_column(String(16), default="user")  # user | chat | channel
    kind: Mapped[str] = mapped_column(String(16), default="private", index=True)  # private | bot | group | channel
    visible: Mapped[bool] = mapped_column(Boolean, default=True, index=True)
    muted: Mapped[bool] = mapped_column(Boolean, default=False)
    can_send: Mapped[bool] = mapped_column(Boolean, default=True)
    folder_id: Mapped[int | None] = mapped_column(
        ForeignKey("folders.id", ondelete="SET NULL"), nullable=True, index=True
    )
    access_hash: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    title: Mapped[str] = mapped_column(String(255), default="")
    username: Mapped[str | None] = mapped_column(String(64), nullable=True)
    phone: Mapped[str | None] = mapped_column(String(32), nullable=True)
    unread_count: Mapped[int] = mapped_column(Integer, default=0)
    last_message_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True, index=True)
    last_message_text: Mapped[str | None] = mapped_column(Text, nullable=True)
    last_message_out: Mapped[bool] = mapped_column(Boolean, default=False)
    last_incoming_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    read_outbox_max_id: Mapped[int] = mapped_column(Integer, default=0)
    history_complete: Mapped[bool] = mapped_column(Boolean, default=False)
    client_id: Mapped[int | None] = mapped_column(
        ForeignKey("clients.id", ondelete="SET NULL"), nullable=True, index=True
    )
    assigned_user_id: Mapped[int | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), nullable=True
    )
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Message(Base):
    __tablename__ = "messages"
    __table_args__ = (
        UniqueConstraint("chat_id", "tg_id", name="uq_message_tg"),
        Index("ix_message_account_tg", "account_id", "tg_id"),
    )

    id: Mapped[int] = mapped_column(primary_key=True)
    chat_id: Mapped[int] = mapped_column(ForeignKey("chats.id", ondelete="CASCADE"), index=True)
    account_id: Mapped[int] = mapped_column(ForeignKey("tg_accounts.id", ondelete="CASCADE"))
    tg_id: Mapped[int] = mapped_column(BigInteger)
    out: Mapped[bool] = mapped_column(Boolean, default=False)
    text: Mapped[str] = mapped_column(Text, default="")
    date: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    edited: Mapped[bool] = mapped_column(Boolean, default=False)
    deleted: Mapped[bool] = mapped_column(Boolean, default=False)
    media_type: Mapped[str | None] = mapped_column(String(16), nullable=True)
    media_name: Mapped[str | None] = mapped_column(String(255), nullable=True)
    media_mime: Mapped[str | None] = mapped_column(String(128), nullable=True)
    media_size: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    media_path: Mapped[str | None] = mapped_column(Text, nullable=True)
    sender_name: Mapped[str | None] = mapped_column(String(255), nullable=True)
    reply_to_tg_id: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    buttons: Mapped[str | None] = mapped_column(Text, nullable=True)  # bot tugmalari (JSON)
    sent_by_user_id: Mapped[int | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), nullable=True
    )


class OrderStatus(Base):
    __tablename__ = "order_statuses"

    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(64))
    color: Mapped[str] = mapped_column(String(16), default="#64748b")
    position: Mapped[int] = mapped_column(Integer, default=0)
    is_done: Mapped[bool] = mapped_column(Boolean, default=False)
    is_cancel: Mapped[bool] = mapped_column(Boolean, default=False)


class Order(Base):
    __tablename__ = "orders"

    id: Mapped[int] = mapped_column(primary_key=True)
    title: Mapped[str] = mapped_column(String(255))
    description: Mapped[str] = mapped_column(Text, default="")
    client_id: Mapped[int | None] = mapped_column(
        ForeignKey("clients.id", ondelete="SET NULL"), nullable=True, index=True
    )
    chat_id: Mapped[int | None] = mapped_column(ForeignKey("chats.id", ondelete="SET NULL"), nullable=True)
    account_id: Mapped[int | None] = mapped_column(
        ForeignKey("tg_accounts.id", ondelete="SET NULL"), nullable=True
    )
    status_id: Mapped[int] = mapped_column(ForeignKey("order_statuses.id"), index=True)
    position: Mapped[int] = mapped_column(Integer, default=0)
    price: Mapped[int] = mapped_column(BigInteger, default=0)
    paid: Mapped[int] = mapped_column(BigInteger, default=0)
    deadline: Mapped[date | None] = mapped_column(Date, nullable=True)
    assignee_id: Mapped[int | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), nullable=True, index=True
    )
    created_by_id: Mapped[int | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), nullable=True
    )
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow, index=True)
    updated_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow, onupdate=utcnow)
    closed_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)


TX_KINDS = ("kirim", "chiqim", "otkazma")
PAY_METHODS = ("naqd", "karta", "o'tkazma", "boshqa")


class Category(Base):
    """Kassa toifasi (kirim yoki chiqim)."""

    __tablename__ = "categories"

    id: Mapped[int] = mapped_column(primary_key=True)
    name: Mapped[str] = mapped_column(String(64))
    kind: Mapped[str] = mapped_column(String(8))  # kirim | chiqim
    color: Mapped[str] = mapped_column(String(16), default="#64748b")
    position: Mapped[int] = mapped_column(Integer, default=0)
    # Tizim toifalari: order_payment (zakaz to'lovi), refund (qaytarilgan pul), opening (boshlang'ich qoldiq)
    system_key: Mapped[str | None] = mapped_column(String(32), nullable=True, unique=True)


class Debt(Base):
    """Qarz daftari: bizga (bizdan qarzdor) yoki bizdan (biz qarzdormiz)."""

    __tablename__ = "debts"

    id: Mapped[int] = mapped_column(primary_key=True)
    direction: Mapped[str] = mapped_column(String(8))  # bizga | bizdan
    person: Mapped[str] = mapped_column(String(200))
    phone: Mapped[str | None] = mapped_column(String(64), nullable=True)
    client_id: Mapped[int | None] = mapped_column(ForeignKey("clients.id", ondelete="SET NULL"), nullable=True)
    amount: Mapped[int] = mapped_column(BigInteger)
    note: Mapped[str | None] = mapped_column(Text, nullable=True)
    due_date: Mapped[date | None] = mapped_column(Date, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    closed_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    created_by_id: Mapped[int | None] = mapped_column(ForeignKey("users.id", ondelete="SET NULL"), nullable=True)


class Transaction(Base):
    """Kassa yozuvi: kirim, chiqim yoki o'tkazma (naqd ⇄ karta)."""

    __tablename__ = "transactions"

    id: Mapped[int] = mapped_column(primary_key=True)
    kind: Mapped[str] = mapped_column(String(8), index=True)
    amount: Mapped[int] = mapped_column(BigInteger)
    date: Mapped[date] = mapped_column(Date, index=True)
    method: Mapped[str] = mapped_column(String(16), default="naqd")
    to_method: Mapped[str | None] = mapped_column(String(16), nullable=True)
    category_id: Mapped[int | None] = mapped_column(
        ForeignKey("categories.id", ondelete="SET NULL"), nullable=True, index=True
    )
    note: Mapped[str | None] = mapped_column(Text, nullable=True)
    client_id: Mapped[int | None] = mapped_column(ForeignKey("clients.id", ondelete="SET NULL"), nullable=True)
    order_id: Mapped[int | None] = mapped_column(
        ForeignKey("orders.id", ondelete="SET NULL"), nullable=True, index=True
    )
    debt_id: Mapped[int | None] = mapped_column(ForeignKey("debts.id", ondelete="SET NULL"), nullable=True, index=True)
    source: Mapped[str] = mapped_column(String(8), default="site")  # site | bot | order
    created_by_id: Mapped[int | None] = mapped_column(ForeignKey("users.id", ondelete="SET NULL"), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class OrderFile(Base):
    __tablename__ = "order_files"

    id: Mapped[int] = mapped_column(primary_key=True)
    order_id: Mapped[int] = mapped_column(ForeignKey("orders.id", ondelete="CASCADE"), index=True)
    name: Mapped[str] = mapped_column(String(255))
    path: Mapped[str] = mapped_column(Text)
    mime: Mapped[str | None] = mapped_column(String(128), nullable=True)
    size: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    uploaded_by_id: Mapped[int | None] = mapped_column(
        ForeignKey("users.id", ondelete="SET NULL"), nullable=True
    )


class OrderEvent(Base):
    __tablename__ = "order_events"

    id: Mapped[int] = mapped_column(primary_key=True)
    order_id: Mapped[int] = mapped_column(ForeignKey("orders.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[int | None] = mapped_column(ForeignKey("users.id", ondelete="SET NULL"), nullable=True)
    kind: Mapped[str] = mapped_column(String(16))  # created | status | comment | payment | assign | edit | file
    text: Mapped[str] = mapped_column(Text, default="")
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Setting(Base):
    __tablename__ = "settings"

    key: Mapped[str] = mapped_column(String(64), primary_key=True)
    value: Mapped[str] = mapped_column(Text, default="")


class NotifyLog(Base):
    __tablename__ = "notify_log"

    key: Mapped[str] = mapped_column(String(128), primary_key=True)
    sent_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
