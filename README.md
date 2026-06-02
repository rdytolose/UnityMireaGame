# Cashier & Doom — сервер (бэкенд + фронт): запуск и деплой

Здесь живёт серверная часть игры:
- **`backend/`** — FastAPI (Python), SQLite в docker-volume.
- **`frontend/`** — статический сайт-компаньон (телефон) + nginx, который и раздаёт
  сайт, и проксирует `/api/*` на бэкенд.
- **`docker-compose.yml`** — поднимает оба контейнера.

Наружу торчит только **фронт-nginx на порту `8081`** (его же адрес зашит в игре в
`BackendConfig.BaseUrl`). Бэкенд доступен только внутри сети compose.

---

## 1. Локальный запуск (для проверки)

Нужен **Docker** с плагином **compose v2**.

```bash
cd temp
cp .env.example .env          # (по желанию) впиши свои секреты/порт
docker compose up -d --build
```

Проверка:
```bash
curl http://localhost:8081/health        # {"status":"ok"}
curl http://localhost:8081/api/levels     # таблица сложности
```
- Сайт-компаньон: <http://localhost:8081/>
- Админка: <http://localhost:8081/admin.html> (ключ = `ADMIN_KEY` из `.env`)
- Логи: `docker compose logs -f backend`
- Остановить: `docker compose down` (БД сохранится; `down -v` — стереть БД).

> Для локального теста пейринга с телефона `SITE_BASE_URL` должен быть адресом,
> доступным телефону (IP машины в сети, напр. `http://192.168.0.10:8081`), и игра
> должна стучаться туда же.

### Запуск без Docker (если очень надо)
```bash
cd temp/backend
pip install .                 # ставит зависимости из pyproject.toml
uvicorn app.main:app --host 0.0.0.0 --port 8000
# фронт — любым статик-сервером из temp/frontend, и проксировать /api на :8000
```
Docker-вариант проще — рекомендую его.

---

## 2. Залить на GitHub

Репозиторий: **`rdytolose/UnityMireaGame`** (remote `origin` уже настроен).

```bash
# из корня репозитория (папка Assets)
git add -A
git commit -m "server + ci/cd"
git push origin main
```

Секреты в репозиторий **не попадают**: реальные значения лежат в `.env` на сервере,
а `.env` в `.gitignore`. В репо — только `.env.example` (шаблон).

---

## 3. CI/CD: автодеплой при пуше

Workflow **`.github/workflows/deploy.yml`** при пуше в `main` работает в два этапа:
1. **build** — собирает образы `backend` и `frontend` прямо в GitHub Actions и пушит
   их в **GHCR** (`ghcr.io/rdytolose/cashier-doom-backend|frontend`).
2. **deploy** — заходит по SSH на сервер, делает `git reset --hard origin/main`
   (ради свежего `docker-compose.yml`), `docker login ghcr.io`, `docker compose pull`
   и `docker compose up -d`. **Сервер ничего не компилирует — только тянет образы.**

Для входа в GHCR используется встроенный `GITHUB_TOKEN` (пробрасывается на сервер в
workflow) — отдельный секрет для реестра создавать не нужно.

### Шаг 1. Подготовить сервер (один раз)
```bash
# поставить docker + compose
curl -fsSL https://get.docker.com | sh
sudo systemctl enable --now docker

# склонировать репо в рабочую папку (нужно ради docker-compose.yml и .env)
git clone https://github.com/rdytolose/UnityMireaGame.git ~/UnityMireaGame
cd ~/UnityMireaGame/temp
cp .env.example .env          # впиши реальные JWT_SECRET / ADMIN_KEY / SITE_BASE_URL

# первый запуск можно собрать локально, дальше всё тянется из GHCR:
docker compose up -d --build  # проверить, что всё поднялось

# открыть порт
sudo ufw allow 8081/tcp       # + открыть 8081 в фаерволе хостинга, если есть
```

> `DEPLOY_PATH` в секретах = **абсолютный путь к этому клону** (напр.
> `/home/youruser/UnityMireaGame`, без `~`). Внутри workflow сам делает `cd temp`.

### Шаг 2. Сгенерить SSH-ключ для деплоя (на своей машине)
```bash
ssh-keygen -t ed25519 -f deploy_key -N ""
# публичную часть — на сервер, в авторизованные ключи:
ssh-copy-id -i deploy_key.pub user@СЕРВЕР      # или вручную в ~/.ssh/authorized_keys
# приватную часть (файл deploy_key) — в секреты GitHub (ниже)
```

### Шаг 3. Прописать секреты репозитория
GitHub → твой репозиторий → **Settings → Secrets and variables → Actions → New
repository secret**. Создай:

| Secret | Значение |
|--------|----------|
| `SSH_HOST` | IP/домен сервера (напр. `game.podrik150cm.space`) |
| `SSH_USER` | пользователь SSH (напр. `root` или `deploy`) |
| `SSH_KEY` | **приватный** ключ целиком (содержимое файла `deploy_key`) |
| `SSH_PORT` | порт SSH (обычно `22`) |
| `DEPLOY_PATH` | путь к клону на сервере (напр. `/home/youruser/UnityMireaGame`) |

### Шаг 4. Проверить
- Сделай пуш в `main` → вкладка **Actions** в GitHub покажет job `Deploy`.
- Или запусти вручную: Actions → Deploy → **Run workflow**.
- На сервере убедись: `docker compose ps` (оба Up), `docker compose logs -f`.

Готово: теперь **каждый пуш в `main` сам обновляет бек+фронт на сервере**.

---

## 4. Важные мелочи

- **Порт и адрес.** Внешний порт фронта (`FRONTEND_PORT`, дефолт 8081) и
  `SITE_BASE_URL` должны совпадать с тем, что зашито в игре
  (`Assets/scripts/Backend/BackendConfig.cs`). Меняешь адрес/порт — меняй в обоих местах.
- **Секреты.** `JWT_SECRET` и `ADMIN_KEY` задавай в `.env` на сервере. Смена
  `JWT_SECRET` разлогинит все токены (придётся переспариться).
- **БД.** SQLite в volume `backend_data` — переживает рестарты и редеплои. Стереть
  прогресс: `docker compose down -v`. Новые колонки добавляются авто-миграцией
  (`_auto_migrate` в `app/main.py`) — `down -v` для них не нужен.
- **Образы в GHCR.** Собираются автоматически в Actions при каждом пуше и лежат в
  пакетах репозитория (`ghcr.io/rdytolose/cashier-doom-backend|frontend`). Каждый
  образ тегается `latest` и `<sha>` коммита — можно откатиться на конкретный тег.
- **Приватность пакетов.** По умолчанию пакеты GHCR приватные; сервер тянет их через
  `GITHUB_TOKEN` (workflow логинится за тебя), так что вручную делать их публичными не
  нужно. Если решишь раздавать образ кому-то ещё — переключи видимость пакета в
  Settings → Packages.

---

## 5. Структура

```
temp/
├── docker-compose.yml      # поднимает backend + frontend
├── .env.example            # шаблон секретов (скопируй в .env на сервере)
├── backend/                # FastAPI: Dockerfile, app/ (роутеры, модели, конфиг)
└── frontend/               # сайт-компаньон: Dockerfile, nginx.conf, *.html, app.js
```
Подробно про код и API — см. `Assets/Docs/` (`API_INTEGRATION.md`,
`ALL_SCRIPTS_REFERENCE.md`).
