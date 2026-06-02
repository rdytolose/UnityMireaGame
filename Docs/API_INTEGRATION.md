# Cashier & Doom — как игра связывается с сервером (API)

Гайд **только по скриптам, которые обращаются к бэкенду**. Объясняет, кто, когда и
зачем шлёт запросы, какие эндпоинты дёргает и как это связано в общий поток.

Бэкенд — FastAPI на `BackendConfig.BaseUrl` (`http://game.podrik150cm.space:8081`),
nginx проксирует `/api/*` на контейнер `backend:8000`. Тот же домен раздаёт сайт-
компаньон (телефон). Игра и телефон работают с **одним игроком**, но разными
токенами:
- **game-токен** (scope `game`) — у игры, получает при пейринге, хранится в
  `PlayerPrefs["cd_game_token"]`.
- **site-токен** (scope `site`) — у телефона, в localStorage браузера.

Координация «игра ↔ телефон» идёт **через сервер**: одна сторона пишет состояние,
другая опрашивает (`current_screen`, гейт диалога, вопрос математики).

---

## 0. Транспортный слой (фундамент)

### `BackendConfig.cs`
Просто константа `BaseUrl`. От неё строятся все URL. Меняешь адрес сервера тут.

### `GameApi.cs` — HTTP-клиент (MonoBehaviour, DontDestroyOnLoad)
Единственный, кто реально шлёт HTTP. Остальные ходят через него.
- Синглтон `Instance`; `Ensure()` создаёт объект, если его нет в сцене.
- `Get(path, cb, auth)` / `Post(path, json, cb, auth)` → корутина `Send` на
  `UnityWebRequest`. Колбэк `(bool ok, string body)`.
- Если `auth==true` и есть токен — добавляет заголовок `Authorization: Bearer <token>`.
- Токен: `Token` (get/set в PlayerPrefs), `HasToken`, `ClearToken()`.
- `DontDestroyOnLoad` → запрос не прерывается при смене сцены (важно: POST успевает
  уйти даже если сразу грузим другую сцену).

### `BackendDTO.cs` — контейнеры под JSON
`[Serializable]`-классы для `JsonUtility`. Имена полей **обязаны совпадать** с JSON
бэка. Ключевые: `MeResp` (деньги, уровни, items, `equipped_weapon`,
`weapon_damage/fire_rate`), `LevelConfig`/`ShooterCfg`/`ShopCfg`, `CluesResp`/`ClueDTO`,
`ChatQuestionResp`/`ChatStateResp`, `IntroStatusResp` (гейт), `PairStartResp`/`PairStatusResp`.
Логики нет — только парсинг.

### `GameSession.cs` — фасад над эндпоинтами
Статические обёртки: вызываешь `GameSession.Earn(...)`, а оно само зовёт
`GameApi.Instance.Post/Get`, парсит ответ в DTO и отдаёт в колбэк. Методы:
`Earn`, `Buy`, `CompleteLevel`, `SetScreen`, `FetchMe`, `FetchClues`,
`ChatNewQuestion`, `FetchChatState`. (Гейт/диалоги некоторые скрипты дёргают
напрямую через `GameApi`, см. ниже.)

> **Правило:** все обращения к серверу идут так:
> `скрипт → GameSession (или напрямую GameApi) → GameApi.Send → UnityWebRequest → /api/...`

---

## 1. Пейринг и «гейт» старта сцены

### `PairingManager.cs` (сцена Pairing)
- `Start`: `GameApi.Ensure()`. Если токен уже есть и `skipIfAlreadyPaired` — сразу
  грузит игровую сцену.
- `StartPairing`: **POST `/api/pair/start`** (без токена) → получает `code`,
  `qr_png_base64`, `poll_secret`. Рисует QR.
- `PollLoop`: каждые `pollInterval` сек **GET `/api/pair/status?code&poll_secret`**.
  Когда телефон подтвердил — ответ `linked` + `game_token`; сохраняет
  `GameApi.Token`.
- `WaitIntroThenLoad`: **POST `/api/gate/reset`** (сбросить флаг), затем опрашивает
  **GET `/api/gate/status`** пока телефон не пройдёт интро-чат (`done==true`) или
  таймаут → грузит игровую сцену. То есть **игра ждёт окончания диалога на телефоне**.

Сервер: `routers/pairing.py` (start/status/quick-confirm) + `player.py` (gate).

---

## 2. Сложность уровня

### `DifficultyManager.cs` (по одному в Shop и doom)
- `Start` → `FetchAndApply`:
  1. **GET `/api/me`** → берёт `current_level`.
  2. **GET `/api/levels/{level}`** → `LevelConfig` (survival_time, множители врагов,
     спавн, параметры лавки). Кладёт в статик `DifficultyManager.Current`.
  3. `Apply()` раскидывает по сцене: `DoomTimer.SetTotalTime`, `EnemySpawner`
     (maxEnemies/задержки), либо `CustomerManager` (badChance/заказ).
- Бэк отдаёт **merged-конфиг** (дефолт + оверрайды из админки) — см. `config_store.py`.

---

## 3. Синхронизация экрана телефона

### `ScreenReporter.cs` (drop-in в каждой сцене)
- `Start` → **POST `/api/state/screen`** со значением `screen` (wait|clues|shop|chat|…).
  Телефон опрашивает `/api/state/screen` и сам переходит на нужную страницу.
- Так игра «говорит» телефону, что показывать, в зависимости от активной сцены.

Этот же механизм используют `EquipmentPhaseManager`, `LevelTransitionChat`,
`GameStats` — они зовут `GameSession.SetScreen(...)` вручную в нужный момент.

---

## 4. Экономика, прогресс, снаряжение

### `CustomerManager.cs` (лавка)
- `Start`: **GET `/api/clues`** (`GameSession.FetchClues`) — накопленные приметы
  плохого клиента; **GET `/api/me`** (`FetchMe`) — стартовый баланс для табло.
- При обслуживании/скипе: **POST `/api/economy/earn`** (`GameSession.Earn(delta, cb)`)
  — начисляет/списывает деньги; колбэк возвращает авторитетную сумму, ею
  корректируется табло.

### `LoadoutManager.cs` (doom)
- `Start`: **GET `/api/me`** (`FetchMe`) → `equipped_weapon`, список `items`,
  `weapon_damage`, `weapon_fire_rate`. Применяет к `DoomWeapon` (урон/скорострельность
  + контроллер/звук ствола) и перки (armor/speed_boots). Урон/скорострельность
  редактируются в админке и приходят именно из `/api/me`.

### `DoomTimer.cs` (doom)
- По концу таймера: **POST `/api/progress/complete-level`** (`GameSession.CompleteLevel`)
  — двигает уровень игрока на бэке. Затем решает: победа (сцена итогов) или переход
  в магазин.

### Покупки (телефон, не Unity)
Покупка/экипировка оружия идёт с **сайта**: `shop.html` → `/api/shop/items`,
`/api/shop/buy`, `/api/loadout/weapon`. Игра видит результат через `/api/me`.

---

## 5. Математика в чате (doom)

### `ChatMathManager.cs` (doom)
Координация игра↔телефон полностью через сервер:
- **POST `/api/chat/question`** (`GameSession.ChatNewQuestion`) — игра просит новый
  пример; бэк генерит (или берёт из пула админки), запоминает ответ, отдаёт `id`.
  Телефон (`chat.html`) показывает его, опрашивая `/api/chat/question`.
- **GET `/api/chat/state`** (`GameSession.FetchChatState`) — игра опрашивает: ответил
  ли игрок и верно ли. По `status` (`pending/correct/wrong`) решает: молчит дольше
  grace → урон + подсказка; неверно → урон.
- Телефон отвечает через **POST `/api/chat/answer`**.

---

## 6. Переходы и конец игры

### `EquipmentPhaseManager.cs` (сцена EquipmentShop)
- `Start`: `GameApi.Ensure()`, **POST `/api/state/screen` = `shop`** (покупки на
  телефоне). Геймплея нет — только таймер; по нулю грузит doom.

### `LevelTransitionChat.cs` (переход doom→shop)
- **POST `/api/gate/reset`** + **POST `/api/state/screen` = `talk`** → телефон уходит
  на переходный диалог `story.html?key=doom_to_shop`.
- Опрашивает **GET `/api/gate/status`** пока телефон не закончит диалог (`done`), затем
  грузит магазин. Диалог показывается **один раз за забег** (флаг в `GameStats`), далее
  просто видео N секунд.

### `GameStats.cs` (синглтон, DontDestroyOnLoad)
- `End(outcome)`: **POST `/api/state/screen` = `over_win|over_lose|over_quit`** —
  телефон показывает исход и уходит на главный экран. Затем грузит сцену итогов.
  (Сама статистика копится локально, без сервера.)

### `GameOverManager.cs` (сцена Results)
- `Start`: **GET `/api/me`** (`FetchMe`) — итоговый баланс для экрана.
- Кнопка «Начать заново»/«Выйти»: `GameApi.ClearToken()` — стирает game-токен (новый
  забег спарится заново).

---

## 7. Таблица «скрипт → эндпоинт»

| Скрипт | Метод/эндпоинт | Когда | Зачем |
|--------|----------------|-------|-------|
| GameApi | (любой) | — | транспорт всех запросов |
| PairingManager | POST /api/pair/start | старт Pairing | получить код+QR |
| PairingManager | GET /api/pair/status | опрос | дождаться линка, забрать game-токен |
| PairingManager | POST /api/gate/reset, GET /api/gate/status | после линка | ждать интро-чат на телефоне |
| DifficultyManager | GET /api/me, GET /api/levels/{n} | старт сцены | конфиг сложности уровня |
| ScreenReporter | POST /api/state/screen | старт сцены | сказать телефону страницу |
| CustomerManager | GET /api/clues, GET /api/me, POST /api/economy/earn | лавка | приметы, баланс, деньги |
| LoadoutManager | GET /api/me | старт doom | оружие+статы+перки |
| DoomTimer | POST /api/progress/complete-level | конец боя | +1 уровень |
| ChatMathManager | POST /api/chat/question, GET /api/chat/state | бой | математика в чате |
| EquipmentPhaseManager | POST /api/state/screen | старт EquipmentShop | экран «магазин в телефоне» |
| LevelTransitionChat | POST /api/gate/reset, GET /api/gate/status, POST /api/state/screen | doom→shop | переходный диалог |
| GameStats | POST /api/state/screen | конец игры | показать исход на телефоне |
| GameOverManager | GET /api/me, (ClearToken) | сцена итогов | итоговый баланс, сброс токена |

---

## 8. Бэкенд-роутеры (что отвечает на запросы)

- `routers/auth.py` — регистрация/логин (site-токен). Для quick-confirm не нужен.
- `routers/pairing.py` — `/api/pair/start|status|quick-confirm|confirm`. Связывает
  игру и телефон, выдаёт токены.
- `routers/player.py` — основное игровое API: `/me`, `/state/screen`, `/economy/earn`,
  `/shop/items|buy`, `/loadout/weapon`, `/progress/complete-level`, `/levels(/{n})`,
  `/clues`, `/chat/*`, `/dialogue/{key}`, `/gate/*`. Скоупы токенов проверяет
  `security.py` (`get_current_user/get_game_user/get_site_user`).
- `routers/admin.py` — `/api/admin/*` (ключ `X-Admin-Key`): правка конфига
  (экономика, сложность, матвопросы, диалоги, оружие) и игроков. Игра сюда не ходит —
  только админ-страница.

Оверрайды из админки складываются поверх дефолтов в `config_store.py`, поэтому
`/api/me`, `/api/levels`, `/api/clues` уже отдают «смерженные» значения — игра
получает актуальные настройки без своих правок.
