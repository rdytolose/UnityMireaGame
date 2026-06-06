using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GtaHud : MonoBehaviour
{
    [Header("Источники (находятся сами, если пусто)")]
    public DoomHealth playerHealth;
    public DoomTimer timer;

    [Header("Твои UI-элементы (перетащи с канваса)")]
    [Tooltip("Image полоски HP. Поставь у него Image Type = Filled, Fill Method = Horizontal, " +
             "Fill Origin = Left. Либо обычный Image с Pivot слева — тогда сработает масштаб.")]
    public Image hpFill;
    [Tooltip("Текст времени (сколько осталось), формат MM:SS.")]
    public TMP_Text timeText;
    [Tooltip("Текст денег. Обновляется один раз на старте (в бою деньги не меняются).")]
    public TMP_Text moneyText;
    public string moneyPrefix = "$";

    int _money = 0;

    void Start()
    {
        if (playerHealth == null)
            foreach (var h in FindObjectsByType<DoomHealth>(FindObjectsSortMode.None))
                if (h.isPlayer) { playerHealth = h; break; }
        if (timer == null) timer = FindFirstObjectByType<DoomTimer>();

        GameApi.Ensure();
        GameSession.FetchMe(me =>
        {
            _money = (me != null) ? me.money : 0;
            if (moneyText != null) moneyText.text = moneyPrefix + _money;
        });
        if (moneyText != null) moneyText.text = moneyPrefix + _money;
    }

    void Update()
    {
        if (hpFill != null && playerHealth != null)
        {
            int max = Mathf.Max(1, playerHealth.GetMaxHealth());
            float pct = Mathf.Clamp01((float)playerHealth.GetCurrentHealth() / max);
            if (hpFill.type == Image.Type.Filled)
                hpFill.fillAmount = pct;
            else
            {
                var s = hpFill.rectTransform.localScale;
                s.x = pct;
                hpFill.rectTransform.localScale = s;
            }
        }

        if (timeText != null && timer != null)
        {
            float t = Mathf.Max(0f, timer.GetRemainingTime());
            int m = Mathf.FloorToInt(t / 60f);
            int s = Mathf.FloorToInt(t % 60f);
            timeText.text = $"{m:00}:{s:00}";
        }
    }
}
