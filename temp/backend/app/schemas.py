from pydantic import BaseModel, EmailStr, Field


# ---- Auth ----
class RegisterRequest(BaseModel):
    email: EmailStr
    username: str = Field(min_length=1, max_length=40)
    password: str = Field(min_length=6, max_length=128)


class LoginRequest(BaseModel):
    email: EmailStr
    password: str


class TokenResponse(BaseModel):
    access_token: str
    token_type: str = "bearer"
    scope: str


# ---- Pairing ----
class PairStartResponse(BaseModel):
    code: str
    qr_url: str          # URL encoded into the QR shown by the game
    qr_png_base64: str   # ready-to-render QR image (PNG, base64) for the game
    poll_secret: str     # secret the game uses to poll status
    expires_in: int


class PairStatusResponse(BaseModel):
    status: str                 # pending | linked | consumed | expired
    game_token: str | None = None
    username: str | None = None


class PairConfirmRequest(BaseModel):
    code: str


class PairQuickConfirmResponse(BaseModel):
    """No-login pairing: scanning the QR creates an anonymous player."""
    site_token: str
    username: str


class ScreenRequest(BaseModel):
    screen: str  # wait | clues | shop | chat | pause


class ScreenResponse(BaseModel):
    screen: str


# ---- Player ----
class PlayerStateResponse(BaseModel):
    username: str
    money: int
    current_level: int
    highest_level: int
    items: list[str]
    equipped_weapon: str = "pistol"
    weapon_damage: float = 25      # эффективный урон активного оружия (с оверрайдами админки)
    weapon_fire_rate: float = 0.3  # эффективная скорострельность (пауза между выстрелами)
    weapon_range: float = 100      # дальность луча: ближний бой (топор) = маленькое значение


class EquipWeaponRequest(BaseModel):
    item_id: str


class EarnRequest(BaseModel):
    amount: int  # may be negative (penalty)


class BuyRequest(BaseModel):
    item_id: str


class MoneyResponse(BaseModel):
    money: int


class CompleteLevelResponse(BaseModel):
    current_level: int
    highest_level: int
    finished_game: bool
    next_level_config: dict | None = None


# ---- Chat math minigame (doom) ----
class ChatQuestionResponse(BaseModel):
    id: int                     # 0 = вопроса нет
    text: str = ""
    options: list[int] = []
    status: str = "idle"        # idle | pending | correct | wrong


class ChatAnswerRequest(BaseModel):
    id: int
    choice: int


class ChatAnswerResponse(BaseModel):
    correct: bool
    status: str


class ChatStateResponse(BaseModel):
    id: int
    status: str                 # idle | pending | correct | wrong
