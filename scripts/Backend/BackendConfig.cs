using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Адрес сервера игры. По умолчанию — публичный сервер, но его можно
/// переопределить БЕЗ пересборки: положить рядом с билдом файл server.cfg
/// (или отредактировать тот, что лежит в *_Data/StreamingAssets/).
///
/// Формат server.cfg (обычный текст, правится блокнотом):
///     # адрес твоего сервера
///     server_url=http://my-server.com:8081
/// (можно также просто одной строкой http://my-server.com:8081)
/// </summary>
public static class BackendConfig
{
    // Значение по умолчанию, если server.cfg не найден/пуст.
    private const string DefaultBaseUrl = "http://game.podrik150cm.space:8081";

    // Имя конфиг-файла, который кладётся рядом с билдом.
    private const string ConfigFileName = "server.cfg";

    private static string _baseUrl;

    /// <summary>Адрес сервера. При первом обращении читается из server.cfg.</summary>
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
            catch { /* файл недоступен — пробуем следующий путь */ }
        }
    }

    private static IEnumerable<string> CandidatePaths()
    {
        // 1) рядом с исполняемым файлом игры (самый удобный для пользователя)
        string exeDir = Directory.GetParent(Application.dataPath)?.FullName;
        if (!string.IsNullOrEmpty(exeDir))
            yield return Path.Combine(exeDir, ConfigFileName);

        // 2) внутри билда — едет в комплекте по умолчанию
        yield return Path.Combine(Application.streamingAssetsPath, ConfigFileName);
    }

    private static string ParseUrl(string content)
    {
        if (string.IsNullOrEmpty(content)) return null;
        foreach (string raw in content.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;

            // поддержка "server_url=..." и просто "http://..."
            int eq = line.IndexOf('=');
            string value = eq >= 0 ? line.Substring(eq + 1).Trim() : line;

            if (value.StartsWith("http://") || value.StartsWith("https://"))
                return value.TrimEnd('/');
        }
        return null;
    }
}
