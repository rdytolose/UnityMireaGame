from pydantic import BaseModel, EmailStr, Field


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


class PairStartResponse(BaseModel):
    code: str
    qr_url: str
    qr_png_base64: str
    poll_secret: str
    expires_in: int


class PairStatusResponse(BaseModel):
    status: str
    game_token: str | None = None
    username: str | None = None


class PairConfirmRequest(BaseModel):
    code: str


class PairQuickConfirmResponse(BaseModel):
    """No-login pairing: scanning the QR creates an anonymous player."""
    site_token: str
    username: str


class ScreenRequest(BaseModel):
    screen: str


class ScreenResponse(BaseModel):
    screen: str


class PlayerStateResponse(BaseModel):
    username: str
    money: int
    current_level: int
    highest_level: int
    items: list[str]
    equipped_weapon: str = "pistol"
    weapon_damage: float = 25
    weapon_fire_rate: float = 0.3
    weapon_range: float = 100


class EquipWeaponRequest(BaseModel):
    item_id: str


class EarnRequest(BaseModel):
    amount: int


class BuyRequest(BaseModel):
    item_id: str


class MoneyResponse(BaseModel):
    money: int


class CompleteLevelResponse(BaseModel):
    current_level: int
    highest_level: int
    finished_game: bool
    next_level_config: dict | None = None


class ChatQuestionResponse(BaseModel):
    id: int
    text: str = ""
    options: list[int] = []
    status: str = "idle"


class ChatAnswerRequest(BaseModel):
    id: int
    choice: int


class ChatAnswerResponse(BaseModel):
    correct: bool
    status: str


class ChatStateResponse(BaseModel):
    id: int
    status: str
