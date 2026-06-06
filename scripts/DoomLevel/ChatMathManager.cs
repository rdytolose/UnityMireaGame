using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatMathManager : MonoBehaviour
{
    [Header("Refs (находится сам, если пусто)")]
    public DoomHealth playerHealth;

    [Header("Тайминги")]
    public float firstDelay = 3f;
    public float askInterval = 8f;
    public float answerGrace = 5f;
    public float tickInterval = 1f;
    public float pollInterval = 0.5f;

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
        bool created = false;
        status = "pending";
        GameSession.ChatNewQuestion(q => { currentQid = q.id; created = true; });

        float t0 = Time.time;
        while (!created && Time.time - t0 < 5f) yield return null;
        if (!created) yield break;

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

        HideHint();
        if (status == "wrong" && playerHealth != null)
            playerHealth.TakeDamage(wrongAnswerDamage);
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
