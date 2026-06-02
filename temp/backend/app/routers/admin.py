"""Admin panel API. Protected by the `X-Admin-Key` header (settings.ADMIN_KEY).

Lets you list/edit players (money, level, items, equipped weapon) and tweak
runtime config (economy values + per-level difficulty overrides) without
touching code or the DB by hand.
"""

from fastapi import APIRouter, Depends, Header, HTTPException, status
from pydantic import BaseModel
from sqlalchemy.orm import Session

from .. import config_store
from ..database import get_db
from ..gamedata import ITEMS, ITEMS_BY_ID
from ..models import OwnedItem, PlayerState, User
from ..settings import settings

router = APIRouter(prefix="/api/admin", tags=["admin"])


def require_admin(x_admin_key: str = Header(default="", alias="X-Admin-Key")) -> bool:
    if not x_admin_key or x_admin_key != settings.ADMIN_KEY:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Bad admin key")
    return True


def _get_state(db: Session, user_id: int) -> PlayerState:
    st = db.query(PlayerState).filter(PlayerState.user_id == user_id).first()
    if st is None:
        st = PlayerState(user_id=user_id, money=settings.STARTING_MONEY,
                         current_level=1, highest_level=1)
        db.add(st)
        db.commit()
    return st


def _player_dict(db: Session, u: User) -> dict:
    st = _get_state(db, u.id)
    items = [i.item_id for i in db.query(OwnedItem).filter(OwnedItem.user_id == u.id).all()]
    return {
        "user_id": u.id,
        "email": u.email,
        "username": u.username,
        "money": st.money,
        "current_level": st.current_level,
        "highest_level": st.highest_level,
        "equipped_weapon": st.equipped_weapon or "pistol",
        "items": items,
    }


# ---- request bodies ----
class PlayerPatch(BaseModel):
    money: int | None = None
    current_level: int | None = None
    highest_level: int | None = None
    equipped_weapon: str | None = None


class MoneyOp(BaseModel):
    amount: int
    mode: str = "add"  # "add" | "set"


class ItemOp(BaseModel):
    item_id: str
    action: str = "add"  # "add" | "remove"


class ConfigPut(BaseModel):
    economy: dict | None = None
    level_overrides: dict | None = None
    math_questions: list | None = None
    dialogues: dict | None = None
    weapons: dict | None = None


# ---- auth check ----
@router.get("/ping")
def ping(_: bool = Depends(require_admin)):
    return {"ok": True}


# ---- players ----
@router.get("/players")
def list_players(_: bool = Depends(require_admin), db: Session = Depends(get_db)):
    return {"players": [_player_dict(db, u) for u in db.query(User).order_by(User.id).all()]}


@router.patch("/players/{user_id}")
def patch_player(user_id: int, body: PlayerPatch,
                 _: bool = Depends(require_admin), db: Session = Depends(get_db)):
    u = db.query(User).filter(User.id == user_id).first()
    if u is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Player not found")
    st = _get_state(db, user_id)
    if body.money is not None:
        st.money = max(0, body.money)
    if body.current_level is not None:
        st.current_level = max(1, body.current_level)
    if body.highest_level is not None:
        st.highest_level = max(1, body.highest_level)
    if body.equipped_weapon is not None:
        st.equipped_weapon = body.equipped_weapon
    st.highest_level = max(st.highest_level, st.current_level)
    db.commit()
    return _player_dict(db, u)


@router.post("/players/{user_id}/money")
def change_money(user_id: int, body: MoneyOp,
                 _: bool = Depends(require_admin), db: Session = Depends(get_db)):
    u = db.query(User).filter(User.id == user_id).first()
    if u is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Player not found")
    st = _get_state(db, user_id)
    st.money = body.amount if body.mode == "set" else st.money + body.amount
    st.money = max(0, st.money)
    db.commit()
    return _player_dict(db, u)


@router.post("/players/{user_id}/items")
def change_items(user_id: int, body: ItemOp,
                 _: bool = Depends(require_admin), db: Session = Depends(get_db)):
    u = db.query(User).filter(User.id == user_id).first()
    if u is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Player not found")
    if body.item_id not in ITEMS_BY_ID:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Unknown item")
    existing = db.query(OwnedItem).filter(
        OwnedItem.user_id == user_id, OwnedItem.item_id == body.item_id
    ).first()
    if body.action == "add" and existing is None:
        db.add(OwnedItem(user_id=user_id, item_id=body.item_id))
    elif body.action == "remove" and existing is not None:
        db.delete(existing)
    db.commit()
    return _player_dict(db, u)


@router.post("/players/{user_id}/reset")
def reset_player(user_id: int, _: bool = Depends(require_admin), db: Session = Depends(get_db)):
    u = db.query(User).filter(User.id == user_id).first()
    if u is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Player not found")
    st = _get_state(db, user_id)
    st.money = settings.STARTING_MONEY
    st.current_level = 1
    st.highest_level = 1
    st.equipped_weapon = "pistol"
    st.revealed_clues = ""
    db.query(OwnedItem).filter(OwnedItem.user_id == user_id).delete()
    db.commit()
    return _player_dict(db, u)


# ---- config ----
@router.get("/config")
def get_config(_: bool = Depends(require_admin), db: Session = Depends(get_db)):
    return {
        "max_level": settings.MAX_LEVEL,
        "economy": config_store.merged_economy(db),
        "economy_overrides": config_store.economy_overrides(db),
        "level_overrides": config_store.level_overrides(db),
        "levels": config_store.merged_all_levels(db),
        "math_questions": config_store.math_questions(db),
        "dialogues": config_store.all_dialogues(db),
        "weapons": config_store.all_weapon_stats(db),
        "items": ITEMS,
    }


@router.put("/config")
def put_config(body: ConfigPut, _: bool = Depends(require_admin), db: Session = Depends(get_db)):
    if body.economy is not None:
        config_store.set_economy(db, body.economy)
    if body.level_overrides is not None:
        config_store.set_level_overrides(db, body.level_overrides)
    if body.math_questions is not None:
        config_store.set_math_questions(db, body.math_questions)
    if body.dialogues is not None:
        config_store.set_dialogues(db, body.dialogues)
    if body.weapons is not None:
        config_store.set_weapons(db, body.weapons)
    return get_config(_=True, db=db)
