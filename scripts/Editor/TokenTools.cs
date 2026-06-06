#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TokenTools
{
    const string TOKEN_KEY = "cd_game_token";

    [MenuItem("Tools/Pairing/Clear Game Token")]
    public static void ClearGameToken()
    {
        bool had = PlayerPrefs.HasKey(TOKEN_KEY);
        PlayerPrefs.DeleteKey(TOKEN_KEY);
        PlayerPrefs.Save();
        Debug.Log(had ? "[TokenTools] Game token cleared." : "[TokenTools] No token was set.");
    }

    [MenuItem("Tools/Pairing/Show Game Token")]
    public static void ShowGameToken()
    {
        string t = PlayerPrefs.GetString(TOKEN_KEY, "");
        Debug.Log(string.IsNullOrEmpty(t) ? "[TokenTools] Token is EMPTY." : $"[TokenTools] Token: {t}");
    }
}
#endif
