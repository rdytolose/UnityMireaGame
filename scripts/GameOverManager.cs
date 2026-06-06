using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverManager : MonoBehaviour
{
    [Header("Сцена пейринга (для «начать заново»)")]
    public string pairingScene = "Pairing";

    [Header("Твои UI-элементы (перетащи с канваса)")]
    public TMP_Text titleText;
    public TMP_Text statsText;
    public Button restartButton;
    public Button quitButton;

    [Header("Тексты исхода")]
    public string winText = "ПОБЕДА";
    public string loseText = "ПОРАЖЕНИЕ";
    public string quitText = "СМЕНА ПРЕРВАНА";
    public Color winColor = new Color(0.4f, 1f, 0.45f);
    public Color loseColor = new Color(1f, 0.35f, 0.3f);

    int _money = -1;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var outcome = GameStats.I != null ? GameStats.I.outcome : GameStats.Outcome.Lose;

        if (titleText != null)
        {
            titleText.text = outcome == GameStats.Outcome.Win ? winText
                           : outcome == GameStats.Outcome.Quit ? quitText : loseText;
            titleText.color = outcome == GameStats.Outcome.Win ? winColor : loseColor;
        }

        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

        RenderStats();

        GameApi.Ensure();
        GameSession.FetchMe(me => { _money = (me != null) ? me.money : 0; RenderStats(); });
    }

    void RenderStats()
    {
        if (statsText == null) return;
        var s = GameStats.Ensure();
        int acc = s.shotsFired > 0 ? Mathf.RoundToInt(100f * s.hits / s.shotsFired) : 0;
        var sb = new StringBuilder();
        sb.AppendLine($"Убито мобов:      {s.kills}");
        sb.AppendLine($"Макс. уровень:    {s.maxLevel}");
        sb.AppendLine($"Последнее оружие: {s.lastWeapon}");
        sb.AppendLine($"Выстрелов:        {s.shotsFired}  (точность {acc}%)");
        sb.AppendLine($"Деньги:           {(_money >= 0 ? "$" + _money : "—")}");
        statsText.text = sb.ToString();
    }

    void Restart()
    {
        GameApi.ClearToken();
        GameStats.ResetRun();
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.LoadSceneWithFade(pairingScene);
        else
            SceneManager.LoadScene(pairingScene);
    }

    void QuitGame()
    {
        GameApi.ClearToken();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
