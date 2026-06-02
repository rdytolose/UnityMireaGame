ИНСТРУКЦИЯ ПО НАСТРОЙКЕ КАТСЦЕНЫ В АВТОБУСЕ
==============================================

1. СОЗДАНИЕ ОБЪЕКТА В СЦЕНЕ:
   - Создайте пустой GameObject в сцене (правый клик в Hierarchy -> Create Empty)
   - Назовите его "BusCutsceneManager"

2. ДОБАВЛЕНИЕ СКРИПТОВ:
   - Выберите объект BusCutsceneManager
   - В Inspector нажмите "Add Component"
   - Добавьте скрипт "BusCutsceneManager"
   - Добавьте скрипт "DialogueSystem" (он добавится автоматически если нужен)

3. НАСТРОЙКА ДИАЛОГА:
   В компоненте DialogueSystem:
   
   a) Dialogue Lines - нажмите "+" чтобы добавить реплики:
      
      Реплика 1:
      - Speaker Name: "Мама"
      - Text: "Привет, сынок! Как дела в школе?"
      - Duration: 3
      
      Реплика 2:
      - Speaker Name: "Я"
      - Text: "Привет, мам. Все нормально, еду домой."
      - Duration: 3
      
      Реплика 3:
      - Speaker Name: "Мама"
      - Text: "Хорошо, жду тебя. Будь осторожен!"
      - Duration: 3

   b) Настройки UI (можно оставить по умолчанию):
      - Auto Text Speed: 0.05
      - Typewriter Effect: ✓ (включено)
      - Font Size: 32
      - Panel Color: черный полупрозрачный

4. НАСТРОЙКА МЕНЕДЖЕРА:
   В компоненте BusCutsceneManager:
   - Dialogue System: перетащите сюда этот же объект
   - Play On Start: ✓ (включено)
   - Delay Before Dialogue: 1

5. ЗАПУСК:
   - Нажмите Play в Unity
   - Катсцена должна запуститься автоматически
   - В Console будут видны отладочные сообщения

6. ПРОВЕРКА ЛОГОВ:
   В Console должны появиться сообщения:
   - "BusCutsceneManager Start() called"
   - "DialogueSystem Start() called"
   - "Starting cutscene..."
   - "Starting dialogue with X lines"
   - "Showing line 0: ..."

РЕШЕНИЕ ПРОБЛЕМ:
================

Если диалог не появляется:
1. Проверьте Console на ошибки
2. Убедитесь что Dialogue Lines заполнены
3. Проверьте что оба скрипта добавлены на один объект
4. Убедитесь что в сцене есть Canvas (создастся автоматически)

Если текст не виден:
1. Проверьте что Canvas в режиме Screen Space - Overlay
2. Проверьте Panel Color (альфа должна быть > 0)
3. Проверьте Font Size (должен быть > 0)

ДОПОЛНИТЕЛЬНО:
==============

Для добавления звуков:
- Phone Ring Sound: звук звонка телефона
- Phone Pickup Sound: звук поднятия трубки
- Voice Clip: озвучка для каждой реплики (опционально)

Перетащите аудиофайлы из папки Assets в соответствующие поля.
