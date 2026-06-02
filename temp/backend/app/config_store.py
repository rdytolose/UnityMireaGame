"""Runtime config overrides (admin-editable), merged over gamedata defaults.

The admin panel writes JSON into the AppConfig table; here we read it back and
deep-merge it on top of the static defaults in gamedata.py. Anything not
overridden keeps its default, so a fresh DB behaves exactly like before.
"""

import copy
import json

from sqlalchemy.orm import Session

from . import gamedata
from .models import AppConfig


def _get(db: Session, key: str, default):
    row = db.query(AppConfig).filter(AppConfig.key == key).first()
    if row is None:
        return default
    try:
        return json.loads(row.value)
    except (ValueError, TypeError):
        return default


def _set(db: Session, key: str, value) -> None:
    payload = json.dumps(value, ensure_ascii=False)
    row = db.query(AppConfig).filter(AppConfig.key == key).first()
    if row is None:
        db.add(AppConfig(key=key, value=payload))
    else:
        row.value = payload
    db.commit()


def _deep_merge(base: dict, over: dict) -> dict:
    """Recursively merge `over` onto a copy of `base`."""
    out = copy.deepcopy(base)
    for k, v in (over or {}).items():
        if isinstance(v, dict) and isinstance(out.get(k), dict):
            out[k] = _deep_merge(out[k], v)
        else:
            out[k] = v
    return out


# ---- reads (merged) ----

def economy_overrides(db: Session) -> dict:
    return _get(db, "economy", {})


def level_overrides(db: Session) -> dict:
    return _get(db, "level_overrides", {})


def math_questions(db: Session) -> list:
    """Кастомные матвопросы из админки: [{text, options:[3], answer}]. Пусто = генерим случайно."""
    v = _get(db, "math_questions", [])
    return v if isinstance(v, list) else []


# Диалоги (вступительный и переходный). Узел: {"text","a":[2]} либо строка "MATH"
# (вставляет арифм. вопрос из пула). Редактируются в админке, по умолчанию — ниже.
DEFAULT_DIALOGUES = {
    "intro": [
        {"text": "Ну чё, на смену заступаешь?", "a": ["Ага, погнали", "Не выспался капец"]},
        {"text": "Ночка будет дикая. Я буду подкидывать задачки — решай на ходу.", "a": ["Понял", "А что за дичь?"]},
        "MATH",
        {"text": "Норм считаешь. Ещё одну на разогрев.", "a": ["Давай", "Лёгкотня"]},
        "MATH",
        {"text": "Готов. Фильтруй кто заходит и держись.", "a": ["Окей", "Погнали"]},
    ],
    "doom_to_shop": [
        {"text": "Фух, отбился! Цел там?", "a": ["Вроде да", "Еле живой"]},
        {"text": "Передохни в лавке, закупись — скоро снова полезут.", "a": ["Понял", "Что брать?"]},
        {"text": "Бери ствол помощнее и броню. Деньги есть — трать смело.", "a": ["Ок", "Лады"]},
    ],
}


def all_dialogues(db: Session) -> dict:
    """Все диалоги для админки: сохранённые перекрывают дефолтные (по ключу)."""
    stored = _get(db, "dialogues", {})
    if not isinstance(stored, dict):
        stored = {}
    out = {k: v for k, v in DEFAULT_DIALOGUES.items()}
    out.update(stored)
    return out


def dialogue(db: Session, key: str) -> list:
    """Узлы одного диалога по ключу (сохранённый или дефолтный)."""
    nodes = all_dialogues(db).get(key)
    return nodes if isinstance(nodes, list) else []


def set_dialogues(db: Session, value: dict) -> None:
    _set(db, "dialogues", value or {})


def merged_economy(db: Session) -> dict:
    return _deep_merge(gamedata.ECONOMY, economy_overrides(db))


def merged_level_config(db: Session, level: int) -> dict:
    base = gamedata.level_config(level)
    ov = level_overrides(db)
    per = ov.get(str(level)) or ov.get(level) or {}
    return _deep_merge(base, per)


def merged_all_levels(db: Session) -> list:
    return [merged_level_config(db, i) for i in range(1, gamedata.MAX_LEVEL + 1)]


# ---- writes ----

def set_economy(db: Session, value: dict) -> None:
    _set(db, "economy", value or {})


def set_level_overrides(db: Session, value: dict) -> None:
    _set(db, "level_overrides", value or {})


def set_math_questions(db: Session, value: list) -> None:
    _set(db, "math_questions", value or [])


# ---- Параметры оружия (урон/скорострельность), редактируются в админке ----

def weapon_overrides(db: Session) -> dict:
    v = _get(db, "weapons", {})
    return v if isinstance(v, dict) else {}


def effective_weapon_stats(db: Session, weapon_id: str) -> dict:
    """Базовые stats из gamedata.ITEMS + оверрайды из админки."""
    item = gamedata.ITEMS_BY_ID.get(weapon_id)
    base = dict(item.get("stats", {})) if item else {}
    ov = weapon_overrides(db).get(weapon_id, {})
    if isinstance(ov, dict):
        base.update(ov)
    return base


def all_weapon_stats(db: Session) -> dict:
    """Для админки: {id: {name, damage, fire_rate}} по всем стволам (с оверрайдами)."""
    out = {}
    for it in gamedata.ITEMS:
        if it.get("type") != "weapon":
            continue
        eff = effective_weapon_stats(db, it["id"])
        out[it["id"]] = {
            "name": it.get("name", it["id"]),
            "damage": eff.get("damage", 25),
            "fire_rate": eff.get("fire_rate", 0.3),
        }
    return out


def set_weapons(db: Session, value: dict) -> None:
    _set(db, "weapons", value or {})
