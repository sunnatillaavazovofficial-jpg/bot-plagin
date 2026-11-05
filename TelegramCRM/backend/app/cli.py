"""Serverda bajariladigan buyruqlar.

    docker compose exec app python -m app.cli reset-password <login> <yangi_parol>

Login topilmasa, shu login bilan yangi admin yaratiladi.
"""

from __future__ import annotations

import asyncio
import sys

from sqlalchemy import select

from .db import SessionLocal, dispose_engine, init_engine
from .models import User
from .security import hash_password


async def reset_password(login: str, password: str) -> str:
    login = login.strip().lower()
    if not login or len(password) < 6:
        raise SystemExit("Login va kamida 6 belgili parol kerak")
    init_engine()
    try:
        async with SessionLocal() as s:
            user = await s.scalar(select(User).where(User.login == login))
            if user is None:
                s.add(User(name="Administrator", login=login, role="admin", password_hash=hash_password(password)))
                result = "yaratildi"
            else:
                user.password_hash = hash_password(password)
                user.is_active = True
                result = "yangilandi"
            await s.commit()
    finally:
        await dispose_engine()
    return result


def main(argv: list[str]) -> int:
    if len(argv) == 3 and argv[0] == "reset-password":
        result = asyncio.run(reset_password(argv[1], argv[2]))
        print(f"«{argv[1]}» paroli {result}")
        return 0
    print(__doc__)
    return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
