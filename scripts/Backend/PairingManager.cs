using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.SceneManagement;

public class PairingManager : MonoBehaviour
{
    [Header("UI (опционально)")]
    public RawImage qrImage;
    public TMP_Text codeText;
    public TMP_Text statusText;

    [Header("Поток")]
    public string introSceneName = "Shop";
    public float pollInterval = 2f;
    public bool skipIfAlreadyPaired = true;
    [Tooltip("Ждать окончания вступительного чата на телефоне перед загрузкой сцены.")]
    public bool waitForPhoneIntro = true;
    [Tooltip("Макс. ожидание интро (сек), потом грузим сцену в любом случае — защита от зависания.")]
    public float introMaxWait = 180f;

    bool _introDone = false;

    string _code, _pollSecret;

    void Start()
    {
        GameApi.Ensure();
        if (skipIfAlreadyPaired && GameApi.HasToken) { GoIntro(); return; }
        StartPairing();
    }

    void StartPairing()
    {
        SetStatus("Подключение к серверу...");
        GameApi.Instance.Post("/api/pair/start", "{}", (ok, body) =>
        {
            if (!ok) { SetStatus("Нет связи с сервером"); StartCoroutine(Retry()); return; }
            var r = JsonUtility.FromJson<PairStartResp>(body);
            _code = r.code;
            _pollSecret = r.poll_secret;
            if (codeText) codeText.text = r.code;
            ShowQr(r.qr_png_base64);
            SetStatus("Отсканируй QR телефоном и войди на сайте");
            StartCoroutine(PollLoop());
        }, auth: false);
    }

    IEnumerator Retry() { yield return new WaitForSeconds(3f); StartPairing(); }

    void ShowQr(string b64)
    {
        if (qrImage == null || string.IsNullOrEmpty(b64)) return;
        byte[] png = System.Convert.FromBase64String(b64);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(png);
        tex.filterMode = FilterMode.Point;
        qrImage.texture = tex;
    }

    IEnumerator PollLoop()
    {
        while (!GameApi.HasToken)
        {
            yield return new WaitForSeconds(pollInterval);

            string url = BackendConfig.BaseUrl.TrimEnd('/') +
                         $"/api/pair/status?code={_code}&poll_secret={UnityWebRequest.EscapeURL(_pollSecret)}";
            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) continue;

                var r = JsonUtility.FromJson<PairStatusResp>(req.downloadHandler.text);
                if (r.status == "linked" && !string.IsNullOrEmpty(r.game_token))
                {
                    GameApi.Token = r.game_token;
                    SetStatus($"Привет, {r.username}! Загрузка...");
                }
                else if (r.status == "expired")
                {
                    SetStatus("Код истёк, получаю новый...");
                    StartPairing();
                    yield break;
                }
            }
        }
        yield return new WaitForSeconds(0.8f);
        if (waitForPhoneIntro)
            yield return StartCoroutine(WaitIntroThenLoad());
        else
            GoIntro();
    }

    IEnumerator WaitIntroThenLoad()
    {
        SetStatus("Смотри в телефон — инструктаж…");
        GameApi.Instance.Post("/api/gate/reset", "{}", (ok, body) => { });
        yield return new WaitForSeconds(1f);

        float waited = 0f;
        while (!_introDone && waited < introMaxWait)
        {
            GameApi.Instance.Get("/api/gate/status", (ok, body) =>
            {
                if (ok && JsonUtility.FromJson<IntroStatusResp>(body).done) _introDone = true;
            });
            yield return new WaitForSeconds(pollInterval);
            waited += pollInterval;
        }
        GoIntro();
    }

    void GoIntro()
    {
        if (!string.IsNullOrEmpty(introSceneName))
            SceneManager.LoadScene(introSceneName);
    }

    void SetStatus(string s)
    {
        if (statusText) statusText.text = s;
    }
}
