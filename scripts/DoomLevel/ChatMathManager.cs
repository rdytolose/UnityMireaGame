using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Математика во время боя. Повесь на пустой объект в сцене doom.
///
/// Цикл: игра просит у бэка новый пример (POST /api/chat/question) → телефон (chat.html)
/// показывает пример и 3 варианта → игрок отвечает на телефоне → игра опрашивает
/// /api/chat/state и реагирует:
///   - верно   → ничего, следующий вопрос через askInterval;
///   - неверно → урон wrongAnswerDamage;
///   - не отвечает дольше answerGrace сек → на экране «СМОТРИ В ТЕЛЕФОН» и урон
///     damagePerTick каждые tickInterval, пока не ответит.
/// </summary>
public class ChatMathManager : MonoBehaviour
{
    [Header("Refs (находится сам, если пусто)")]
    public DoomHealth playerHealth;

    [Header("Тайминги")]
    public float firstDelay = 3f;       // пауза перед первым вопросом
    public float askInterval = 8f;      // пауза между вопросами после ответа
    public float answerGrace = 5f;      // сколько ждать ответа без урона
    public float tickInterval = 1f;     // как часто бить за молчание
    public float pollInterval = 0.5f;   // как часто спрашивать бэк о результате

    [Header("Штрафы (HP)")]
    public int wrongAnswerDamage = 20;
    public int damagePerTick = 5;

    [Header("Подсказка на экране (создастся сама, если пусто)")]
    public TMP_Text hintText;
    public string hintMessage = "СМОТРИ В ТЕЛЕФОН!";
    public Color hintColor = new Color(1f, 0.25f, 0.25f, 1f);

    int currentQid = 0;
    string status = "idle";

    void Start()
    {
        if (playerHealth == null)
        {
            foreach (var h in FindObjectsByType<DoomHealth>(FindObjectsSortMode.None))
                if (h.isPlayer) { playerHealth = h; break; }
        }
        GameApi.Ensure();
        if (hintText == null) CreateHintUI();
        HideHint();
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            if (playerHealth != null && playerHealth.IsDead()) yield break;
            yield return StartCoroutine(AskAndWait());
            yield return new WaitForSeconds(askInterval);
        }
    }

    IEnumerator AskAndWait()
    {
        // 1) Просим бэк сгенерить новый пример (телефон его покажет).
        bool created = false;
        status = "pending";
        GameSession.ChatNewQuestion(q => { currentQid = q.id; created = true; });

        float t0 = Time.time;
        while (!created && Time.time - t0 < 5f) yield return null;
        if (!created) yield break;   // бэк недоступен — выходим, попробуем на след. круге

        // 2) Ждём ответа с телефона, опрашивая статус. Молчание = урон после grace.
        HideHint();
        float start = Time.time;
        float nextPoll = 0f;
        float nextTick = start + answerGrace + tickInterval;
        status = "pending";

        while (status == "pending")
        {
            if (playerHealth != null && playerHealth.IsDead()) { HideHint(); yield break; }

            if (Time.time >= nextPoll)
            {
                nextPoll = Time.time + pollInterval;
                GameSession.FetchChatState(s => { if (s.id == currentQid) status = s.status; });
            }

            if (Time.time - start >= answerGrace)
            {
                ShowHint();
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + tickInterval;
                    if (playerHealth != null) playerHealth.TakeDamage(damagePerTick);
                }
            }

            yield return null;
        }

        // 3) Ответ получен.
        HideHint();
        if (status == "wrong" && playerHealth != null)
            playerHealth.TakeDamage(wrongAnswerDamage);
        // status == "correct" → без урона
    }

    void ShowHint() { if (hintText != null) hintText.enabled = true; }
    void HideHint() { if (hintText != null) hintText.enabled = false; }

    void CreateHintUI()
    {
        var canvasObj = new GameObject("ChatHintCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // без GraphicRaycaster — подсказка ничего не должна перехватывать

        var textObj = new GameObject("ChatHintText");
        textObj.transform.SetParent(canvas.transform, false);
        hintText = textObj.AddComponent<TextMeshProUGUI>();
        hintText.text = hintMessage;
        hintText.fontSize = 56;
        hintText.color = hintColor;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.raycastTarget = false;
        var font = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
        if (font == null)
        {
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (all.Length > 0) font = all[0];
        }
        if (font != null) hintText.font = font;

        var rect = hintText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0, 160);
        rect.sizeDelta = new Vector2(1200, 120);
    }
}
