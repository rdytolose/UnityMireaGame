from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from ..database import get_db
from ..models import PlayerState, User
from ..schemas import LoginRequest, RegisterRequest, TokenResponse
from ..security import create_token, hash_password, verify_password
from ..settings import settings

router = APIRouter(prefix="/api/auth", tags=["auth"])


@router.post("/register", response_model=TokenResponse)
def register(req: RegisterRequest, db: Session = Depends(get_db)):
    existing = db.query(User).filter(User.email == req.email).first()
    if existing:
        raise HTTPException(status.HTTP_409_CONFLICT, "Email already registered")

    user = User(
        email=req.email,
        username=req.username,
        password_hash=hash_password(req.password),
    )
    db.add(user)
    db.flush()
    db.add(PlayerState(user_id=user.id, money=settings.STARTING_MONEY, current_level=1, highest_level=1))
    db.commit()

    token = create_token(user.id, "site", settings.SITE_TOKEN_TTL_MIN)
    return TokenResponse(access_token=token, scope="site")


@router.post("/login", response_model=TokenResponse)
def login(req: LoginRequest, db: Session = Depends(get_db)):
    user = db.query(User).filter(User.email == req.email).first()
    if user is None or not verify_password(req.password, user.password_hash):
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, "Invalid email or password")

    token = create_token(user.id, "site", settings.SITE_TOKEN_TTL_MIN)
    return TokenResponse(access_token=token, scope="site")
