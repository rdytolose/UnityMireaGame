# Cashier & Doom — Backend

FastAPI backend for the Unity game. Handles player accounts, QR device pairing
(game ↔ website), the in-game economy (money), weapon/perk purchases, and level
progression / difficulty (levels 1–10).

## Run locally

```bash
uv venv .venv && source .venv/bin/activate
uv pip install -e .
uvicorn app.main:app --reload
```

Interactive docs: http://localhost:8000/docs

### Environment variables

| Var | Default | Purpose |
|-----|---------|---------|
| `JWT_SECRET` | `dev-secret-change-me` | JWT signing secret (set in prod!) |
| `SITE_BASE_URL` | `http://localhost:5173` | URL encoded in the QR (phone opens it) |
| `DATABASE_URL` | `sqlite:///./cashier_doom.db` | DB connection string |
| `PAIRING_TTL_SEC` | `300` | Pairing code lifetime |
| `STARTING_MONEY` | `0` | Money for a new player |
| `MAX_LEVEL` | `10` | Number of levels |

## QR pairing flow

1. **Game (exe)** calls `POST /api/pair/start` → gets `code`, `qr_url`,
   `qr_png_base64` (render this QR), and `poll_secret`.
2. Player **scans the QR with a phone** → opens `SITE_BASE_URL/pair?code=XXXX`.
3. On the **website**, the player registers/logs in (`/api/auth/*`) and then
   calls `POST /api/pair/confirm` with the code.
4. **Game** polls `GET /api/pair/status?code=...&poll_secret=...`; once linked it
   receives a one-time **game token** and starts the intro cutscene.

Two JWT scopes exist: `site` (issued to the website/phone) and `game` (issued to
the exe). Mutating game endpoints require the `game` scope.

## API summary

### Auth (`site` token)
- `POST /api/auth/register` `{email, username, password}` → `{access_token, scope}`
- `POST /api/auth/login` `{email, password}` → `{access_token, scope}`

### Pairing
- `POST /api/pair/start` → code + QR (no auth; called by the game)
- `GET  /api/pair/status?code=&poll_secret=` → status / game token (game polls)
- `POST /api/pair/confirm` `{code}` (site token) → links account to the game

### Player & economy
- `GET  /api/me` (any token) → `{username, money, current_level, highest_level, items}`
- `POST /api/economy/earn` `{amount}` (game token) → adds/subtracts money (floored at 0)
- `GET  /api/shop/items` → catalog with `owned` / `unlocked` flags
- `POST /api/shop/buy` `{item_id}` (game token) → buy a weapon/perk
- `POST /api/progress/complete-level` (game token) → advance level + next config

### Reference data
- `GET /api/levels` → economy values + full difficulty table for all 10 levels
- `GET /api/levels/{n}` → difficulty config for one level

## Economy (money, ×100 vs original score)

| Action | Money |
|--------|-------|
| Served a good customer correctly | +100 |
| Served a bad customer | −300 |
| Correctly skipped a bad customer | +100 |
| Wrongly skipped a good customer | −100 |

## Difficulty scaling

Per level the shooter survival time, enemy health/speed/damage, spawn rate and
max enemies ramp up; the shop unlocks more clue types, raises the bad-customer
chance and order sizes. See `app/gamedata.py` (`level_config`).
