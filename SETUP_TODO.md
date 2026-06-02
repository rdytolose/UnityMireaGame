# Что доделать руками (TODO для редактора/сервера)

Код всех фич уже написан. Ниже — ручные шаги, которые нужно сделать в Unity-редакторе
и на сервере, чтобы фичи заработали.

---

## A. Фича: улики + математика в бою (последнее, что делали)

Три системы: пул примет с памятью, связка примет с игрой, математика в чате doom.

### 1. Бэкенд

1. Заполнить пул примет `CLUES` в `temp/backend/app/gamedata.py`.
   Поля каждой улики: `id`, `emoji`, `name`, `text`, `type` (`object` | `text`).
   Для `type: "text"` добавить `phrase` — фразу, которую клиент вставит в реплику.
   `id` должны совпадать с тем, что впишешь в карту в Unity (см. ниже).
2. Задеплоить: `docker compose up -d --build backend frontend`.
3. ⚠️ Появились новые колонки в БД (`revealed_clues`, `chat_q_*`, `equipped_weapon`).
   `create_all` не меняет существующие таблицы → **пересоздать БД**:
   `docker compose down -v && docker compose up -d --build`
   (после `down -v` придётся заново спарситься с телефона).

### 2. Unity — сцена Shop

- На `CustomerManager` заполнить **`Clue Object Prefabs`**: для каждой `object`-улики
  пара `clueId` (как на бэке) → `prefab` (предмет в лавке).
- `Clue Spawn Points` уже расставлены — проверить, что не пустые.

### 3. Unity — сцена doom

- Пустой объект → `Add Component → ChatMathManager` (рефы и UI-подсказка создаются сами).
  При желании покрутить: `askInterval`, `answerGrace` (5с), `wrongAnswerDamage` (20),
  `damagePerTick` (5).
- Проверить, что в сцене есть `ScreenReporter` со `screen = chat` (иначе телефон
  не переключится на страницу математики).

### Проверка

- Shop день 1 → в телефоне 1 примета; плохой клиент проявляет её (предмет или фраза).
- После doom (уровень+1) → день 2 → 2 приметы (старая + новая случайная, запоминается).
- В doom: не ответил на пример 5с → урон + «СМОТРИ В ТЕЛЕФОН»; неверно → −20 HP.

---

## B. Фича: выбор оружия на сайте (владеешь многими — активен один)

Код готов. Нужно:

- Деплой бэка/фронта + пересоздать БД (см. A.1 п.2–3 — те же команды).
- В Unity на объекте `Loadout` (сцена doom) заполнить:
  - `Pistol / Shotgun / Smg Controller` — Animator Controller'ы стволов (Idle + триггер `Shoot`).
  - `Pistol / Shotgun / Smg Sfx` — звуки выстрела.
- Проверка: купить оружие в телефоне → нажать «Экипировать» → в doom активен выбранный
  ствол со своей анимацией и звуком.

---

## C. Фича: переходный уровень закупки (EquipmentShop)

Скрипт `EquipmentPhaseManager` готов. Нужно собрать сцену:

1. `File → New Scene` → сохранить как `Assets/Scenes/EquipmentShop.unity` (имя ровно так).
2. Main Camera (фон — тёмный Solid Color).
3. Пустой объект → `Add Component → EquipmentPhaseManager`:
   `Buy Duration = 45`, `Next Scene Name = doom`, `Phone Screen = shop`.
4. **Build Settings** → добавить `EquipmentShop.unity` в список сцен.
5. В сцене Shop на `ShopTimer`: `Next Scene Name = EquipmentShop`, назначить `Timer Text`.
6. В сцене doom на `DoomTimer`: `Cutscene Scene Name = Shop` (замкнуть roguelike-петлю).

Полная петля: Shop → EquipmentShop (закупка в телефоне) → doom → Shop (сложнее) → …

---

## D. Мелочи / на будущее

- Патроны сейчас «спят» (200 + бесплатный R). Если делать ресурсом — включить `Ammo Text`
  в `DoomUI` и убрать халявную перезарядку; тогда перк `ammo_belt` обретёт смысл.
- Отладка пейринга: `Tools → Pairing → Clear Game Token` / `Show Game Token`.
- Адрес бэка в игре: `Assets/scripts/Backend/BackendConfig.cs`
  (`http://game.podrik150cm.space:8081`).
