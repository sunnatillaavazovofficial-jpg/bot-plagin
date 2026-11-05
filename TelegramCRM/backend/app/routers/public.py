"""Kirishsiz ochiq ma'lumot: bosh sahifadagi kompaniya haqidagi matn."""

from __future__ import annotations

from fastapi import APIRouter, Depends
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..settings_store import get_all
from ..site import public_site

router = APIRouter(prefix="/api/public", tags=["public"])


@router.get("/site")
async def site(session: AsyncSession = Depends(get_session)):
    return public_site(await get_all(session))
