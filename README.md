# Cashier & Doom

Roguelike-игра на Unity: ночная смена в киоске (проверяешь покупателей по приметам,
как в *Papers, Please*) ↔ шутер от первого лица в духе *Doom*. Уровни чередуются,
прошёл 6 — победил. К игре подключается **телефон-компаньон** (сайт): связь по QR,
через него идут часть действий (касса, приметы, чат). Всё это связывает **сервер**
(FastAPI + статический фронт).

---

# Часть 1. Развёртывание сервера через Docker

Серверная часть лежит в папке **`temp/`**:

```
temp/
├── docker-compose.yml   # поднимает оба контейнера
├── .env.example         # шаблон настроек → скопировать в .env
├── backend/             # FastAPI (Python) + SQLite в docker-volume
└── frontend/            # сайт-компаньон + nginx (раздаёт сайт и проксирует /api на backend)
```

Наружу торчит только **nginx-фронт на порту `8081`**; бэкенд доступен лишь внутри
сети compose.

## 1. Быстрый запуск

Нужен **Docker** с плагином **compose v2**.

```bash
cd temp
cp .env.example .env          # СНАЧАЛА впиши свои значения (см. таблицу ниже)
docker compose up -d --build
```

Проверка:
```bash
curl http://localhost:8081/health     # {"status":"ok"}
curl http://localhost:8081/api/levels  # таблица сложности
```
- Сайт-компаньон: <http://localhost:8081/>
- Админка: <http://localhost:8081/admin.html> (ключ = `ADMIN_KEY` из `.env`)
- Логи: `docker compose logs -f backend`
- Остановить: `docker compose down` (БД сохранится; `down -v` — стереть БД).

## 2. ⚙️ Где менять значения

| Что меняешь | Где | Зачем |
|---|---|---|
| `JWT_SECRET` | `temp/.env` | секрет подписи токенов. Поставь длинную случайную строку. Смена — разлогинит всех. |
| `ADMIN_KEY` | `temp/.env` | пароль входа в `/admin.html`. |
| `SITE_BASE_URL` | `temp/.env` | публичный адрес, который кодируется в QR. **Должен совпадать с адресом в игре** (см. ниже). |
| `FRONTEND_PORT` | `temp/.env` | внешний порт фронта (дефолт `8081`). Меняешь — меняй и адрес в игре. |
| `BaseUrl` (адрес сервера в игре) | `scripts/Backend/BackendConfig.cs` | куда Unity-клиент шлёт запросы. Должен указывать на тот же `host:port`, что и `SITE_BASE_URL`/`FRONTEND_PORT`. |

> Правило простое: **адрес и порт в `.env` и в `BackendConfig.cs` должны совпадать.**
> Для теста пейринга с телефона в локалке поставь IP машины в сети, напр.
> `http://192.168.0.10:8081` (а не `localhost`).

## 3. Деплой на свой сервер (продакшен)

```bash
# на сервере: docker
curl -fsSL https://get.docker.com | sh
sudo systemctl enable --now docker

# код + настройки
git clone https://github.com/rdytolose/UnityMireaGame.git ~/UnityMireaGame
cd ~/UnityMireaGame/temp
cp .env.example .env          # впиши боевые значения (таблица выше)
docker compose up -d --build

sudo ufw allow 8081/tcp       # + открыть порт в фаерволе хостинга
```

`.env` в репозиторий **не попадает** (он в `.gitignore`) — секреты живут только на
сервере. В репо лежит лишь `.env.example`.

## 4. CI/CD: автодеплой при пуше (опционально)

Workflow `.github/workflows/deploy.yml` при пуше в `main`:
1. **build** — собирает образы в GitHub Actions и пушит в **GHCR**
   (`ghcr.io/rdytolose/unitymireagame-backend|frontend`);
2. **deploy** — по SSH на сервере: `git reset --hard` → `docker compose pull` →
   `up -d`. Сервер только тянет готовые образы, ничего не компилирует.

Что нужно один раз настроить:
- **Settings → Actions → General → Workflow permissions → Read and write** (иначе
  пуш в GHCR упадёт с `write_package denied`).
- **Секреты репозитория** (Settings → Secrets and variables → Actions):

  | Secret | Значение |
  |--------|----------|
  | `SSH_HOST` | IP/домен сервера |
  | `SSH_USER` | пользователь SSH |
  | `SSH_KEY` | приватный SSH-ключ целиком |
  | `SSH_PORT` | порт SSH (обычно `22`) |
  | `DEPLOY_PATH` | абсолютный путь к клону (напр. `/home/user/UnityMireaGame`) |

- На сервере держи клон репо по этому `DEPLOY_PATH` (workflow сам делает `cd temp`).

## 5. Заметки

- **БД** — SQLite в volume `backend_data`, переживает рестарты/редеплои. Стереть
  прогресс: `docker compose down -v`. Новые колонки добавляются авто-миграцией
  (`_auto_migrate` в `app/main.py`).
- **Без Docker** (если очень надо): `cd temp/backend && pip install . &&
  uvicorn app.main:app --host 0.0.0.0 --port 8000`, фронт — любым статик-сервером с
  проксированием `/api` на `:8000`. Docker проще.

---

# Часть 2. Структура игры

## Как это работает в целом

Игрок связывает игру с телефоном по **QR** (сцена пейринга). Дальше игра и сайт-
компаньон общаются **через сервер**: игра отправляет текущий экран и состояние,
телефон показывает нужный интерфейс (касса, приметы, чат) и шлёт обратно действия.
Сервер хранит прогресс, экономику (деньги), покупки и сложность по уровням.

Цикл уровня: **магазин → экипировка → doom → (чат-переход) → магазин …** Победа — на
6-м уровне; смерть/выход — экран итогов.

## Сцены (`Scenes/`)

| Сцена | Роль |
|---|---|
| `Pairing.unity` | связка с телефоном: QR-код на «старом компьютере» (CRT-моргание). |
| `Shop.unity` | уровень-киоск: обслуживаешь покупателей, проверяешь по **приметам**, зарабатываешь деньги. |
| `equipment.unity` | фаза экипировки между уровнями (таймер на закупку). |
| `doom.unity` | шутер от первого лица: волны врагов, оружие, HP, таймер, GTA-HUD. |
| `Results.unity` | итоги: победа / поражение / выход; кнопки «начать заново» и «выйти» (токен стирается). |

## Скрипты (`scripts/`)

**`Backend/` — связь с сервером:**
- `BackendConfig.cs` — адрес сервера (`BaseUrl`).
- `GameApi.cs` — HTTP-запросы к API.
- `GameSession.cs` — токен и состояние сессии.
- `BackendDTO.cs` — модели данных запросов/ответов.
- `PairingManager.cs` — связка по QR + ожидание «ворот» (gate) с телефона.
- `DifficultyManager.cs` — параметры сложности по номеру уровня.
- `ScreenReporter.cs` — сообщает серверу текущий экран игры.

**`DoomLevel/` — дум-уровень:**
- `DoomPlayerController.cs`, `MouseLook` — управление и камера.
- `DoomWeapon.cs`, `LoadoutManager.cs` — оружие и его параметры (из `/api/me`).
- `DoomEnemy.cs`, `EnemySpawner.cs`, `NavMeshLinkJump.cs` — 3D-враги на NavMesh.
- `DoomHealth.cs`, `DamageScreenEffect.cs` — HP и эффект урона.
- `GtaHud.cs`, `DoomFaceUI.cs`, `Hitmarker.cs`, `HitParticles.cs` — HUD/фидбек.
- `DoomTimer.cs`, `LevelTransitionChat.cs` — таймер и чат-переход между уровнями.
- прочее: звуки, следы, тьма, пул объектов.

**Корень `scripts/` — магазин и общий геймплей:**
- `CustomerManager.cs` — покупатели и **приметы** (clues), начисление денег.
- `ShopTimer.cs`, `EquipmentPhaseManager.cs` — таймеры магазина/экипировки.
- `PlayerInteraction.cs`, `PlayerMovement.cs`, интерактивы (`TVInteractable`,
  `RadioInteractable`, `SecurityCamerasController`, `ShelfItem`, …).
- `SceneTransition.cs` — переходы между сценами.
- `GameOverManager.cs`, `GameStats.cs` — конец игры и статистика.

## Сервер (`temp/backend/app/`)

- `main.py` — приложение FastAPI + авто-миграция БД.
- `routers/auth.py` — регистрация/логин, JWT.
- `routers/pairing.py` — связка игра ↔ телефон по QR.
- `routers/player.py` — состояние игрока, экономика, диалоги, «ворота» (gate).
- `routers/admin.py` — админка: правка конфига и просмотр игроков (`X-Admin-Key`).
- `gamedata.py` — приметы, товары, генерация мат-вопросов.
- `config_store.py` — оверрайды из админки (экономика, уровни, диалоги, оружие).
- `models.py` / `schemas.py` / `database.py` — БД и схемы.

## Сайт-компаньон (`temp/frontend/`)

Статические страницы под телефон + `admin.html`:
`index`/`pair` (связка), `story` (интро), `shop` (касса), `clues` (приметы),
`chat` (мат-чат), `wait` (ожидание), `over` (конец), `admin` (админка).
Логика — `app.js`, адрес API — `config.js`.
