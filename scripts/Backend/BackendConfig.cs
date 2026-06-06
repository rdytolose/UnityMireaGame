using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class BackendConfig
{
    private const string DefaultBaseUrl = "http://game.podrik150cm.space:8081";

    private const string ConfigFileName = "server.cfg";

    private static string _baseUrl;

    public static string BaseUrl
    {
        get
        {
            if (_baseUrl == null) Load();
            return _baseUrl;
        }
    }

    private static void Load()
    {
        _baseUrl = DefaultBaseUrl;
        foreach (string path in CandidatePaths())
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                string url = ParseUrl(File.ReadAllText(path));
                if (!string.IsNullOrEmpty(url))
                {
                    _baseUrl = url;
                    Debug.Log($"[BackendConfig] Сервер из {path}: {_baseUrl}");
                    return;
                }
            }
            catch {  }
        }
    }

    private static IEnumerable<string> CandidatePaths()
    {
        string exeDir = Directory.GetParent(Application.dataPath)?.FullName;
        if (!string.IsNullOrEmpty(exeDir))
            yield return Path.Combine(exeDir, ConfigFileName);

        yield return Path.Combine(Application.streamingAssetsPath, ConfigFileName);
    }

    private static string ParseUrl(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;
        foreach (string raw in content.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

            int eq = line.IndexOf('=');
            string value = eq >= 0 ? line.Substring(eq + 1).Trim() : line;

            if (value.StartsWith("http://") || value.StartsWith("https://"))
                return value.TrimEnd('/');
        }
        return null;
    }
}
