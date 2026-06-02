from datetime import datetime, timezone

from sqlalchemy import (
    Boolean,
    Column,
    DateTime,
    ForeignKey,
    Integer,
    String,
    Text,
    UniqueConstraint,
)
from sqlalchemy.orm import relationship

from .database import Base
from .settings import settings


def utcnow():
    return datetime.now(timezone.utc)


class User(Base):
    __tablename__ = "users"

    id = Column(Integer, primary_key=True, index=True)
    email = Column(String, unique=True, index=True, nullable=False)
    username = Column(String, nullable=False)
    password_hash = Column(String, nullable=False)
    created_at = Column(DateTime, default=utcnow)

    player = relationship(
        "PlayerState", back_populates="user", uselist=False, cascade="all, delete-orphan"
    )
    items = relationship(
        "OwnedItem", back_populates="user", cascade="all, delete-orphan"
    )


class PlayerState(Base):
    __tablename__ = "player_states"

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id"), unique=True, nullable=False)
    money = Column(Integer, default=settings.STARTING_MONEY)
    current_level = Column(Integer, default=1)
    highest_level = Column(Integer, default=1)
    # Which companion screen the phone should show, driven by the game scene.
    current_screen = Column(String, default="wait")  # wait | clues | shop | chat | pause
    # Активное оружие, выбранное на сайте (один ствол). По умолчанию пистолет.
    equipped_weapon = Column(String, default="pistol")
    # Накопленные приметы (id из пула CLUES через запятую). Копятся случайно по дням.
    revealed_clues = Column(String, default="")
    # Текущий вопрос математики в чате doom (координация телефон ↔ игра).
    chat_q_id = Column(Integer, default=0)
    chat_q_text = Column(String, default="")        # текст примера, напр. "7 + 5 = ?"
    chat_q_options = Column(String, default="")     # варианты через запятую, напр. "12,9,15"
    chat_q_answer = Column(Integer, default=0)
    chat_q_status = Column(String, default="idle")  # idle | pending | correct | wrong
    # Прошёл ли игрок вступительный чат на телефоне (игра ждёт этого перед стартом doom).
    intro_done = Column(Boolean, default=False)
    updated_at = Column(DateTime, default=utcnow, onupdate=utcnow)

    user = relationship("User", back_populates="player")


class OwnedItem(Base):
    """A weapon or perk a player has purchased."""

    __tablename__ = "owned_items"
    __table_args__ = (UniqueConstraint("user_id", "item_id", name="uq_user_item"),)

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id"), nullable=False)
    item_id = Column(String, nullable=False)
    acquired_at = Column(DateTime, default=utcnow)

    user = relationship("User", back_populates="items")


class PairingSession(Base):
    """Device-pairing handshake between the game (exe) and the website (phone).

    The game creates a pairing session and renders its QR. The phone (after the
    user logs in on the site) confirms the code, which links the session to the
    user and lets the game obtain a game token by polling.
    """

    __tablename__ = "pairing_sessions"

    id = Column(Integer, primary_key=True, index=True)
    code = Column(String, unique=True, index=True, nullable=False)
    status = Column(String, default="pending")  # pending | linked | consumed | expired
    user_id = Column(Integer, ForeignKey("users.id"), nullable=True)
    poll_secret = Column(String, nullable=False)
    created_at = Column(DateTime, default=utcnow)
    expires_at = Column(DateTime, nullable=False)


class AppConfig(Base):
    """Runtime config overrides editable from the admin panel (key -> JSON value).

    Keys used: "economy" (dict overrides), "level_overrides" (per-level dict).
    Anything not overridden falls back to the defaults in gamedata.py.
    """

    __tablename__ = "app_config"

    key = Column(String, primary_key=True)
    value = Column(Text, nullable=False, default="{}")
    updated_at = Column(DateTime, default=utcnow, onupdate=utcnow)
