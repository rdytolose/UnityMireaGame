using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Тонкий HTTP-клиент к бэкенду. Singleton с DontDestroyOnLoad, поэтому
/// корутины-запросы переживают смену сцены. Токен игры хранится в PlayerPrefs.
/// </summary>
public class GameApi : MonoBehaviour
{
    public static GameApi Instance { get; private set; }
    const string TOKEN_KEY = "cd_game_token";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Создаёт GameApi в сцене, если его ещё нет.</summary>
    public static void Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("GameApi");
            go.AddComponent<GameApi>();
        }
    }

    // ---- Токен ----
    public static string Token
    {
        get => PlayerPrefs.GetString(TOKEN_KEY, "");
        set { PlayerPrefs.SetString(TOKEN_KEY, value); PlayerPrefs.Save(); }
    }
    public static bool HasToken => !string.IsNullOrEmpty(Token);
    public static void ClearToken() { PlayerPrefs.DeleteKey(TOKEN_KEY); PlayerPrefs.Save(); }

    // ---- Запросы (callback: ok, тело-ответа) ----
    public void Get(string path, Action<bool, string> cb, bool auth = true)
        => StartCoroutine(Send("GET", path, null, cb, auth));

    public void Post(string path, string json, Action<bool, string> cb, bool auth = true)
        => StartCoroutine(Send("POST", path, json, cb, auth));

    IEnumerator Send(string method, string path, string json, Action<bool, string> cb, bool auth)
    {
        string url = BackendConfig.BaseUrl.TrimEnd('/') + path;
        using (var req = new UnityWebRequest(url, method))
        {
            if (json != null)
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (auth && HasToken)
                req.SetRequestHeader("Authorization", "Bearer " + Token);

            yield return req.SendWebRequest();

            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.LogWarning($"[GameApi] {method} {path} -> {req.responseCode} {req.error} {req.downloadHandler.text}");
            cb?.Invoke(ok, req.downloadHandler != null ? req.downloadHandler.text : "");
        }
    }
}
