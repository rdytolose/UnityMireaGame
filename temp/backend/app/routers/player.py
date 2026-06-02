from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from ..database import get_db
import random

from .. import config_store
from ..gamedata import (
    CLUES,
    CLUES_BY_ID,
    ITEMS,
    ITEMS_BY_ID,
    make_math_question,
)
from ..models import OwnedItem, PlayerState, User
from ..schemas import (
    BuyRequest,
    ChatAnswerRequest,
    ChatAnswerResponse,
    ChatQuestionResponse,
    ChatStateResponse,
    CompleteLevelResponse,
    EarnRequest,
    EquipWeaponRequest,
    MoneyResponse,
    PlayerStateResponse,
    ScreenRequest,
    ScreenResponse,
)
from ..security import get_current_user, get_game_user
from ..settings import settings

router = APIRouter(prefix="/api", tags=["player"])


def _state(db: Session, user: User) -> PlayerState:
    st = db.query(PlayerState).filter(PlayerState.user_id == user.id).first()
    if st is None:
        st = PlayerState(user_id=user.id, money=settings.STARTING_MONEY, current_level=1, highest_level=1)
        db.add(st)
        db.commit()
    return st


def _state_response(db: Session, user: User) -> PlayerStateResponse:
    st = _state(db, user)
    items = [i.item_id for i in db.query(OwnedItem).filter(OwnedItem.user_id == user.id).all()]
    equipped = st.equipped_weapon or "pistol"
    stats = config_store.effective_weapon_stats(db, equipped)
    return PlayerStateResponse(
        username=user.username,
        money=st.money,
        current_level=st.current_level,
        highest_level=st.highest_level,
        items=items,
        equipped_weapon=equipped,
        weapon_damage=float(stats.get("damage", 25)),
        weapon_fire_rate=float(stats.get("fire_rate", 0.3)),
    )


@router.get("/me", response_model=PlayerStateResponse)
def me(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    return _state_response(db, user)


@router.post("/state/screen", response_model=ScreenResponse)
def set_screen(req: ScreenRequest, user: User = Depends(get_game_user), db: Session = Depends(get_db)):
    """Game tells the backend which scene it's on; the phone polls and follows."""
    st = _state(db, user)
    st.current_screen = req.screen
    db.commit()
    return ScreenResponse(screen=st.current_screen)


@router.get("/state/screen", response_model=ScreenResponse)
def get_screen(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Phone polls this to know which companion page to show."""
    st = _state(db, user)
    return ScreenResponse(screen=st.current_screen or "wait")


@router.post("/economy/earn", response_model=MoneyResponse)
def earn(req: EarnRequest, user: User = Depends(get_game_user), db: Session = Depends(get_db)):
    """Add (or subtract) money. Used by the shop level. Money never goes below 0."""
    st = _state(db, user)
    st.money = max(0, st.money + req.amount)
    db.commit()
    return MoneyResponse(money=st.money)


@router.get("/shop/items")
def shop_items(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    st = _state(db, user)
    owned = {i.item_id for i in db.query(OwnedItem).filter(OwnedItem.user_id == user.id).all()}
    out = []
    for item in ITEMS:
        out.append({
            **item,
            # пистолет — бесплатный дефолт, считаем его всегда «в наличии»
            "owned": item["id"] in owned or item["id"] == "pistol",
            "unlocked": st.current_level >= item["unlock_level"],
        })
    return {"money": st.money, "equipped_weapon": st.equipped_weapon or "pistol", "items": out}


@router.post("/loadout/weapon", response_model=PlayerStateResponse)
def equip_weapon(req: EquipWeaponRequest, user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Выбрать активное оружие (один ствол). Сайт зовёт это для купленного оружия."""
    item = ITEMS_BY_ID.get(req.item_id)
    if item is None or item["type"] != "weapon":
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Unknown weapon")

    st = _state(db, user)
    # Пистолет доступен всегда (дефолт); остальные стволы нужно сперва купить.
    if req.item_id != "pistol":
        owned = db.query(OwnedItem).filter(
            OwnedItem.user_id == user.id, OwnedItem.item_id == req.item_id
        ).first()
        if owned is None:
            raise HTTPException(status.HTTP_403_FORBIDDEN, "Weapon not owned")

    st.equipped_weapon = req.item_id
    db.commit()
    return _state_response(db, user)


@router.post("/shop/buy", response_model=PlayerStateResponse)
def buy(req: BuyRequest, user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    item = ITEMS_BY_ID.get(req.item_id)
    if item is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Unknown item")

    st = _state(db, user)
    if st.current_level < item["unlock_level"]:
        raise HTTPException(status.HTTP_403_FORBIDDEN, "Item not unlocked yet")

    already = db.query(OwnedItem).filter(
        OwnedItem.user_id == user.id, OwnedItem.item_id == req.item_id
    ).first()
    if already:
        raise HTTPException(status.HTTP_409_CONFLICT, "Item already owned")

    if st.money < item["price"]:
        raise HTTPException(status.HTTP_402_PAYMENT_REQUIRED, "Not enough money")

    st.money -= item["price"]
    db.add(OwnedItem(user_id=user.id, item_id=req.item_id))
    db.commit()
    return _state_response(db, user)


@router.post("/progress/complete-level", response_model=CompleteLevelResponse)
def complete_level(user: User = Depends(get_game_user), db: Session = Depends(get_db)):
    """Called when the player survives the shooter level. Advances progression."""
    st = _state(db, user)
    finished = st.current_level >= settings.MAX_LEVEL
    if not finished:
        st.current_level += 1
        st.highest_level = max(st.highest_level, st.current_level)
        db.commit()
        return CompleteLevelResponse(
            current_level=st.current_level,
            highest_level=st.highest_level,
            finished_game=False,
            next_level_config=config_store.merged_level_config(db, st.current_level),
        )
    db.commit()
    return CompleteLevelResponse(
        current_level=st.current_level,
        highest_level=st.highest_level,
        finished_game=True,
        next_level_config=None,
    )


@router.get("/levels")
def levels(db: Session = Depends(get_db)):
    """Full difficulty table for all levels (useful for the game + site).
    Includes admin overrides from the config store."""
    return {
        "max_level": settings.MAX_LEVEL,
        "economy": config_store.merged_economy(db),
        "levels": config_store.merged_all_levels(db),
    }


@router.get("/levels/{level}")
def one_level(level: int, db: Session = Depends(get_db)):
    return config_store.merged_level_config(db, level)


def _revealed_clue_ids(st: PlayerState) -> list:
    return [c for c in (st.revealed_clues or "").split(",") if c]


def _ensure_revealed(db: Session, st: PlayerState) -> list:
    """Гарантирует, что у игрока раскрыто min(level, |пул|) примет (день = уровень).
    Новые добавляются СЛУЧАЙНО из пула и запоминаются навсегда.
    Устаревшие id (из старого пула) автоматически выкидываются — БД сбрасывать не нужно."""
    ids = [i for i in _revealed_clue_ids(st) if i in CLUES_BY_ID]
    target = min(st.current_level, len(CLUES))
    if len(ids) < target:
        pool = [c["id"] for c in CLUES if c["id"] not in ids]
        random.shuffle(pool)
        ids += pool[: target - len(ids)]
    new_val = ",".join(ids)
    if new_val != (st.revealed_clues or ""):
        st.revealed_clues = new_val
        db.commit()
    return ids


@router.get("/clues")
def my_clues(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Накопленные приметы игрока (эмодзи+название+описание+тип). Растут случайно по дням."""
    st = _state(db, user)
    ids = _ensure_revealed(db, st)
    clues = [CLUES_BY_ID[i] for i in ids if i in CLUES_BY_ID]
    return {"level": st.current_level, "clues": clues}


# ---- Математика в чате (doom): телефон ↔ игра через бэкенд ----

@router.post("/chat/question", response_model=ChatQuestionResponse)
def chat_new_question(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Игра просит новый пример. Если в админке заданы кастомные вопросы — берём случайный
    из них, иначе генерим арифметику. Запоминаем ответ, телефон покажет вопрос."""
    st = _state(db, user)
    pool = config_store.math_questions(db)
    if pool:
        raw = random.choice(pool)
        opts = [int(o) for o in raw.get("options", [])][:3]
        q = {"text": raw.get("text", "?"), "options": opts, "answer": int(raw.get("answer", 0))}
    else:
        q = make_math_question()
    st.chat_q_id = (st.chat_q_id or 0) + 1
    st.chat_q_text = q["text"]
    st.chat_q_options = ",".join(str(o) for o in q["options"])
    st.chat_q_answer = q["answer"]
    st.chat_q_status = "pending"
    db.commit()
    return ChatQuestionResponse(id=st.chat_q_id, text=q["text"], options=q["options"], status="pending")


def _options_list(st: PlayerState) -> list:
    return [int(x) for x in (st.chat_q_options or "").split(",") if x.strip()]


@router.get("/chat/question", response_model=ChatQuestionResponse)
def chat_get_question(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Телефон опрашивает текущий вопрос (текст + варианты, без правильного ответа)."""
    st = _state(db, user)
    return ChatQuestionResponse(
        id=st.chat_q_id or 0,
        text=st.chat_q_text or "",
        options=_options_list(st),
        status=st.chat_q_status or "idle",
    )


@router.post("/chat/answer", response_model=ChatAnswerResponse)
def chat_answer(req: ChatAnswerRequest, user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Телефон присылает выбранный вариант. Бэк ставит статус correct/wrong."""
    st = _state(db, user)
    if req.id != st.chat_q_id or st.chat_q_status != "pending":
        return ChatAnswerResponse(correct=False, status=st.chat_q_status or "idle")
    correct = req.choice == st.chat_q_answer
    st.chat_q_status = "correct" if correct else "wrong"
    db.commit()
    return ChatAnswerResponse(correct=correct, status=st.chat_q_status)


@router.get("/chat/state", response_model=ChatStateResponse)
def chat_state(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Игра опрашивает: ответил ли игрок и верно ли (чтобы снять/не снимать HP)."""
    st = _state(db, user)
    return ChatStateResponse(id=st.chat_q_id or 0, status=st.chat_q_status or "idle")


# ---- Диалоги на телефоне (интро / переход) + «гейт» старта сцены ----

@router.get("/dialogue/{key}")
def get_dialogue(key: str, user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Узлы диалога по ключу (intro | doom_to_shop), редактируются в админке."""
    return {"key": key, "nodes": config_store.dialogue(db, key)}


@router.get("/intro/questions")
def intro_questions(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Арифметические вопросы для чата (из пула админки, иначе генерим несколько)."""
    pool = config_store.math_questions(db)
    if pool:
        out = [{"text": q.get("text", "?"),
                "options": [int(o) for o in q.get("options", [])][:3]} for q in pool]
    else:
        out = []
        for _ in range(3):
            q = make_math_question()
            out.append({"text": q["text"], "options": q["options"]})
    return {"questions": out}


# «Гейт» один на игрока: игра ставит его в False перед ожиданием, телефон —
# в True по окончании диалога. Используется и для link→doom, и для doom→shop.

@router.post("/gate/reset")
def gate_reset(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    st = _state(db, user)
    st.intro_done = False
    db.commit()
    return {"done": False}


@router.post("/gate/done")
def gate_done(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Телефон: диалог пройден → игра может грузить следующую сцену."""
    st = _state(db, user)
    st.intro_done = True
    db.commit()
    return {"done": True}


@router.get("/gate/status")
def gate_status(user: User = Depends(get_current_user), db: Session = Depends(get_db)):
    """Игра опрашивает: закончил ли игрок диалог на телефоне."""
    st = _state(db, user)
    return {"done": bool(st.intro_done)}
