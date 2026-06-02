# Unity ↔ Backend — пошаговая интеграция

Гайд: как прикрутить бэкенд (QR-привязка, токен, деньги, магазин, сложность 1–10)
и 4 веб-страницы (`shop`, `clues`, `chat`, `wait`) к проекту.

---

## 0. Что мы строим (поток игры)

```
[Pairing scene]  ── показывает QR ──►  телефон сканит ──►  сайт /pair (вход) ──►  игра получает game-токен
       │
       ▼
[BusCutscene]  вступительный ролик
       │
       ▼
[Shop scene]   зарабатываем деньги (CustomerManager → /api/economy/earn)
               на телефоне открыта clues.html (приметы плохих клиентов)
       │  ShopTimer 3:00
       ▼
[EquipmentShop] тратим деньги (телефон: shop.html → /api/shop/buy)
       │
       ▼
[Doom scene]   выживаем survival_time секунд
               на телефоне открыта chat.html (диалог с девушкой)
               сложность из /api/levels/{n}: HP/скорость/урон/спавн мобов
       │  DoomTimer закончился → /api/progress/complete-level (level +1)
       ▼
[Cutscene] → следующий день/уровень … до 10-го
```

Веб-страницы открываются **на телефоне** (второй экран) — они уже готовы и
ходят в тот же бэкенд. Со стороны Unity их трогать не нужно (см. §8).

---

## 1. Бэкенд-скрипты

Все скрипты **уже лежат** в `Assets/scripts/Backend/`:

| Файл | Назначение |
|---|---|
| `BackendConfig.cs` | Адрес бэкенда (одна константа) |
| `GameApi.cs` | HTTP-клиент + хранение токена (singleton, DontDestroyOnLoad) |
| `BackendDTO.cs` | Классы под JSON-ответы для `JsonUtility` |
| `PairingManager.cs` | QR-привязка на старте |
| `GameSession.cs` | Обёртки: `Earn`, `Buy`, `CompleteLevel`, `FetchMe` |
| `DifficultyManager.cs` | Применяет сложность уровня к сцене |

Открой `BackendConfig.cs` и поставь свой адрес:
```csharp
public const string BaseUrl = "http://127.0.0.1:8000"; // или http://<ip>:8080 через docker
```

> ⚠️ Адрес должен быть доступен **с ПК, где запущена игра**. Для реальных тестов с
> телефоном бэкенд должен быть виден и телефону (LAN-IP или домен); в QR попадает
> `SITE_BASE_URL` из настроек бэкенда.

> Дополнительных зависимостей нет — используются `UnityWebRequest` и `JsonUtility`.

---

## 2. Сцена привязки (QR) — новая первая сцена

1. Создай сцену `Pairing` (File → New Scene) и сделай её **первой** в
   `Build Settings → Scenes In Build` (индекс 0).
2. На Canvas добавь:
   - `RawImage` (большой квадрат) — сюда выведется QR.
   - `TMP_Text` для кода (например «ABC123»).
   - `TMP_Text` для статуса («Отсканируй QR…»).
3. Создай пустой GameObject `Pairing`, повесь на него **`PairingManager`** и в инспекторе:
   - `Qr Image` → твой RawImage.
   - `Code Text` / `Status Text` → твои TMP_Text.
   - `Intro Scene Name` → имя сцены вступительного ролика (например `BusCutscene`).

Что делает `PairingManager` (уже реализовано):
- `POST /api/pair/start` → получает `code`, `qr_png_base64`, `poll_secret`,
  рисует QR из base64.
- Каждые 2 с опрашивает `GET /api/pair/status` пока пользователь не войдёт с телефона.
- Как только статус `linked` — сохраняет `game_token` в `PlayerPrefs` и грузит
  вступительную сцену.
- Если токен уже есть (повторный запуск) — сразу в катсцену.

> Все остальные сцены уже будут авторизованы: `GameApi` держит токен,
> в заголовок каждого запроса подставляется `Authorization: Bearer <token>`.

---

## 3. Деньги в магазине — `CustomerManager.cs`

### 3.1. Значения наград

Поля объявлены в инспекторе — правь прямо там **или** в коде (строки 21–24):

```csharp
public int rewardCorrectServe = 1;   // → поставь 100
public int penaltyServeBad   = -3;   // → поставь -300
public int rewardSkipBad      = 1;   // → поставь 100
public int penaltySkipGood   = -1;   // → поставь -100
```

### 3.2. Отправка денег на сервер ✅ уже сделано

`ChangeScore` в конце файла уже синхронизирует каждое изменение с бэкендом:

```csharp
void ChangeScore(int delta)
{
    score += delta;
    UpdateScoreUI();
    GameSession.Earn(delta);   // POST /api/economy/earn
}
```

Локальный `score` остаётся для UI, «настоящие» деньги хранятся на сервере
(там стоит floor 0 — не уходят в минус).

---

## 4. Сложность магазина — `DifficultyManager` (Shop) ✅ уже готово

1. В сцену магазина добавь пустой объект `Difficulty`, повесь **`DifficultyManager`**,
   в инспекторе `Mode = Shop`.
2. Он сам подтянет `bad_chance`, `min_order`, `max_order`, `customer_move_duration`
   из `/api/levels/{уровень}` и проставит их в `CustomerManager`.

Ничего больше менять не нужно — поля `CustomerManager` публичные.

> Количество активных «примет» (`clue_types`) отображается на телефоне (clues.html),
> поэтому в Unity для них кода не требуется.

---

## 5. Магазин снаряжения (EquipmentShop)

Покупки делаются с телефона на странице `shop.html` (`/api/shop/buy`) — это проще
всего и не требует Unity-кода. Если хочешь покупать **внутри** игры, вызывай:

```csharp
GameSession.Buy("pistol", (ok, body) => {
    if (ok) Debug.Log("Куплено!");
});
```

ID предметов берутся из `GET /api/shop/items` (`pistol`, `shotgun`, `smg`, `rifle`,
`rocket`, `armor`, `medkit`, `boots`, `ammo_belt` — точный список отдаёт бэкенд).

---

## 6. Сложность Дума — таймер, спавнер, враги

### 6.1. `DoomTimer.cs` — завершение уровня ✅ уже сделано

`SetTotalTime(float t)` уже существует (применяется из `DifficultyManager`).

`OnTimerEnd()` теперь вызывает `GameSession.CompleteLevel()` перед переходом в катсцену:

```csharp
void OnTimerEnd()
{
    GameSession.CompleteLevel(finished =>
    {
        if (finished) Debug.Log("[Дум] Игра пройдена!");
    });

    // затем переход на cutsceneSceneName (с fade или без)
}
```

> `GameApi` переживает смену сцены (DontDestroyOnLoad), поэтому POST успеет уйти
> даже при немедленном переходе.

### 6.2. Поставь `DifficultyManager` в сцену Дума

Пустой объект → `DifficultyManager`, `Mode = Shooter`. Он выставит
`DoomTimer.SetTotalTime`, `EnemySpawner.maxEnemies/minSpawnDelay/maxSpawnDelay`.

### 6.3. `DoomHealth.cs` — множитель здоровья ✅ уже сделано

Добавлен метод `ApplyHealthMultiplier(float mult)`:
- Запоминает базовое `maxHealth` один раз (`_baseMaxHealth`).
- Переиспользование из пула не «накручивает» HP.
- Вызывается **до** `ResetHealth()`.

### 6.4. `DoomEnemy.cs` — множители скорости и урона ✅ уже сделано

Добавлен метод `ApplyDifficulty(float speedMult, float damageMult)`:
- Запоминает базовые `moveSpeed`, `chaseSpeed`, `attackDamage` один раз.
- Применяет `agent.speed` сразу (с учётом текущего состояния chase/patrol).
- Переиспользование из пула не накручивает статы.

### 6.5. `EnemySpawner.cs` — применяем множители при спавне ✅ уже сделано

Сразу после `spawnedEnemies.Add(enemy)` вызывается `ApplyDifficultyToEnemy(enemy)`:

```csharp
void ApplyDifficultyToEnemy(GameObject enemy)
{
    var cfg = DifficultyManager.Current;
    if (cfg == null) return;

    var h = enemy.GetComponent<DoomHealth>();
    if (h != null) { h.ApplyHealthMultiplier(cfg.shooter.enemy_health_mult); h.ResetHealth(); }

    var de = enemy.GetComponent<DoomEnemy>();
    if (de != null) de.ApplyDifficulty(cfg.shooter.enemy_speed_mult, cfg.shooter.enemy_damage_mult);
}
```

Итог: каждый заспавненный моб получает HP/скорость/урон по уровню, а число мобов и
частота спавна берутся из конфига. Пул не ломается (значения считаются от базы).

---

## 7. (Опц.) Звонок завершения уровня без таймера

Если уровень должен засчитываться не по таймеру, а по другому событию
(например, дошёл до `LevelExit`), просто вызови оттуда:

```csharp
GameSession.CompleteLevel();
```

---

## 8. 4 веб-страницы на телефоне (когда что открывать)

Страницы уже готовы и работают с тем же бэкендом. Игроку их показывает **телефон**:

| Страница | Когда | URL |
|---|---|---|
| `pair.html` | при сканировании QR из игры | `/pair?code=XXXXXX` (в QR уже зашит) |
| `clues.html` | всю шоп-фазу | `/clues.html` |
| `shop.html` | в EquipmentShop (трата денег) | `/shop.html` |
| `chat.html` | во время Дума (диалог с девушкой) | `/chat.html` |
| `wait.html` | загрузка/пауза | `/wait.html?mode=loading` или `?mode=pause` |

Игроку достаточно один раз войти на телефоне (при привязке) — токен сайта сохранится,
и эти страницы будут открывать его данные.

**Если хочешь показывать страницы прямо внутри .exe** (в окне игры):
- **WebView-плагин** (`3D WebView` / `UniWebView` из Asset Store): открой нужный URL
  в момент сцены (clues — в начале шопа, chat — в начале Дума, wait — на паузе).
- **Нативно в Unity**: бери данные из тех же эндпоинтов (`/api/clues`, `/api/dialogue`)
  и рисуй своим UI. JSON-формат смотри в ответах бэкенда.

Для паузы Дума дёргай `DoomTimer.PauseTimer()` / `ResumeTimer()` и параллельно
открывай `wait.html?mode=pause` на телефоне.

---

## 9. Чек-лист тестирования

Подними бэкенд+фронт (`docker compose up -d --build`) и в `BackendConfig.BaseUrl`
укажи его адрес. Затем:

1. **Привязка**: запусти сцену `Pairing` → виден QR и код. Отсканируй телефоном →
   войди на сайте → игра должна перейти в катсцену.