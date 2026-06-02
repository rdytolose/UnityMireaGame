# Уровень Shop (ночная лавка) — как всё устроено

Папир-плиз-механика: к окошку подходят клиенты, ты их обслуживаешь (выдаёшь бутылки)
или прогоняешь (skip). Часть клиентов «плохие» — их выдаёт **примета** (предмет в
лавке или фраза в реплике). Правильно обслужил/прогнал → деньги; ошибся → штраф.
По таймеру смена заканчивается и идёт переход в EquipmentShop (закупка).

Ниже — каждый скрипт сцены Shop, что делает и как связан с остальными.

---

## 1. Ядро — `CustomerManager.cs`

Двигатель уровня. Держит счёт, рулит очередью клиентов, диалогом, ставней, приметами,
синком денег.

### Цикл `CustomerLoop()` (корутина)
1. Ждёт загрузку примет (флаг `cluesReady`) — чтобы первый «плохой» клиент не вышел
   без улики.
2. `PrepareNewCustomer()`: случайный размер заказа (`minOrder..maxOrder`), бросок
   «плохой ли» (`badChance`), спавн визуала клиента; если плохой — `SpawnClueForBadCustomer()`.
3. `SlideObject(...)` — вдвигает клиента к окну.
4. `PlayDialog()` — камера едет к клиенту, выключаются контролы, `DialogUI.Show(текст)`;
   ждёт нажатия «Понял» (`dialogUI.acknowledged`).
5. `customerActive = true` → ждёт действия игрока (выдал бутылки или скип).
6. Закрывает ставней (`MoveShutter`), выдвигает клиента, спавнит следующего.

### Оценка
- `OnBottleDelivered(d)`: если клиент **плохой** → штраф (`penaltyServeBad`); если
  добил заказ хорошему → награда (`rewardCorrectServe`).
- `PlayerSkips()`: прогнал плохого → награда (`rewardSkipBad`); прогнал хорошего →
  штраф (`penaltySkipGood`).
- `ChangeScore(delta)`: меняет локальный счёт и шлёт на сервер (см. деньги ниже),
  плюс дёргает `FeedbackFX`.

### Приметы (связь с сервером)
- В `Start`: `GameSession.FetchClues` → **GET `/api/clues`** — накопленный пул примет
  игрока (растёт по дням, запоминается).
- `SpawnClueForBadCustomer()`: берёт случайную раскрытую примету:
  - тип **`object`** → находит префаб по карте `clueObjectPrefabs` (`clueId → prefab +
    точка`) и спавнит **строго в свою точку**;
  - тип **`text`** → кладёт `currentCluePhrase`, которая в `PlayDialog` дописывается в
    реплику клиента.

### Деньги (связь с сервером)
- В `Start`: `GameSession.FetchMe` → **GET `/api/me`** → ставит счёт = реальный баланс
  игрока (а не 0).
- В `ChangeScore`: `GameSession.Earn(delta, money => …)` → **POST `/api/economy/earn`**
  — начисляет/списывает; колбэк возвращает авторитетную сумму, ей правится табло.
- Табло показывает `$N`.

---

## 2. Взаимодействие игрока

### `PlayerMovement.cs` + `MouseLook.cs`
FPS-контролы лавки: WASD+бег+прыжок (`CharacterController`) и обзор мышью. `MouseLook`
лочит курсор в `Start`, освобождает по Escape.

### `PlayerInteraction.cs`
Луч из центра камеры по `pickupMask` (ЛКМ):
- по **полке** (`ShelfItem`) → спавнит её `itemPrefab` (бутылку) в руки;
- по **ТВ** (`TVInteractable`) / **радио** (`RadioInteractable`) / **кнопке скип**
  (`SkipButton`) → зовёт их `Activate()`;
- иначе подбирает Rigidbody; в `FixedUpdate` «держит» его перед камерой; скролл — дистанция,
  R — бросок; подсветка наведённого через `Outline`.

### `ShelfItem.cs` / `Deliverable.cs` / `AcceptZone.cs`
- `ShelfItem` — маркер полки с префабом товара.
- `Deliverable` — пустой маркер «эту вещь можно сдать» (на бутылке).
- `AcceptZone` — триггер-коллайдер у окна: в `OnTriggerEnter`, если влетел
  `Deliverable`, зовёт `CustomerManager.OnBottleDelivered`.

### `SkipButton.cs`
Worldspace-кнопка (не UI): `Activate()` → `CustomerManager.PlayerSkips()`. Нажимается
лучом из `PlayerInteraction`.

### `DialogUI.cs`
Панель реплики клиента: `Show(text)` включает панель и пишет текст; кнопка «Понял»
ставит `acknowledged=true` (это ждёт `CustomerManager.PlayDialog`).

---

## 3. Обратная связь — `FeedbackFX_Version2.cs` (класс `FeedbackFX`)

`Correct()` / `Wrong()` — вспышка экрана (overlay), тряска камеры (Perlin, на
unscaled-времени) и звук. `CustomerManager` зовёт их при верном/ошибочном действии,
чтобы игрок сразу чувствовал результат.

---

## 4. Камеры наблюдения

### `SecurityCamerasController.cs`
Панель видеонаблюдения. `Awake`: RenderTexture на каждую камеру, привязка кнопок.
`TogglePanel`: открывает панель, **освобождает курсор**, отключает скрипты игрока
(`mouseLook/playerMovement/playerInteraction`), включает выбранную камеру; закрытие —
обратно. `Update` держит курсор разблокированным, пока панель открыта. `SwitchToCamera`
со «снежным» переходом (статик-шум).

### `TVInteractable.cs` / `RadioInteractable.cs`
- ТВ: `Activate()` → открыть/закрыть панель камер.
- Радио: `Activate()` переключает звук (AudioSource), частицы и индикатор вкл/выкл.

---

## 5. Таймер и переход — `ShopTimer.cs`

Таймер смены (MM:SS, мигает красным под конец). Сам создаёт отдельный Canvas
(sortingOrder −1, без рейкастера — не блокирует клики). По нулю →
`SceneTransition.Instance.LoadSceneWithFade(nextSceneName)` (обычно **EquipmentShop**).
Есть Pause/Resume/Add/Set/GetRemainingTime. Длительность смены задаёт
`DifficultyManager` (через конфиг уровня).

### `SceneTransition.cs`
Плавный fade между сценами. Fullscreen-Image поверх всего, **без GraphicRaycaster**
(чтобы после перехода не блокировать клики). `LoadSceneWithFade`: FadeOut → LoadScene →
FadeIn.

---

## 6. Сложность и экран телефона (связь с сервером)

### `DifficultyManager.cs` (mode = Shop)
`Start` → **GET `/api/me`** (уровень) → **GET `/api/levels/{n}`** → `Current`. `Apply()`
для лавки настраивает `CustomerManager`: `badChance`, `minOrder`, `maxOrder`,
`customerMoveDuration` (всё растёт с уровнем; значения «смержены» с оверрайдами админки).

### `ScreenReporter.cs`
В сцене Shop стоит со `screen = clues` → **POST `/api/state/screen`** → телефон
показывает страницу примет (`clues.html`), чтобы игрок сверялся.

---

## 7. Что лавка шлёт на сервер (сводка)

| Скрипт | Эндпоинт | Когда | Зачем |
|--------|----------|-------|-------|
| CustomerManager | GET /api/clues | старт | накопленные приметы плохих клиентов |
| CustomerManager | GET /api/me | старт | стартовый баланс на табло |
| CustomerManager | POST /api/economy/earn | обслужил/скип | начислить/списать деньги |
| DifficultyManager | GET /api/me, GET /api/levels/{n} | старт | параметры лавки (badChance, заказ, скорость) |
| ScreenReporter | POST /api/state/screen=clues | старт | телефон → страница примет |

(Покупки снаряжения тут НЕ происходят — это сцена EquipmentShop/телефон. Подробнее
про сервер — `Docs/API_INTEGRATION.md`.)

---

## 8. Чек-лист «что должно быть в сцене Shop»

- **`CustomerManager`** заполнен: точки спавна/входа/выхода, визуалы клиентов, ставня и
  её точки, `DialogUI`, портретная камера и её цель, камера игрока, скрипты для
  отключения в диалоге, `FeedbackFX`, шаблоны реплик; **карта `clueObjectPrefabs`**
  (`clueId → prefab + точка спавна`) — для предметных примет; точки `clueSpawnPoints`
  больше не нужны (точка хранится в карте).
- **Игрок**: `PlayerMovement` + `MouseLook` + `PlayerInteraction` (с `pickupMask`,
  включающим слой кнопки/полок), коллайдеры/слои настроены.
- **Окно выдачи**: `AcceptZone` (триггер) → ссылается на `CustomerManager`.
- **Полки**: `ShelfItem` с префабом бутылки (бутылка имеет `Deliverable` + Rigidbody).
- **Кнопка скип**: worldspace-объект с `SkipButton` (Layer в `pickupMask`).
- **ТВ/радио**: `TVInteractable`/`RadioInteractable` (если есть камеры).
- **`ShopTimer`**: `Next Scene Name = EquipmentShop`.
- **`DifficultyManager`** с `mode = Shop`.
- **`ScreenReporter`** с `screen = clues`.
- (`SceneTransition` создаётся сам, если его нет.)

---

## 9. Связанные сцены/доки

- Сервер и API — `Docs/API_INTEGRATION.md`.
- Полный справочник по всем скриптам — `SCRIPTS_README.md`.
- Приметы наполняются в `gamedata.py` (`CLUES`) и правятся накоплением на бэке
  (`_ensure_revealed`); карта `clueObjectPrefabs` в `CustomerManager` связывает их id
  с префабами в лавке.
