"""Parollar, shifrlash va kirish tokenlari."""

from __future__ import annotations

import base64
import hashlib
import hmac
import secrets
from datetime import UTC, datetime, timedelta

import jwt
from cryptography.fernet import Fernet, InvalidToken

from .config import get_settings

TOKEN_COOKIE = "crm_token"
TOKEN_DAYS = 30

_SCRYPT = {"n": 2**14, "r": 8, "p": 1, "dklen": 32}


def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    digest = hashlib.scrypt(password.encode(), salt=salt, **_SCRYPT)
    return f"scrypt${salt.hex()}${digest.hex()}"


def verify_password(password: str, stored: str) -> bool:
    try:
        algo, salt_hex, digest_hex = stored.split("$", 2)
    except ValueError:
        return False
    if algo != "scrypt":
        return False
    digest = hashlib.scrypt(password.encode(), salt=bytes.fromhex(salt_hex), **_SCRYPT)
    return hmac.compare_digest(digest.hex(), digest_hex)


def _fernet() -> Fernet:
    key = hashlib.sha256(("fernet:" + get_settings().secret_key).encode()).digest()
    return Fernet(base64.urlsafe_b64encode(key))


def encrypt(text: str) -> str:
    return _fernet().encrypt(text.encode()).decode()


def decrypt(token: str) -> str | None:
    try:
        return _fernet().decrypt(token.encode()).decode()
    except (InvalidToken, ValueError):
        return None


def _jwt_key() -> str:
    return hashlib.sha256(("jwt:" + get_settings().secret_key).encode()).hexdigest()


def create_token(user_id: int) -> str:
    payload = {
        "sub": str(user_id),
        "exp": datetime.now(UTC) + timedelta(days=TOKEN_DAYS),
    }
    return jwt.encode(payload, _jwt_key(), algorithm="HS256")


def decode_token(token: str) -> int | None:
    try:
        payload = jwt.decode(token, _jwt_key(), algorithms=["HS256"])
        return int(payload["sub"])
    except (jwt.PyJWTError, KeyError, ValueError):
        return None


def random_code(length: int = 10) -> str:
    alphabet = "abcdefghjkmnpqrstuvwxyz23456789"
    return "".join(secrets.choice(alphabet) for _ in range(length))


def verify_telegram_init_data(init_data: str, bot_token: str, max_age: int = 86400) -> dict | None:
    """Telegram Mini App initData imzosini tekshiradi. To'g'ri bo'lsa — Telegram foydalanuvchisi (dict)."""
    import json
    import time
    from urllib.parse import parse_qsl

    if not init_data or not bot_token:
        return None
    try:
        pairs = dict(parse_qsl(init_data, keep_blank_values=True, strict_parsing=True))
    except ValueError:
        return None
    received = pairs.pop("hash", None)
    if not received:
        return None
    check = "\n".join(f"{k}={v}" for k, v in sorted(pairs.items()))
    secret = hmac.new(b"WebAppData", bot_token.encode(), hashlib.sha256).digest()
    expected = hmac.new(secret, check.encode(), hashlib.sha256).hexdigest()
    if not hmac.compare_digest(expected, received):
        return None
    try:
        auth_date = int(pairs.get("auth_date", "0"))
        user = json.loads(pairs.get("user", "null"))
    except ValueError:
        return None
    if not isinstance(user, dict) or "id" not in user or time.time() - auth_date > max_age:
        return None
    return user
