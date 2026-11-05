"""chat turlari, ko'rinish, papkalar, bot tugmalari

Revision ID: 0002
Revises: 0001
Create Date: 2026-11-05
"""
import sqlalchemy as sa
from alembic import op

revision = "0002"
down_revision = "0001"
branch_labels = None
depends_on = None


def upgrade() -> None:
    op.create_table(
        "folders",
        sa.Column("id", sa.Integer(), nullable=False),
        sa.Column("name", sa.String(length=64), nullable=False),
        sa.Column("color", sa.String(length=16), nullable=False),
        sa.Column("position", sa.Integer(), nullable=False),
        sa.PrimaryKeyConstraint("id"),
    )

    with op.batch_alter_table("tg_accounts") as batch:
        batch.add_column(sa.Column("show_private", sa.Boolean(), nullable=False, server_default=sa.true()))
        batch.add_column(sa.Column("show_groups", sa.Boolean(), nullable=False, server_default=sa.false()))
        batch.add_column(sa.Column("show_channels", sa.Boolean(), nullable=False, server_default=sa.false()))
        batch.add_column(sa.Column("show_bots", sa.Boolean(), nullable=False, server_default=sa.false()))
    op.execute("UPDATE tg_accounts SET show_groups = include_groups")
    with op.batch_alter_table("tg_accounts") as batch:
        batch.drop_column("include_groups")

    with op.batch_alter_table("chats") as batch:
        batch.add_column(sa.Column("kind", sa.String(length=16), nullable=False, server_default="private"))
        batch.add_column(sa.Column("visible", sa.Boolean(), nullable=False, server_default=sa.true()))
        batch.add_column(sa.Column("muted", sa.Boolean(), nullable=False, server_default=sa.false()))
        batch.add_column(sa.Column("can_send", sa.Boolean(), nullable=False, server_default=sa.true()))
        batch.add_column(sa.Column("folder_id", sa.Integer(), nullable=True))
        batch.create_foreign_key("fk_chats_folder", "folders", ["folder_id"], ["id"], ondelete="SET NULL")
        batch.create_index("ix_chats_kind", ["kind"])
        batch.create_index("ix_chats_visible", ["visible"])
        batch.create_index("ix_chats_folder_id", ["folder_id"])
    # Avval faqat shaxsiy chatlar va guruhlar (megagroup) saqlangan edi
    op.execute("UPDATE chats SET kind = 'group' WHERE peer_type IN ('chat', 'channel')")

    with op.batch_alter_table("messages") as batch:
        batch.add_column(sa.Column("buttons", sa.Text(), nullable=True))


def downgrade() -> None:
    with op.batch_alter_table("messages") as batch:
        batch.drop_column("buttons")
    with op.batch_alter_table("chats") as batch:
        batch.drop_index("ix_chats_folder_id")
        batch.drop_index("ix_chats_visible")
        batch.drop_index("ix_chats_kind")
        batch.drop_constraint("fk_chats_folder", type_="foreignkey")
        for col in ("folder_id", "can_send", "muted", "visible", "kind"):
            batch.drop_column(col)
    with op.batch_alter_table("tg_accounts") as batch:
        batch.add_column(sa.Column("include_groups", sa.Boolean(), nullable=False, server_default=sa.false()))
    op.execute("UPDATE tg_accounts SET include_groups = show_groups")
    with op.batch_alter_table("tg_accounts") as batch:
        for col in ("show_bots", "show_channels", "show_groups", "show_private"):
            batch.drop_column(col)
    op.drop_table("folders")
