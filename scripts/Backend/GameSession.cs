using System;
using UnityEngine;

/// <summary>
/// Удобные обёртки над эндпоинтами экономики/прогресса.
/// Вызывай из любого скрипта: GameSession.Earn(100); и т.п.
/// </summary>
public static class GameSession
{
    /// <summary>Начислить/списать деньги (магазин). На сервере не уходит ниже 0.</summary>
    public static void Earn(int amount, Action<int> onMoney = null)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/economy/earn",
            JsonUtility.ToJson(new EarnReq { amount = amount }),
            (ok, body) =>
            {
                if (ok && onMoney != null)
                    onMoney(JsonUtility.FromJson<MoneyResp>(body).money);
            });
    }

    /// <summary>Купить предмет (оружие/перк) по id из каталога.</summary>
    public static void Buy(string itemId, Action<bool, string> onResult = null)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/shop/buy",
            JsonUtility.ToJson(new BuyReq { item_id = itemId }),
            (ok, body) => onResult?.Invoke(ok, body));
    }

    /// <summary>Вызвать, когда игрок выжил в Думе. Двигает прогресс на след. уровень.</summary>
    public static void CompleteLevel(Action<bool> onDone = null)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/progress/complete-level", "{}",
            (ok, body) =>
            {
                bool finished = false;
                if (ok) finished = JsonUtility.FromJson<CompleteLevelResp>(body).finished_game;
                onDone?.Invoke(finished);
            });
    }

    /// <summary>Сообщить бэкенду текущую сцену — телефон сам переключит страницу.
    /// Допустимые значения: "wait" | "clues" | "shop" | "chat" | "pause".</summary>
    public static void SetScreen(string screen)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/state/screen",
            JsonUtility.ToJson(new ScreenReq { screen = screen }),
            (ok, body) => { });
    }

    /// <summary>Текущее состояние игрока (деньги, уровень).</summary>
    public static void FetchMe(Action<MeResp> onMe)
    {
        GameApi.Ensure();
        GameApi.Instance.Get("/api/me", (ok, body) =>
        {
            if (ok) onMe?.Invoke(JsonUtility.FromJson<MeResp>(body));
        });
    }

    /// <summary>Накопленные приметы плохого клиента (растут случайно по дням).</summary>
    public static void FetchClues(Action<CluesResp> onClues)
    {
        GameApi.Ensure();
        GameApi.Instance.Get("/api/clues", (ok, body) =>
        {
            if (ok) onClues?.Invoke(JsonUtility.FromJson<CluesResp>(body));
        });
    }

    /// <summary>Создать новый пример в чате (бэк генерит, телефон покажет).</summary>
    public static void ChatNewQuestion(Action<ChatQuestionResp> onQ)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/chat/question", "{}", (ok, body) =>
        {
            if (ok) onQ?.Invoke(JsonUtility.FromJson<ChatQuestionResp>(body));
        });
    }

    /// <summary>Узнать, ответил ли игрок на телефоне и верно ли.</summary>
    public static void FetchChatState(Action<ChatStateResp> onState)
    {
        GameApi.Ensure();
        GameApi.Instance.Get("/api/chat/state", (ok, body) =>
        {
            if (ok) onState?.Invoke(JsonUtility.FromJson<ChatStateResp>(body));
        });
    }
}
