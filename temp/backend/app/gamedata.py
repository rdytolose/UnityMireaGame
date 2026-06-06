"""Static game data: shop economy, purchasable items, and per-level difficulty.

These values drive both the Unity client (via the API) and the backend's
validation of purchases / progression.
"""

import random

ECONOMY = {
    "reward_correct_serve": 100,
    "penalty_serve_bad": -300,
    "reward_skip_bad": 100,
    "penalty_skip_good": -100,
}

ITEMS = [
    {"id": "axe",           "name": "Топор",           "type": "weapon", "price": 600,  "unlock_level": 1,
     "stats": {"damage": 80, "fire_rate": 0.50, "range": 2.5, "melee": True}},
    {"id": "pistol",        "name": "Pistol",          "type": "weapon", "price": 0,    "unlock_level": 1,
     "stats": {"damage": 25, "fire_rate": 0.30, "range": 100, "ammo": 80}},
    {"id": "shotgun",       "name": "Shotgun",         "type": "weapon", "price": 1500, "unlock_level": 2,
     "stats": {"damage": 60, "fire_rate": 0.80, "range": 100, "ammo": 40}},
    {"id": "smg",           "name": "SMG",             "type": "weapon", "price": 2500, "unlock_level": 3,
     "stats": {"damage": 18, "fire_rate": 0.08, "range": 100, "ammo": 200}},
    {"id": "rifle",         "name": "Assault Rifle",   "type": "weapon", "price": 4000, "unlock_level": 4,
     "stats": {"damage": 35, "fire_rate": 0.12, "range": 100, "ammo": 150}},
    {"id": "rocket",        "name": "Rocket Launcher", "type": "weapon", "price": 8000, "unlock_level": 6,
     "stats": {"damage": 150, "fire_rate": 1.20, "range": 100, "ammo": 12}},

    {"id": "armor",         "name": "Body Armor",      "type": "perk",   "price": 1200, "unlock_level": 1,
     "stats": {"max_health_bonus": 50}},
    {"id": "speed_boots",   "name": "Speed Boots",     "type": "perk",   "price": 1800, "unlock_level": 2,
     "stats": {"move_speed_mult": 1.25}},
]

ITEMS_BY_ID = {item["id"]: item for item in ITEMS}

MAX_LEVEL = 10

CLUES = [
    {"id": "police_car",     "emoji": "🚓", "name": "Полицейская машина", "text": "У входа стоит полицейская машина",     "type": "object"},
    {"id": "suspicious_man", "emoji": "🕴️", "name": "Подозрительный тип",  "text": "Рядом топчется чей-то подельник",     "type": "object"},
]

CLUES_BY_ID = {c["id"]: c for c in CLUES}


def make_math_question() -> dict:
    """Случайный пример: возвращает {text, options:[3], answer}. Бэк генерит сам."""
    op = random.choice(["+", "-", "×"])
    a, b = random.randint(2, 12), random.randint(2, 12)
    if op == "+":
        ans = a + b
    elif op == "-":
        if b > a:
            a, b = b, a
        ans = a - b
    else:
        ans = a * b
    opts = {ans}
    while len(opts) < 3:
        cand = ans + random.choice([-4, -3, -2, -1, 1, 2, 3, 4])
        if cand >= 0:
            opts.add(cand)
    options = list(opts)
    random.shuffle(options)
    return {"text": f"{a} {op} {b} = ?", "options": options, "answer": ans}


def level_config(level: int) -> dict:
    """Difficulty parameters for a given level (1..MAX_LEVEL).

    Shooter level: survive `survival_time` seconds. Mobs get stronger/faster and
    spawn more often. Shop level: more `clue_types` make bad customers harder to
    spot, and customers arrive a bit faster.
    """
    level = max(1, min(level, MAX_LEVEL))
    step = level - 1

    survival_time = 180 + step * 15

    return {
        "level": level,
        "shooter": {
            "survival_time": survival_time,
            "enemy_health_mult": round(1.0 + step * 0.20, 2),
            "enemy_speed_mult": round(1.0 + step * 0.10, 2),
            "enemy_damage_mult": round(1.0 + step * 0.15, 2),
            "max_enemies": 8 + step * 2,
            "spawn_min_delay": round(max(0.8, 5.0 - step * 0.4), 2),
            "spawn_max_delay": round(max(2.0, 15.0 - step * 1.0), 2),
        },
        "shop": {
            "clue_types": min(2 + step, 11),
            "bad_chance": round(min(0.3 + step * 0.04, 0.7), 2),
            "min_order": 1 + step // 3,
            "max_order": 6 + step,
            "customer_move_duration": round(max(0.5, 1.2 - step * 0.05), 2),
        },
    }


def all_levels() -> list:
    return [level_config(i) for i in range(1, MAX_LEVEL + 1)]
