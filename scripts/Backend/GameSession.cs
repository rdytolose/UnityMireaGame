using System;
using UnityEngine;

public static class GameSession
{
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

    public static void Buy(string itemId, Action<bool, string> onResult = null)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/shop/buy",
            JsonUtility.ToJson(new BuyReq { item_id = itemId }),
            (ok, body) => onResult?.Invoke(ok, body));
    }

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

    public static void SetScreen(string screen)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/state/screen",
            JsonUtility.ToJson(new ScreenReq { screen = screen }),
            (ok, body) => { });
    }

    public static void FetchMe(Action<MeResp> onMe)
    {
        GameApi.Ensure();
        GameApi.Instance.Get("/api/me", (ok, body) =>
        {
            if (ok) onMe?.Invoke(JsonUtility.FromJson<MeResp>(body));
        });
    }

    public static void FetchClues(Action<CluesResp> onClues)
    {
        GameApi.Ensure();
        GameApi.Instance.Get("/api/clues", (ok, body) =>
        {
            if (ok) onClues?.Invoke(JsonUtility.FromJson<CluesResp>(body));
        });
    }

    public static void ChatNewQuestion(Action<ChatQuestionResp> onQ)
    {
        GameApi.Ensure();
        GameApi.Instance.Post("/api/chat/question", "{}", (ok, body) =>
        {
            if (ok) onQ?.Invoke(JsonUtility.FromJson<ChatQuestionResp>(body));
        });
    }

    public static void FetchChatState(Action<ChatStateResp> onState)
    {
        GameApi.Ensure();
        GameApi.Instance.Get("/api/chat/state", (ok, body) =>
        {
            if (ok) onState?.Invoke(JsonUtility.FromJson<ChatStateResp>(body));
        });
    }
}
