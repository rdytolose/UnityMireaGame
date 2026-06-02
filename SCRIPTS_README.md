# Cashier & Doom — справочник по всем скриптам

Документация по каждому скрипту: что делает и как работает изнутри.
Игра — рогалик: ночная лавка (Papers-Please-стиль) ↔ Doom-шутер, связанные
FastAPI-бэкендом и телефоном-компаньоном (второй экран по QR).

Петля: **Pairing → (интро-чат) → Shop → EquipmentShop → doom → (переходный чат) → Shop …**
Победа на 6-м уровне / смерть / выход → сцена итогов.

---

## 1. Сеть и бэкенд-клиент (`scripts/Backend/`)

### BackendConfig.cs
Статический класс с одной константой `BaseUrl` — адрес бэкенда
(`http://game.podrik150cm.space:8081`). Все запросы строятся от него.

### BackendDTO.cs
`[Serializable]`-классы для `JsonUtility` (имена полей = JSON бэка): `MeResp`
(деньги, уровни, items, `equipped_weapon`, `weapon_damage`, `weapon_fire_rate`),
`CompleteLevelResp`, `LevelConfig`/`ShooterCfg`/`ShopCfg`, `ClueDTO`/`CluesResp`,
`ChatQuestionResp`/`ChatStateResp`, `IntroStatusResp` (гейт), `PairStartResp`/`PairStatusResp`.
Чистые контейнеры данных, логики нет.

### GameApi.cs (MonoBehaviour, DontDestroyOnLoad)
Тонкий HTTP-клиент. Синглтон (`Instance`), `Ensure()` создаёт его при отсутствии.
Токен игры хранится в `PlayerPrefs["cd_game_token"]` (`Token` get/set, `HasToken`,
`ClearToken`). `Get/Post(path, cb, auth)` запускают корутину `Send` на
`UnityWebRequest`; при `auth && HasToken` шлёт заголовок `Authorization: Bearer`.
Колбэк `(ok, body)`. DontDestroyOnLoad — запросы переживают смену сцены.

### GameSession.cs (статический фасад)
Обёртки над эндпоинтами поверх `GameApi`: `Earn`, `Buy`, `CompleteLevel`,
`SetScreen` (телефону какой экран показать), `FetchMe`, `FetchClues`,
`ChatNewQuestion`/`FetchChatState` (математика в чате). Парсит JSON через
`JsonUtility` и отдаёт типизированные DTO в колбэк.

### PairingManager.cs (сцена Pairing)
Показывает QR/код, ждёт скан. `Start`: если `skipIfAlreadyPaired && HasToken` —
сразу `GoIntro()` (грузит `introSceneName`). Иначе `StartPairing` → POST
`/api/pair/start` (получает code+QR PNG base64) → `PollLoop` опрашивает
`/api/pair/status` каждые `pollInterval`, пока не придёт `linked` + `game_token`.
После линка `WaitIntroThenLoad`: сбрасывает гейт (`/api/gate/reset`) и ждёт, пока
телефон пройдёт интро-диалог (`/api/gate/status.done`), макс `introMaxWait`, затем
грузит сцену. То есть **игра не стартует, пока не пройден интро-чат**.

### DifficultyManager.cs (по одному в Shop и doom)
`Mode` = Shooter|Shop. `Start` → `FetchAndApply`: GET `/api/me` (узнать
`current_level`) → GET `/api/levels/{lvl}` → кладёт `LevelConfig` в статик
`Current` и зовёт `GameStats.SetLevel`. `Apply`: для Shooter настраивает
`DoomTimer.SetTotalTime` и `EnemySpawner` (`maxEnemies`, задержки спавна); для
Shop — `CustomerManager` (`badChance`, размер заказа, скорость клиента).
HP/скорость/урон врагов масштабируются в `EnemySpawner` при спавне.

### ScreenReporter.cs (drop-in в каждой сцене)
В `Start` зовёт `GameSession.SetScreen(screen)` — телефон сам перейдёт на нужную
страницу. Поле `screen` (wait|clues|shop|chat|pause) выставляется в инспекторе под
сцену.

---

## 2. Сцена лавки (Shop)

### CustomerManager.cs (ядро лавки)
Корутина `CustomerLoop`: ждёт загрузку примет (флаг `cluesReady`), затем бесконечно
готовит клиента (`PrepareNewCustomer`: случайный заказ, `currentIsBad` по
`badChance`, спавн визуала), вдвигает его (`SlideObject`), показывает диалог
(`PlayDialog` через `DialogUI`, камера едет к клиенту, контролы выключаются), ждёт
действие игрока, потом выдвигает/закрывает ставней (`MoveShutter`). Оценка:
`OnBottleDelivered` (отдал бутылку плохому → штраф; добил заказ хорошему → награда)
и `PlayerSkips` (скип плохого → награда). Деньги синкаются на бэк через
`GameSession.Earn`; счёт показывается как `$N`. **Приметы**: `FetchClues` тянет
накопленный пул; `SpawnClueForBadCustomer` берёт случайную раскрытую примету —
тип `object` спавнит префаб в свою точку (карта `clueObjectPrefabs`), тип `text`
вшивает фразу в реплику клиента.

### Deliverable.cs
Пустой класс-маркер: «эту вещь можно сдать». Вешается на бутылку.

### AcceptZone.cs (триггер-коллайдер)
`OnTriggerEnter`: если влетел объект с `Deliverable` — зовёт
`CustomerManager.OnBottleDelivered`. `Reset` делает коллайдер триггером.

### ShelfItem.cs
Маркер полки: хранит `itemPrefab` (Rigidbody), который `PlayerInteraction`
спавнит игроку в руки при клике по полке.

### DialogUI.cs
Простая панель реплики: `Show(text)` включает панель и пишет текст, кнопка
«Понял» ставит `acknowledged=true` (это ждёт `CustomerManager`). `Hide` прячет.

### SkipButton.cs
Worldspace-кнопка: `Activate()` → `CustomerManager.PlayerSkips()`. Кликается лучом
из `PlayerInteraction` (не UI).

### ShopTimer.cs
Таймер смены (MM:SS, мигает красным под конец). Сам создаёт Canvas (sortingOrder
−1, без рейкастера). По нулю → `LoadSceneWithFade(nextSceneName)` (обычно
`EquipmentShop`). Есть Pause/Resume/Add/Set/GetRemainingTime.

### SecurityCamerasController.cs
Панель камер видеонаблюдения. `Awake` создаёт RenderTexture на каждую камеру,
вяжет кнопки. `TogglePanel`: открывает панель, освобождает курсор, отключает
скрипты игрока (`SetPlayerScriptsEnabled`), включает нужную камеру; закрытие —
обратно. `Update` форсит курсор разблокированным, пока панель открыта.
`SwitchToCamera` со «снежным» переходом (статик-шум на RawImage).

### TVInteractable.cs / RadioInteractable.cs
`TVInteractable.Activate` → открыть/закрыть камеры. `RadioInteractable.Activate`
переключает радио (AudioSource), частицы и индикатор (вкл/выкл).

### EquipmentPhaseManager.cs (сцена EquipmentShop)
Переходный «уровень закупки». `Start`: `SetScreen("shop")` (покупки на телефоне),
освобождает курсор, создаёт безопасный UI (без рейкастера). Показывает **только
таймер** (формат `СС.сс`, мигает); по нулю/`skipKey` → `LoadSceneWithFade`
(обычно `doom`). Текст/деньги — на твоём изображении сцены, не в скрипте.

---

## 3. Игрок и управление

### PlayerMovement.cs / MouseLook.cs (лавка, FPS-контролы)
`PlayerMovement`: `CharacterController`, WASD + бег (Shift) + прыжок + гравитация.
`MouseLook`: вращение камеры мышью, в `Start` лочит курсор, по Escape освобождает.

### PlayerInteraction.cs (лавка)
Луч из центра камеры по `pickupMask`. ЛКМ: подсветка/подбор объекта (физический
«держатель» в `FixedUpdate` тянет Rigidbody к точке перед камерой), скролл — дистанция,
R — бросок. При попадании по `TV/Radio/SkipButton` зовёт их `Activate()`; по полке
(`ShelfItem`) — спавнит товар в руки. Подсветка через `Outline`.

### DoomPlayerController.cs (doom, FPS-контролы)
Свой контроллер для дума: `CharacterController`, мышь (кламп вертикали),
WASD+бег+прыжок+гравитация. В `Start` лочит курсор.

### PlayerManager.cs (синглтон в doom)
Кэш ссылок на игрока (`PlayerTransform/PlayerHealth/PlayerController`), чтобы враги
и UI не звали медленные `Find*`. Один экземпляр, дубликаты уничтожаются.

---

## 4. Сцена Doom

### DoomHealth.cs (на игроке и врагах)
HP с `OnHealthChanged`/`onDeath` (UnityEvent). `TakeDamage`/`Heal`/`ResetHealth`.
`Die`: игрок → `GameStats.End(Lose)`; враг → `GameStats.AddKill()` и возврат в пул
(`ReturnAfterDelay`) либо `Destroy` через `deathDelay`. Звуки урона/смерти.
`ApplyHealthMultiplier` масштабирует max от базы (безопасно для пула).

### DoomWeapon.cs (оружие игрока)
Хитскан. `Update`: по зажатию Fire1 с паузой `fireRate` зовёт `Shoot`. `Shoot`:
`GameStats.AddShot`, триггер аниматора `Shoot`, muzzleFlash, звук (`PlayOneShot` —
наслаивается, без обрезки очереди), отдача, `Physics.Raycast`. По врагу
(`DoomHealth`) — урон, `GameStats.AddHit`, кровь, **хитмаркер** (`Hitmarker.Hit/Kill`).
Патронов нет (стреляет по нажатию). Статы (урон/скорострельность) ставит `LoadoutManager`.

### DoomEnemy.cs (ИИ через NavMesh)
`NavMeshAgent` + `DoomHealth`. `Update`: в радиусе `detectionRange` преследует
(`SetDestination`), в `attackRange` бьёт по кулдауну (`Attack` → `OverlapSphere`
по `playerLayer` → `TakeDamage`). `ApplyDifficulty` множит скорость/урон от базы.

### DoomEnemySimple.cs (ИИ без NavMesh)
Упрощённый враг: идёт к игроку напрямую через `CharacterController`, поворот
`Slerp`, отходит если слишком близко, бьёт по `playerHealth` (кэш из `PlayerManager`).
Управляет `EnemySpriteAnimator` (Idle/Walk/Attack/Death). Оптимизирован на `sqrMagnitude`.

### EnemySpriteAnimator.cs
Покадровая анимация спрайтов врага (`idle/walk/attack/death`, свой FPS на состояние).
`PlayAnimation(state, playOnce)` переключает набор; `Update` крутит кадры по таймеру;
`playOnce` стопорит на последнем кадре (для смерти).

### EnemySpawner.cs
Спавнит врагов в точках/области через `min..maxSpawnDelay`, держит ≤ `maxEnemies`.
Опционально через `ObjectPool` (создаёт пул в `Start`). `ApplyDifficultyToEnemy`
накладывает множители из `DifficultyManager.Current`. Чистит мёртвых раз в 2с.
Значения `maxEnemies`/задержек перезаписывает `DifficultyManager`.

### ObjectPool.cs
Универсальный пул (Queue свободных + список всех). `Get` достаёт/создаёт и
активирует объект; `Return`/`ReturnAfterDelay` гасит и возвращает. Расширяется до
`maxPoolSize`. Убирает GC-спайки от Instantiate/Destroy.

### DoomTimer.cs
Таймер выживания (MM:SS, мигает). По нулю `OnTimerEnd`: `CompleteLevel` (прогресс
+1 на бэке) → `GameStats.SetLevel`; если уровень ≥ `winLevel` (6) →
`GameStats.End(Win)`; иначе если есть `LevelTransitionChat` — отдаёт ему управление
(видео+чат→shop), иначе сам грузит `cutsceneSceneName`.

### LoadoutManager.cs (doom)
В `Start` тянет `/api/me`. Применяет снаряжение: по `equipped_weapon` ставит
аниматор-контроллер и звук ствола; урон/скорострельность берёт из ответа бэка
(`weapon_damage/fire_rate`, редактируются в админке) с фолбэком на дефолты;
перки — `armor` (+HP), `speed_boots` (×скорость). Запоминает оружие в `GameStats`.

### LevelTransitionChat.cs (переход doom→shop)
`Begin` (зовёт `DoomTimer`): морозит бой (`timeScale=0`), фейд в чёрный (свой
`Fader`, unscaled-время), показывает видео (`VideoPlayer`→RenderTexture, окно
заданного размера, звук через AudioSource), фейд из чёрного. **Первый раз за
забег**: `SetScreen("talk")` + ждёт прохождения чата (гейт) и показывает подсказку;
**далее**: просто крутит видео `videoOnlyDuration` (10с). Затем фейд и
`LoadSceneWithFade(Shop)`. Флаг «диалог был» — в `GameStats.transitionChatShown`.

### Hitmarker.cs (doom)
Синглтон. Строит UI-«✕» из 4 штрихов в центре. `Hit()/Kill()` — поп-анимация
(scale+alpha) + звук (`hitSound`/`killSound`). Зовётся из `DoomWeapon` при
попадании/добивании.

### HitParticles.cs
Кровь при попадании. Статик `CreateBloodSplash(pos, normal)` создаёт временный
объект с `ParticleSystem` (сфера-брызги, гравитация, пиксельная красная текстура),
`Emit`, самоуничтожается.

### DamageScreenEffect.cs (на игроке)
Красная вспышка при уроне. `Start` подписывается на `OnHealthChanged`; при
снижении HP — `ShowDamageFlash` (таймер плавно гасит alpha overlay). Overlay
`raycastTarget=false` (не перехватывает клики).

### DoomFaceUI.cs (лицо игрока)
Меняет спрайт лица по % здоровья (10 состояний), подписан на `OnHealthChanged`.
Если `faceImage` назначен вручную — рисует в нём (можно класть в свой HUD), иначе
создаёт свой.

### GtaHud.cs (HUD в стиле GTA SA)
Драйвер: ты сам раскладываешь Canvas и кидаешь ссылки. Обновляет: полоску HP
(`hpFill` Filled-image `fillAmount` = current/max, либо масштаб по X), время
(из `DoomTimer.GetRemainingTime`, MM:SS) и деньги (один раз из `/api/me`).
Голову даёт отдельный `DoomFaceUI`.

### Billboard.cs
Разворачивает спрайт к камере (как в Doom). `lockY` — только по Y, чтобы не
наклонялся.

### DoomDarkness.cs
Пост-эффект на камере (`OnRenderImage`): затемнение + виньетка через материал
`Hidden/DoomDarkness`.

### AmbientSound.cs
Фоновый эмбиент: создаёт 1–3 AudioSource (основной + ветер + далёкие звуки), 2D,
с fade-in; `FadeOut` плавно глушит.

### FootprintTrail.cs / Footprint.cs / FootstepSounds.cs
`FootprintTrail`: по мере движения через `stepDistance` спавнит префаб следа
(raycast вниз по `snowLayer`, чередует ноги, зеркалит левую), удаляет через время.
`Footprint`: материал следа гаснет по alpha после задержки. `FootstepSounds`:
проигрывает случайный звук шага с разбросом pitch/volume при ходьбе.

### ChatMathManager.cs (doom)
Математика в чате во время боя. Цикл: `ChatNewQuestion` (бэк генерит пример,
телефон показывает) → опрос `FetchChatState`; молчание дольше `answerGrace` →
подсказка «СМОТРИ В ТЕЛЕФОН» + урон `damagePerTick` каждую секунду; неверный
ответ → `wrongAnswerDamage`. Свою UI-подсказку создаёт сам.

---

## 5. Поток игры, переходы, итоги

### SceneTransition.cs (DontDestroyOnLoad)
Плавные переходы. Синглтон создаёт fullscreen fade-Image (sortingOrder 9999, **без
GraphicRaycaster**, `raycastTarget` только на время фейда — чтобы не блокировать
клики после перехода). `LoadSceneWithFade`: FadeOut → `LoadScene` → FadeIn.
Есть `FadeOutOnly/FadeInOnly`.

### PauseMenuManager.cs (DontDestroyOnLoad)
Пауза по Escape: `timeScale=0`, панель, свободный курсор; Resume — обратно.
`OnSceneLoaded` принудительно снимает паузу/время при входе в сцену (чтобы залипшая
пауза не блокировала ввод). Громкость через `AudioMixer`+PlayerPrefs. **«Выйти»** →
`GameStats.End(Quit)` (на сцену итогов). UI строит сам.

### GameStats.cs (DontDestroyOnLoad, синглтон)
Копит статистику забега между сценами: `kills`, `shotsFired`, `hits`, `maxLevel`,
`lastWeapon`, `transitionChatShown`, `outcome`. Статик-хуки (`AddKill/AddShot/AddHit/
SetWeapon/SetLevel`) зовут другие скрипты. `End(outcome)`: размораживает время,
`SetScreen("over_*")` (телефону), грузит `resultsScene`. `ResetRun` — сброс при
«начать заново».

### GameOverManager.cs (сцена Results)
Драйвер: ты делаешь Canvas (заголовок, статы, 2 кнопки) и кидаешь ссылки. Пишет
WIN/LOSE/QUIT (+цвет) и статистику (+деньги из `/api/me`). **Restart** → `ClearToken`
+ `ResetRun` + грузит `Pairing`. **Quit** → `ClearToken` + `Application.Quit`.

### FeedbackFX_Version2.cs (класс `FeedbackFX`)
Универсальный фидбэк: `Correct()/Wrong()` = вспышка экрана (overlay),
тряска камеры (Perlin, unscaled-время) и звук. Использует лавка для реакции на
верное/ошибочное обслуживание.

### CRTScreenFlicker.cs
Эффект старого монитора: мерцание яркости (Perlin) + редкие резкие моргания +
дрожание. Управляет любой целью (Graphic/CanvasGroup/Renderer-материал/Light/
Transform). На выключении возвращает исходные значения.

---

## 6. Editor

### Editor/TokenTools.cs (`#if UNITY_EDITOR`)
Меню `Tools/Pairing/Clear Game Token` и `Show Game Token` — стирают/показывают
game-токен через PlayerPrefs API (правка файла prefs при открытом редакторе не
работает). Для отладки пейринга.

---

## 7. Бэкенд (FastAPI, `temp/backend/app/`)

### settings.py
Конфиг из env: `JWT_SECRET`, `ADMIN_KEY`, TTL токенов, `PAIRING_TTL_SEC`,
`SITE_BASE_URL` (куда ведёт QR), `DATABASE_URL`, `STARTING_MONEY`, `MAX_LEVEL`.

### database.py
SQLAlchemy engine (SQLite), `SessionLocal`, `get_db()` (зависимость FastAPI), `Base`.

### models.py
ORM-таблицы: `User`, `PlayerState` (деньги, уровни, экран, `equipped_weapon`,
`revealed_clues`, поля чата `chat_q_*`, `intro_done`), `OwnedItem`,
`PairingSession`, `AppConfig` (key→JSON для оверрайдов админки).

### security.py
JWT (jose) + bcrypt. `create_token(user_id, scope, ttl)` (scope `site`/`game`),
`get_current_user/get_site_user/get_game_user` — зависимости, проверяющие токен и
scope.

### gamedata.py
Статика: `ECONOMY`, каталог `ITEMS` (оружие/перки + `stats`), `MAX_LEVEL`,
`level_config(level)` (формула сложности), `CLUES`/`CLUES_BY_ID` (пул примет),
`make_math_question()` (генератор арифметики).

### config_store.py
Оверрайды из админки поверх `gamedata` (хранятся в `AppConfig`, deep-merge):
экономика, `level_overrides` (`merged_level_config/all_levels`), `math_questions`,
`weapons` (эффективные урон/скорострельность), `dialogues` (intro/doom_to_shop с
дефолтами). Геттеры/сеттеры.

### schemas.py
Pydantic-модели запросов/ответов (auth, pairing, `PlayerStateResponse` с
`weapon_damage/fire_rate`, chat, и т.д.).

### main.py
Создаёт `FastAPI`, CORS, `create_all`, **`_auto_migrate()`** (ALTER TABLE ADD COLUMN
для недостающих колонок — не нужен `down -v` при новых полях), подключает роутеры
`auth/pairing/player/admin`. `/health`.

### routers/auth.py
`/api/auth/register` (создаёт User+PlayerState, выдаёт site-токен),
`/api/auth/login` (проверка пароля → site-токен).

### routers/pairing.py
`/api/pair/start` (игра: code+QR PNG), `/status` (игра опрашивает; на `linked`
один раз выдаёт game-токен и метит `consumed`), `/quick-confirm` (скан без логина:
создаёт анонимного игрока, линкует, отдаёт site-токен), `/confirm` (подтверждение
с логином).

### routers/player.py
Игровое API: `/api/me`, `/state/screen` (get/set), `/economy/earn`, `/shop/items`,
`/loadout/weapon` (выбор активного ствола), `/shop/buy`, `/progress/complete-level`,
`/levels(/{n})` (merged-сложность), `/clues` (накопительные приметы,
`_ensure_revealed` сам выкидывает устаревшие id), чат-математика
(`/chat/question|state|answer`), диалоги `/dialogue/{key}`, гейт
`/gate/reset|done|status` (синхронизация игра↔телефон на переходах).

### routers/admin.py
Защита заголовком `X-Admin-Key`. `GET/PUT /api/admin/config` (экономика,
level_overrides, math_questions, dialogues, weapons), управление игроками
(`/players`, деньги, уровень, предметы, оружие, reset).

---

## 8. Фронтенд (телефон-компаньон, `temp/frontend/`)

- **app.js** — общий клиент: `api()` (fetch + Bearer), токен сайта в localStorage,
  объекты `Auth/Pairing/State/Game/Clue/Chat/Dialogue`, `SCREEN_PAGE` (карта
  экран→страница, вкл. `talk`, `over_*`), `goToCurrentScreen`/`startScreenWatcher`
  (страницы сами следуют за игрой, опрашивая `/api/state/screen`).
- **config.js** — `window.API_BASE` (пусто = тот же origin через nginx-прокси).
- **index.html** — лендинг: если есть токен — следуем за игрой, иначе «сканируй QR».
- **pair.html** — подключение по `?code=` (quick-confirm) → после линка `story.html`.
- **story.html** — универсальный диалог по `?key=` (intro/doom_to_shop): тянет текст
  из `/api/dialogue`, вставляет матвопросы (маркер `MATH`), в конце `/api/gate/done`.
- **chat.html** — математика во время боя (опрос `/api/chat/question`, ответ).
- **clues.html** — накопленные приметы (эмодзи+название+описание).
- **shop.html** — магазин снаряжения (покупка/экипировка через `/api/shop|loadout`).
- **wait.html** — загрузка/пауза.
- **over.html** — экран исхода (ПОБЕДА/ПОРАЖЕНИЕ/СМЕНА ПРЕРВАНА), стирает токен и
  возвращает на главную.
- **admin.html** — админка (ключ → правка игроков, экономики, сложности, матвопросов,
  диалогов, оружия).
- **styles.css** — ретро ЧБ MS-DOS тема (VT323, сканлайны, инверсия, ASCII-баннеры).
