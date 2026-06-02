# Cashier & Doom — полный справочник по всем скриптам

Подробный разбор **каждого** скрипта проекта (50 файлов): назначение, поля
инспектора, методы жизненного цикла, внутренняя логика, зависимости и обращения к
серверу. Сгруппировано по областям.

Обозначения: **MB** = MonoBehaviour (вешается на объект), **static** = статический
класс (не компонент), **Editor** = только в редакторе.

Связанные доки: `Docs/API_INTEGRATION.md` (сервер), `Docs/SHOP_LEVEL.md` (лавка).

---

# A. Сеть / бэкенд-клиент (`scripts/Backend/`)

## BackendConfig.cs — static
Один публичный `const string BaseUrl` — базовый URL бэкенда. От него строятся все
запросы. Меняешь адрес сервера здесь и нигде больше.

## BackendDTO.cs — data
`[Serializable]`-классы для `JsonUtility` (имена полей = JSON бэка):
- `EarnReq{amount}`, `BuyReq{item_id}`, `ScreenReq{screen}`, `EquipWeaponReq?` — тела запросов.
- `MoneyResp{money}`.
- `MeResp{username, money, current_level, highest_level, items[], equipped_weapon, weapon_damage, weapon_fire_rate}`.
- `CompleteLevelResp{current_level, highest_level, finished_game, next_level_config}`.
- `ShooterCfg`, `ShopCfg`, `LevelConfig{level, shooter, shop}` — конфиг сложности.
- `ClueDTO{id, emoji, name, text, type, phrase}`, `CluesResp{level, clues[]}`.
- `ChatQuestionResp{id, text, options[], status}`, `ChatStateResp{id, status}`.
- `IntroStatusResp{done}` — гейт диалога.
- `PairStartResp{code, qr_url, qr_png_base64, poll_secret, expires_in}`, `PairStatusResp{status, game_token, username}`.
Логики нет — только структуры под десериализацию.

## GameApi.cs — MB (синглтон, DontDestroyOnLoad)
Единственный, кто шлёт HTTP. Остальные ходят через него.
- `static Instance`, `static Ensure()` (создаёт объект, если нет).
- Токен: `const TOKEN_KEY="cd_game_token"`, `static Token` (get/set PlayerPrefs),
  `HasToken`, `ClearToken()`.
- `Get(path, cb, auth=true)` / `Post(path, json, cb, auth=true)` → корутина `Send`.
- `Send`: строит `UnityWebRequest`, ставит `Content-Type: application/json`, при
  `auth&&HasToken` — `Authorization: Bearer <token>`, шлёт, в колбэк `(ok, body)`;
  при ошибке — `Debug.LogWarning`.
- `DontDestroyOnLoad` → запрос переживает смену сцены.

## GameSession.cs — static (фасад)
Обёртки над эндпоинтами через `GameApi`:
- `Earn(amount, onMoney)` → POST `/api/economy/earn`.
- `Buy(itemId, onResult)` → POST `/api/shop/buy`.
- `CompleteLevel(onDone)` → POST `/api/progress/complete-level` (отдаёт `finished_game`).
- `SetScreen(screen)` → POST `/api/state/screen`.
- `FetchMe(onMe)` → GET `/api/me`.
- `FetchClues(onClues)` → GET `/api/clues`.
- `ChatNewQuestion(onQ)` → POST `/api/chat/question`.
- `FetchChatState(onState)` → GET `/api/chat/state`.
Каждый метод сам зовёт `GameApi.Ensure()`, парсит JSON в DTO и отдаёт в колбэк.

## DifficultyManager.cs — MB
Тянет и применяет сложность текущего уровня.
- Поля: `Mode mode` (Shooter|Shop), `bool autoApply`; статик `LevelConfig Current`.
- `Start` → `FetchAndApply`: GET `/api/me` (узнать `current_level`) → GET
  `/api/levels/{lvl}` → `Current` + `GameStats.SetLevel(level)` → `Apply()`.
- `Apply()`/`ApplyShooter`: `DoomTimer.SetTotalTime`, `EnemySpawner`
  (`maxEnemies`/`minSpawnDelay`/`maxSpawnDelay`). `ApplyShop`: `CustomerManager`
  (`badChance`/`minOrder`/`maxOrder`/`customerMoveDuration`).
- HP/скорость/урон врагов масштабируются позже в `EnemySpawner`.

## PairingManager.cs — MB (сцена Pairing)
QR-пейринг + ожидание интро.
- Поля: `qrImage/codeText/statusText`, `introSceneName` (стартовая сцена),
  `pollInterval`, `skipIfAlreadyPaired`, `waitForPhoneIntro`, `introMaxWait`.
- `Start`: `Ensure`; если есть токен и `skipIfAlreadyPaired` → `GoIntro()`; иначе `StartPairing`.
- `StartPairing`: POST `/api/pair/start` → code+QR; рисует QR; `PollLoop`.
- `PollLoop`: GET `/api/pair/status` каждые `pollInterval`, пока не `linked`+`game_token`
  (сохраняет `GameApi.Token`); на `expired` — рестарт.
- `WaitIntroThenLoad`: POST `/api/gate/reset`, опрос GET `/api/gate/status` пока
  `done` или таймаут → `GoIntro` (грузит сцену). Игра ждёт окончания диалога на телефоне.

## ScreenReporter.cs — MB (drop-in)
`Start` → `GameSession.SetScreen(screen)`. Поле `screen` (wait|clues|shop|chat|pause)
ставится в инспекторе под сцену — телефон сам перейдёт на нужную страницу.

---

# B. Сцена лавки (Shop)

## CustomerManager.cs — MB (ядро лавки)
Большой класс. Поля (инспектор): точки клиента (spawn/entry/exit), `customerVisualPrefabs`,
`badChance`, карта `clueObjectPrefabs[{clueId, prefab, spawnPoint}]`, заказ
(`minOrder/maxOrder`), очки (`rewardCorrectServe/penaltyServeBad/rewardSkipBad/penaltySkipGood`),
ставня (`shutter`+точки), тайминги, `dialogText/scoreText`, `dialogUI`, портретная
камера+цель, камера игрока, `scriptsToDisableDuringDialog`, `feedbackFX`, `dialogTemplates`.
Состояние: `score`, `currentOrder/remaining`, `currentIsBad`, `currentClue`,
`currentCluePhrase`, `revealedClues`, `cluesReady`, `customerActive`, `transitioning`.
- `Start`: `FetchClues` (GET `/api/clues`), `FetchMe` (GET `/api/me` → стартовый
  `score`), инициализация UI/ставни, запуск `CustomerLoop`.
- `CustomerLoop`: ждёт `cluesReady` (таймаут 3с) → бесконечно: `PrepareNewCustomer`
  → слайд → `PlayDialog` → ждёт действия → ставня/выход → след. клиент.
- `PrepareNewCustomer`: заказ, бросок `currentIsBad`, спавн визуала, если плохой —
  `SpawnClueForBadCustomer`.
- `SpawnClueForBadCustomer`: случайная раскрытая примета; `object` → `FindClueMap` →
  спавн префаба в свою точку; `text` → `currentCluePhrase`.
- `PlayDialog`: двигает камеру к клиенту (`MoveCameraTo`), `SetControlsEnabled(false)`,
  собирает реплику (+`currentCluePhrase`), `dialogUI.Show`, ждёт `acknowledged`, вернуть камеру.
- `OnBottleDelivered`: плохому → штраф; добил хорошему → награда. `PlayerSkips`:
  плохого → награда, хорошего → штраф. Оба → `ChangeScore` + `FeedbackFX`.
- `ChangeScore(delta)`: `score+=delta`; `GameSession.Earn(delta, money=>{score=money;…})`
  (POST `/api/economy/earn`, авторитетная сумма). `UpdateScoreUI` → `"$"+score`.

## Deliverable.cs — MB
Пустой маркер «эту вещь можно сдать» (на бутылке). Логики нет.

## AcceptZone.cs — MB ([RequireComponent(Collider)])
Триггер у окна. `Reset` делает коллайдер триггером. `OnTriggerEnter`: если влетел
`Deliverable` → `CustomerManager.OnBottleDelivered`.

## ShelfItem.cs — MB
Маркер полки: поле `Rigidbody itemPrefab` — что спавнить в руки при клике.

## DialogUI.cs — MB
Панель реплики. Поля: `panel`, `textLabel`, `acknowledgeButton`, `acknowledged`.
`Show(text)` включает панель/пишет текст/`acknowledged=false`; `Hide`; кнопка →
`OnAcknowledge` ставит `acknowledged=true`.

## SkipButton.cs — MB
Worldspace-кнопка. `Awake` находит `CustomerManager`. `Activate()` → `PlayerSkips()`.
Нажимается лучом `PlayerInteraction`.

## ShopTimer.cs — MB
Таймер смены. Поля: `totalTime`, `nextSceneName` (EquipmentShop), `useFadeTransition`,
`timerText`+позиция/цвета/`warningTime`. Сам создаёт Canvas (sortingOrder −1, без
рейкастера). `Update` тикает; по нулю `OnTimerEnd` → `SceneTransition.LoadSceneWithFade`.
Публичные Pause/Resume/Add/Set/Get/IsRunning.

## SecurityCamerasController.cs — MB
Панель видеонаблюдения. Поля: `panel`, `cameraDisplay`, `staticOverlay`,
`cameraButtons[]`, `securityCameras[]`, ссылки на скрипты игрока, тайминги шума.
`Awake`: RenderTexture на каждую камеру, привязка кнопок (на null — `LogError`).
`Update`: при `panelOpen` держит курсор свободным; крутит шум. `TogglePanel`:
открыть/закрыть, свободный курсор, `SetPlayerScriptsEnabled(false/true)`, включает
выбранную камеру. Статик `activePanelController` + `TryCloseActivePanel` (Escape из
паузы). `SwitchToCamera`/`SwitchRoutine`: «снежный» переход (alpha шума).

## TVInteractable.cs — MB
`Awake` находит `SecurityCamerasController` (на null — `LogError`). `Activate()` →
`TogglePanel()`.

## RadioInteractable.cs — MB
Поля: `radioSource`, `particles`, `indicatorObject`, `isOn`. `Activate()` инвертирует
`isOn` и `ApplyState`: вкл/выкл звук, частицы, индикатор.

---

# C. Игрок и управление

## PlayerMovement.cs — MB ([RequireComponent(CharacterController)])
Контролы лавки. Поля: `walkSpeed/runSpeed/jumpHeight/gravity`. `Update`: WASD+бег
(Shift)+прыжок (Space)+гравитация через `controller.Move`.

## MouseLook.cs — MB
Поля: `playerBody`, `mouseSensitivity`. `Start` лочит курсор. `Update`: обзор мышью
(кламп вертикали ±85°); по Escape освобождает курсор.

## PlayerInteraction.cs — MB (лавка)
Поля: `playerCamera`, `holdPoint`, `pickupRange`, `pickupMask`, `holdForce/holdDamping/
maxHoldDistance/throwForce`, параметры скролла, подсветка (`Outline`).
`Update`: ЛКМ → `TryPickup`/`DropObject`; скролл — дистанция; R — бросок; `UpdateHoverHighlight`.
`FixedUpdate`: «держит» Rigidbody перед камерой. `TryPickup` (луч по `pickupMask`):
ТВ/радио/скип → `Activate()`; полка (`ShelfItem`) → спавн товара; иначе подбор Rigidbody.

## DoomPlayerController.cs — MB ([RequireComponent(CharacterController)])
Контролы doom. Поля: движение (`walkSpeed/runSpeed/jumpForce/gravity`), мышь
(`playerCamera/mouseSensitivity/maxLookAngle`). `Start` лочит курсор. `Update`:
`HandleMouseLook` + `HandleMovement`.

## PlayerManager.cs — MB (синглтон в doom)
Кэш ссылок игрока: `PlayerTransform/PlayerHealth/PlayerController`. `Awake` — синглтон
(дубликаты уничтожает). Используется врагами/UI вместо медленных `Find*`.

---

# D. Сцена Doom

## DoomHealth.cs — MB (игрок и враги)
Поля: `maxHealth/currentHealth`, звуки урона/смерти, события `onDeath`/`OnHealthChanged`,
`isPlayer`, `gameOverScene`, пул (`usePooling`, `deathDelay`).
- `TakeDamage`: −урон, событие, звук, при ≤0 → `Die`.
- `Heal`, `ResetHealth`, `SetPool`, `ApplyHealthMultiplier(mult)` (масштаб от базы).
- `Die`: игрок → `GameStats.End(Lose)`; враг → `GameStats.AddKill()` + возврат в пул
  (`ReturnAfterDelay`) или `Destroy` через `deathDelay`.
- Геттеры: `GetCurrentHealth/GetMaxHealth/IsDead`.

## DoomWeapon.cs — MB (оружие игрока)
Поля: `damage`, `fireRate` (пауза между выстрелами), `range`, эффекты
(`muzzleFlash/impactEffect/shootSound/shootableLayers`), отдача, `playerCamera/
weaponModel/weaponAnimator`. Патронов нет.
- `Start`: находит камеру/аниматор, запоминает позицию модели.
- `Update`: зажат Fire1 и прошёл `fireRate` → `Shoot`; плавный возврат отдачи.
- `Shoot`: `GameStats.AddShot`; триггер `Shoot` аниматора; muzzle; звук
  `shootSound.PlayOneShot(clip)` (наслаивается); отдача; `Physics.Raycast`. По врагу:
  урон, `GameStats.AddHit`, кровь (`HitParticles.CreateBloodSplash`), хитмаркер
  (`Hitmarker.Hit/Kill`); эффект попадания.

## DoomEnemy.cs — MB ([RequireComponent(NavMeshAgent)])
ИИ на NavMesh. Поля: `detectionRange/attackRange/attackCooldown`, `attackDamage/
playerLayer`, `moveSpeed/chaseSpeed`, ссылки (`player/attackSound/animator`).
- `Start`: агент, здоровье; находит игрока (PlayerManager → тег); кэширует
  `_playerHealth` (бьёт по нему напрямую, без зависимости от Player Layer).
- `Update`: мёртв → стоп. В `detectionRange` — преследование (`SetDestination`),
  скорость `chaseSpeed`; в `attackRange` — `Attack` по кулдауну; вне — стоп. Анимация
  через `Animator` (`IsMoving/IsAttacking`).
- `Attack`: звук + `_playerHealth.TakeDamage`. `ApplyDifficulty(speedMult, dmgMult)`
  масштаб от базы. Gizmos радиусов.

## NavMeshLinkJump.cs — MB ([RequireComponent(NavMeshAgent)])
Плавный прыжок по NavMesh Link/Off-Mesh Link вместо «телепорта». Поля: `jumpHeight`,
`duration`. `Start` выключает `autoTraverseOffMeshLink`. `Update`: при
`isOnOffMeshLink` → `Traverse`. `Traverse`: `updatePosition=false`, дуга
(`Sin`*`jumpHeight`) по `transform.position` за `duration`, в конце `Warp(end)` +
`CompleteOffMeshLink`.

## EnemySpawner.cs — MB
Спавн врагов. Поля: `enemyPrefab`, `maxEnemies`, `min/maxSpawnDelay`, пул
(`useObjectPooling/poolSize`), точки/область спавна, `minDistanceFromPlayer`.
- `Start`: находит игрока, создаёт `ObjectPool` (если включён), планирует спавн.
- `Update`: чистит мёртвых (раз в 2с), при таймере и `< maxEnemies` → `SpawnEnemy`.
- `SpawnEnemy`: позиция (точка/область, не ближе `minDistanceFromPlayer`), берёт из
  пула/`Instantiate`, `ResetHealth`+`SetPool`, `ApplyDifficultyToEnemy`.
- `ApplyDifficultyToEnemy`: из `DifficultyManager.Current` — HP-множитель + `DoomEnemy.ApplyDifficulty`.
- Публичные Stop/Resume/Clear/GetEnemyCount. Gizmos области/точек.

## ObjectPool.cs — MB
Пул объектов (Queue свободных + список всех). `Initialize` предсоздаёт; `Get(pos,rot)`
достаёт/расширяет/активирует; `Return`/`ReturnAfterDelay` гасит и возвращает; `Clear`.
Убирает GC-спайки от Instantiate/Destroy.

## DoomTimer.cs — MB
Таймер выживания. Поля: `totalTime`, `cutsceneSceneName`, `winLevel` (=6),
`useFadeTransition`, `timerText`+цвета. `Update` тикает; `OnTimerEnd`: POST
`/api/progress/complete-level`; `GameStats.SetLevel(played)`; если `played≥winLevel` →
`GameStats.End(Win)`; иначе если есть `LevelTransitionChat` → `Begin()`; иначе грузит
`cutsceneSceneName`. `SetTotalTime` ставит `DifficultyManager`.

## LoadoutManager.cs — MB (doom)
Применяет снаряжение. Поля: рефы (`weapon/playerHealth/playerController`), контроллеры
аниматора стволов (`pistol/shotgun/smgController`), звуки (`*Sfx`), перки
(`armorHealthBonus/speedBootsMult`).
- `Start`: `AutoWire` (находит оружие/аниматор/здоровье игрока), `FetchMe` (GET
  `/api/me`) → `equipped`, `items`, `weapon_damage/fire_rate`.
- `ApplyLoadout(equipped, owned, dmg, fr)`: по стволу — контроллер/звук + дефолтные
  статы; урон/скорострельность из бэка (перекрывают дефолт); `GameStats.SetWeapon`;
  перки `armor`(+HP)/`speed_boots`(×скорость).
- `EquipWeapon`: ставит `weapon.damage/fireRate`, `shootSound.clip`, подменяет
  `weaponAnimator.runtimeAnimatorController`.

## ChatMathManager.cs — MB (doom)
Математика в чате. Поля: `playerHealth`, тайминги (`firstDelay/askInterval/
answerGrace/tickInterval/pollInterval`), штрафы (`wrongAnswerDamage/damagePerTick`),
подсказка (`hintText/hintMessage/hintColor`). `Start` находит игрока, создаёт UI
подсказки, запускает `Loop`. `AskAndWait`: POST `/api/chat/question` → опрос GET
`/api/chat/state`; молчание > `answerGrace` → подсказка + урон каждый тик; `wrong` →
`wrongAnswerDamage`.

## Hitmarker.cs — MB (синглтон)
«✕» по центру при попадании. Поля: звуки (`hit/killSound`), `volume`, цвета, размеры
(`tickLength/gap/thickness`), анимация (`duration/popScale`). `Awake` строит UI из 4
штрихов + AudioSource. `Show(kill)`: цвет + звук + поп-анимация (в `Update`). Статик
`Hit()/Kill()` зовёт `DoomWeapon`.

## HitParticles.cs — MB
Кровь при попадании. Создаёт `ParticleSystem` (сфера-брызги, гравитация, пиксельная
красная текстура). `PlayHitEffect(point, normal)` — `Emit`. Статик
`CreateBloodSplash(pos, normal)` — временный объект, играет и самоуничтожается.

## DamageScreenEffect.cs — MB (на игроке)
Красная вспышка при уроне. Поля: `damageImage`, `damageColor`, `flashDuration`.
`Start` создаёт overlay (`raycastTarget=false`) и подписывается на `OnHealthChanged`;
при снижении HP — `ShowDamageFlash` (таймер гасит alpha).

## DoomFaceUI.cs — MB
Лицо игрока по % HP (10 спрайтов). Поля: `face100..face10`, `faceImage`+позиция/размер,
`playerHealth`. Подписан на `OnHealthChanged` → `UpdateFace`. Если `faceImage` назначен
вручную — рисует в нём (можно класть в свой HUD), иначе создаёт свой.

## GtaHud.cs — MB (HUD как в GTA SA)
Драйвер: ты сам делаешь UI, кидаешь ссылки. Поля: `playerHealth/timer`, `hpFill`
(Image Filled), `timeText`, `moneyText`, `moneyPrefix`. `Start` находит health/timer,
`FetchMe` (GET `/api/me`) → деньги. `Update`: HP (`fillAmount`=current/max или scale),
время (`DoomTimer.GetRemainingTime` → MM:SS).

## AmbientSound.cs — MB
Фоновый эмбиент. Поля: `ambientClip`+громкость/`loop`, fade-in, доп-слои (`windSound/
distantSounds`). `Start` создаёт 1–3 AudioSource (2D), играет с fade-in. `FadeOut(dur)`
плавно глушит.

## DoomDarkness.cs — MB (пост-эффект)
`OnRenderImage`: затемнение + виньетка через материал `Hidden/DoomDarkness`. Поля:
`darknessAmount`, `darknessColor`, `useVignette`, `vignetteIntensity`.

## FootprintTrail.cs / Footprint.cs / FootstepSounds.cs — MB
- `FootprintTrail`: по `stepDistance` спавнит префаб следа (raycast вниз по `snowLayer`,
  чередует ноги, зеркалит левую), удаляет через `footprintLifetime`.
- `Footprint`: материал следа гаснет по alpha после `fadeDelay` за `fadeDuration`.
- `FootstepSounds`: при ходьбе по `stepDistance` играет случайный звук шага с разбросом
  pitch/volume.

---

# E. Поток игры, переходы, итоги, FX

## SceneTransition.cs — MB (синглтон, DontDestroyOnLoad)
Плавные переходы. `Awake` строит fade-Canvas (sortingOrder 9999, **без
GraphicRaycaster**) и fade-Image (`raycastTarget=false`, включается только на время
фейда). `LoadSceneWithFade(name/index)`: `FadeOut` → `LoadScene` → `FadeIn`. Есть
`FadeOutOnly/FadeInOnly`.

## PauseMenuManager.cs — MB (синглтон, DontDestroyOnLoad)
Пауза по Escape: `timeScale=0`, панель, свободный курсор; `ResumeGame` — обратно.
`OnSceneLoaded` сбрасывает залипшую паузу/время при входе в сцену. Громкость через
`AudioMixer`+PlayerPrefs. **«Выйти»** → `GameStats.End(Quit)` (на сцену итогов). UI
(панель/кнопки/слайдер) строит сам.

## GameStats.cs — MB (синглтон, DontDestroyOnLoad)
Статистика забега + триггер конца игры. Поля: `kills/shotsFired/hits/maxLevel/
lastWeapon/transitionChatShown/outcome`, `resultsScene`. Статик-хуки
`AddKill/AddShot/AddHit/SetWeapon/SetLevel`, `ResetRun`. `End(outcome)`: `timeScale=1`,
POST `/api/state/screen=over_*` (телефону), грузит сцену итогов.

## GameOverManager.cs — MB (сцена Results)
Драйвер: ты делаешь Canvas (заголовок/статы/2 кнопки), кидаешь ссылки. Поля:
`pairingScene`, `titleText/statsText/restartButton/quitButton`, тексты/цвета исхода.
`Start`: пишет WIN/LOSE/QUIT + статы; `FetchMe` (GET `/api/me`) → деньги; вешает
действия. **Restart**: `ClearToken`+`ResetRun`→Pairing. **Quit**: `ClearToken`+`Application.Quit`.

## EquipmentPhaseManager.cs — MB (сцена EquipmentShop)
Переходная закупка. Поля: `buyDuration`, `nextSceneName` (doom), `phoneScreen` (shop),
`timerText`+цвета, `skipKey`. `Start`: `Ensure`, `SetScreen("shop")`, освобождает
курсор, создаёт безопасный UI (без рейкастера), таймер. `Update` тикает (формат
`СС.сс`); по нулю/`skipKey` → `GoToRaid` (`LoadSceneWithFade`).

## LevelTransitionChat.cs — MB (переход doom→shop)
Поля: `nextSceneName`, `videoClip`+`loopVideo`/`videoSize`/`videoOffset`/`playVideoSound`/
`videoVolume`, `hintMessage`, `fadeDuration/pollInterval/maxWait/videoOnlyDuration`.
`Begin` (зовёт `DoomTimer`): `timeScale=0`, фейд в чёрный (свой `Fader`, unscaled),
видео (RenderTexture, окно заданного размера, звук через AudioSource), фейд из чёрного.
**Первый раз за забег** (флаг `GameStats.transitionChatShown`): POST `/api/gate/reset`
+ `SetScreen("talk")`, ждёт `/api/gate/status.done`; **далее** — просто видео
`videoOnlyDuration` сек. Затем фейд и `LoadSceneWithFade(Shop)`.

## FeedbackFX_Version2.cs (класс `FeedbackFX`) — MB
Фидбэк лавки. Поля: overlay+цвета/тайминги вспышки, камера+параметры тряски, звуки.
`Correct()/Wrong()`: вспышка (`FlashRoutine`, unscaled), тряска камеры (`ShakeRoutine`,
Perlin), звук. Зовётся `CustomerManager` при верном/ошибочном действии.

## CRTScreenFlicker.cs — MB
Эффект старого монитора. Поля: цели (`targetGraphic/canvasGroup/screenRenderer/
screenLight/jitterTarget`), мерцание (`flickerAmount/Speed`), резкие моргания
(`blinkMin/Max/Darkness/Duration`), дрожание (`jitter/Amount/Speed`). `Start`
запоминает исходные значения. `Update`: яркость = Perlin + редкие моргания, плюс
дрожание позиции. `OnDisable` возвращает исходное.

---

# F. Editor

## Editor/TokenTools.cs — Editor (`#if UNITY_EDITOR`)
Меню `Tools/Pairing/Clear Game Token` и `Show Game Token` — стирают/печатают game-токен
через PlayerPrefs API. Для отладки пейринга (правка файла prefs при открытом редакторе
не работает).

---

# Сводка обращений к серверу

Только эти скрипты ходят на бэкенд (через `GameApi`/`GameSession`):
`GameApi`, `GameSession`, `PairingManager`, `DifficultyManager`, `ScreenReporter`,
`CustomerManager`, `LoadoutManager`, `DoomTimer`, `ChatMathManager`,
`EquipmentPhaseManager`, `LevelTransitionChat`, `GameStats`, `GameOverManager`.
Детали запросов — в `Docs/API_INTEGRATION.md`.

Остальные (`DoomWeapon`, `DoomEnemy`, `EnemySpawner`, `ObjectPool`, HUD/эффекты,
контролы, лавка-механика и т.п.) работают локально, без сети.
