import base64
import io
import secrets
from datetime import datetime, timedelta, timezone

import qrcode
from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from ..database import get_db
from ..models import PairingSession, PlayerState, User
from ..schemas import (
    PairConfirmRequest,
    PairQuickConfirmResponse,
    PairStartResponse,
    PairStatusResponse,
)
from ..security import create_token, get_site_user
from ..settings import settings


router = APIRouter(prefix="/api/pair", tags=["pairing"])


def _generate_code() -> str:
    alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
    return "".join(secrets.choice(alphabet) for _ in range(6))


def _qr_png_base64(data: str) -> str:
    img = qrcode.make(data)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("ascii")


@router.post("/start", response_model=PairStartResponse)
def pair_start(db: Session = Depends(get_db)):
    """Called by the game (exe) at launch. Returns a code + QR to display."""
    for _ in range(10):
        code = _generate_code()
        if not db.query(PairingSession).filter(PairingSession.code == code).first():
            break
    else:
        raise HTTPException(status.HTTP_500_INTERNAL_SERVER_ERROR, "Could not allocate code")

    poll_secret = secrets.token_urlsafe(24)
    expires_at = datetime.now(timezone.utc) + timedelta(seconds=settings.PAIRING_TTL_SEC)

    session = PairingSession(
        code=code,
        status="pending",
        poll_secret=poll_secret,
        expires_at=expires_at,
    )
    db.add(session)
    db.commit()

    qr_url = f"{settings.SITE_BASE_URL.rstrip('/')}/pair?code={code}"
    return PairStartResponse(
        code=code,
        qr_url=qr_url,
        qr_png_base64=_qr_png_base64(qr_url),
        poll_secret=poll_secret,
        expires_in=settings.PAIRING_TTL_SEC,
    )


def _is_expired(session: PairingSession) -> bool:
    exp = session.expires_at
    if exp.tzinfo is None:
        exp = exp.replace(tzinfo=timezone.utc)
    return datetime.now(timezone.utc) > exp


@router.get("/status", response_model=PairStatusResponse)
def pair_status(code: str, poll_secret: str, db: Session = Depends(get_db)):
    """Polled by the game until the user confirms on their phone."""
    session = db.query(PairingSession).filter(PairingSession.code == code).first()
    if session is None or session.poll_secret != poll_secret:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Pairing session not found")

    if session.status == "pending" and _is_expired(session):
        session.status = "expired"
        db.commit()

    if session.status != "linked":
        return PairStatusResponse(status=session.status)

    user = db.query(User).filter(User.id == session.user_id).first()
    game_token = create_token(user.id, "game", settings.GAME_TOKEN_TTL_MIN)
    session.status = "consumed"
    db.commit()
    return PairStatusResponse(status="linked", game_token=game_token, username=user.username)


@router.post("/quick-confirm", response_model=PairQuickConfirmResponse)
def pair_quick_confirm(req: PairConfirmRequest, db: Session = Depends(get_db)):
    """No-login pairing. Scanning the QR opens the site, which calls this with the
    code: we create a fresh anonymous player, link the pairing, and hand the phone
    a site token. The game then polls /status and gets its game token. No accounts,
    no passwords."""
    code = req.code.strip().upper()
    session = db.query(PairingSession).filter(PairingSession.code == code).first()
    if session is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Invalid code")
    if _is_expired(session):
        session.status = "expired"
        db.commit()
        raise HTTPException(status.HTTP_410_GONE, "Code expired")
    if session.status not in ("pending",):
        raise HTTPException(status.HTTP_409_CONFLICT, f"Code already {session.status}")

    suffix = secrets.token_hex(4)
    user = User(
        email=f"anon_{code}_{suffix}@local",
        username=f"Игрок-{code}",
        password_hash="",
    )
    db.add(user)
    db.flush()
    db.add(PlayerState(
        user_id=user.id,
        money=settings.STARTING_MONEY,
        current_level=1,
        highest_level=1,
        current_screen="wait",
    ))
    session.user_id = user.id
    session.status = "linked"
    db.commit()

    site_token = create_token(user.id, "site", settings.SITE_TOKEN_TTL_MIN)
    return PairQuickConfirmResponse(site_token=site_token, username=user.username)


@router.post("/confirm", response_model=PairStatusResponse)
def pair_confirm(
    req: PairConfirmRequest,
    user: User = Depends(get_site_user),
    db: Session = Depends(get_db),
):
    """Called from the website (phone) after the user logs in and enters/scans the code."""
    session = db.query(PairingSession).filter(PairingSession.code == req.code.strip().upper()).first()
    if session is None:
        raise HTTPException(status.HTTP_404_NOT_FOUND, "Invalid code")
    if _is_expired(session):
        session.status = "expired"
        db.commit()
        raise HTTPException(status.HTTP_410_GONE, "Code expired")
    if session.status not in ("pending",):
        raise HTTPException(status.HTTP_409_CONFLICT, f"Code already {session.status}")

    session.user_id = user.id
    session.status = "linked"
    db.commit()
    return PairStatusResponse(status="linked", username=user.username)
