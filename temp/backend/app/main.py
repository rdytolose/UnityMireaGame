from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy import inspect, text

from .database import Base, engine
from .routers import admin, auth, pairing, player

Base.metadata.create_all(bind=engine)


def _auto_migrate():
    """Лёгкая миграция: добавляет недостающие колонки в существующие таблицы
    (ALTER TABLE ADD COLUMN). Избавляет от ручного пересоздания БД при новых полях."""
    insp = inspect(engine)
    tables = set(insp.get_table_names())
    for table in Base.metadata.sorted_tables:
        if table.name not in tables:
            continue
        existing = {c["name"] for c in insp.get_columns(table.name)}
        for col in table.columns:
            if col.name in existing:
                continue
            coltype = col.type.compile(engine.dialect)
            default = ""
            arg = getattr(col.default, "arg", None) if col.default is not None else None
            if isinstance(arg, bool):
                default = f" DEFAULT {1 if arg else 0}"
            elif isinstance(arg, (int, float)):
                default = f" DEFAULT {arg}"
            elif isinstance(arg, str):
                default = f" DEFAULT '{arg}'"
            with engine.begin() as conn:
                conn.execute(text(f'ALTER TABLE {table.name} ADD COLUMN {col.name} {coltype}{default}'))


_auto_migrate()

app = FastAPI(title="Cashier & Doom Backend", version="0.1.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(auth.router)
app.include_router(pairing.router)
app.include_router(player.router)
app.include_router(admin.router)


@app.get("/")
def root():
    return {"service": "cashier-doom-backend", "status": "ok"}


@app.get("/health")
def health():
    return {"status": "ok"}
