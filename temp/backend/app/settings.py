import os


class Settings:
    """Runtime configuration read from environment variables."""

    JWT_SECRET: str = os.getenv("JWT_SECRET", "dev-secret-change-me")
    JWT_ALGORITHM: str = "HS256"

    ADMIN_KEY: str = os.getenv("ADMIN_KEY", "admin")

    SITE_TOKEN_TTL_MIN: int = int(os.getenv("SITE_TOKEN_TTL_MIN", str(60 * 24 * 7)))
    GAME_TOKEN_TTL_MIN: int = int(os.getenv("GAME_TOKEN_TTL_MIN", str(60 * 24 * 30)))

    PAIRING_TTL_SEC: int = int(os.getenv("PAIRING_TTL_SEC", "300"))

    SITE_BASE_URL: str = os.getenv("SITE_BASE_URL", "http://localhost:5173")

    DATABASE_URL: str = os.getenv("DATABASE_URL", "sqlite:///./cashier_doom.db")

    STARTING_MONEY: int = int(os.getenv("STARTING_MONEY", "0"))

    MAX_LEVEL: int = int(os.getenv("MAX_LEVEL", "10"))


settings = Settings()
