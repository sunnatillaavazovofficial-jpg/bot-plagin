from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy import func, select, update
from sqlalchemy.ext.asyncio import AsyncSession

from ..db import get_session
from ..deps import current_user, staff_manager
from ..models import Chat, Folder, User
from ..serializers import folder_dict
from ..ws_hub import hub

router = APIRouter(prefix="/api/folders", tags=["folders"])


@router.get("")
async def list_folders(_: User = Depends(current_user), session: AsyncSession = Depends(get_session)):
    folders = (await session.scalars(select(Folder).order_by(Folder.position, Folder.id))).all()
    counts = dict(
        (
            await session.execute(
                select(Chat.folder_id, func.count(Chat.id))
                .where(Chat.folder_id.is_not(None), Chat.visible.is_(True))
                .group_by(Chat.folder_id)
            )
        ).all()
    )
    return [{**folder_dict(f), "chats": int(counts.get(f.id, 0))} for f in folders]


class FolderIn(BaseModel):
    name: str
    color: str = "#64748b"


@router.post("")
async def create_folder(data: FolderIn, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    if not data.name.strip():
        raise HTTPException(400, "Papka nomini kiriting")
    position = (await session.scalar(select(func.max(Folder.position))) or 0) + 1
    folder = Folder(name=data.name.strip()[:64], color=data.color, position=position)
    session.add(folder)
    await session.commit()
    await hub.publish({"type": "folders.update"})
    return folder_dict(folder)


@router.patch("/{folder_id}")
async def update_folder(
    folder_id: int, data: FolderIn, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)
):
    folder = await session.get(Folder, folder_id)
    if folder is None:
        raise HTTPException(404, "Papka topilmadi")
    if data.name.strip():
        folder.name = data.name.strip()[:64]
    folder.color = data.color
    await session.commit()
    await hub.publish({"type": "folders.update"})
    return folder_dict(folder)


class OrderIn(BaseModel):
    folder_ids: list[int]


@router.post("/reorder")
async def reorder(data: OrderIn, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    for pos, fid in enumerate(data.folder_ids):
        await session.execute(update(Folder).where(Folder.id == fid).values(position=pos))
    await session.commit()
    await hub.publish({"type": "folders.update"})
    return {"ok": True}


@router.delete("/{folder_id}")
async def delete_folder(folder_id: int, _: User = Depends(staff_manager), session: AsyncSession = Depends(get_session)):
    """Papka o'chiriladi, ichidagi chatlar papkasiz qoladi."""
    folder = await session.get(Folder, folder_id)
    if folder is None:
        raise HTTPException(404, "Papka topilmadi")
    await session.execute(update(Chat).where(Chat.folder_id == folder_id).values(folder_id=None))
    await session.delete(folder)
    await session.commit()
    await hub.publish({"type": "folders.update"})
    return {"ok": True}
