import os


class Settings:
    """Runtime configuration read from environment variables."""

    # Secret used to sign JWTs. Override in production via env var.
    JWT_SECRET: str = os.getenv("JWT_SECRET", "dev-secret-change-me")
    JWT_ALGORITHM: str = "HS256"

    # Admin panel key. Override in production via env var. The admin web page
    # sends it as the `X-Admin-Key` header on every request.
    ADMIN_KEY: str = os.getenv("ADMIN_KEY", "admin")

    # Token lifetimes (minutes).
    SITE_TOKEN_TTL_MIN: int = int(os.getenv("SITE_TOKEN_TTL_MIN", str(60 * 24 * 7)))
    GAME_TOKEN_TTL_MIN: int = int(os.getenv("GAME_TOKEN_TTL_MIN", str(60 * 24 * 30)))

    # How long a pairing code stays valid before it expires (seconds).
    PAIRING_TTL_SEC: int = int(os.getenv("PAIRING_TTL_SEC", "300"))

    # Public URL of the web site that the QR code points to (phone opens this).
    SITE_BASE_URL: str = os.getenv("SITE_BASE_URL", "http://localhost:5173")

    # Database location. Defaults to a local SQLite file.
    DATABASE_URL: str = os.getenv("DATABASE_URL", "sqlite:///./cashier_doom.db")

    # Starting money for a brand new player.
    STARTING_MONEY: int = int(os.getenv("STARTING_MONEY", "0"))

    MAX_LEVEL: int = int(os.getenv("MAX_LEVEL", "10"))


settings = Settings()
