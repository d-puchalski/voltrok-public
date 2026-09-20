using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace VoltrokServices.Services.Translations;

public sealed class TranslationService
{
    private const string DefaultLanguage = "en";
    private readonly string _translationsPath;
    private readonly IReadOnlyDictionary<string, string> _defaultTranslations;
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _translationsByLanguage =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, string> _activeTranslations;

    public TranslationService(IWebHostEnvironment environment)
    {
        _translationsPath = Path.Combine(environment.ContentRootPath, "wwwroot", "assets", "translations");
        _defaultTranslations = ReadLanguageFile(DefaultLanguage);
        _translationsByLanguage.TryAdd(DefaultLanguage, _defaultTranslations);
        _activeTranslations = _defaultTranslations;
    }

    public string this[string key] => Translate(key);

    public bool SetLanguage(string? languageCode)
    {
        var normalizedCode = NormalizeLanguageCode(languageCode);
        _activeTranslations = LoadLanguage(normalizedCode);
        return true;
    }

    public string Translate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (_activeTranslations.TryGetValue(key, out var localizedValue))
        {
            return localizedValue;
        }

        return _defaultTranslations.GetValueOrDefault(key, key);
    }

    public IReadOnlyDictionary<string, string> GetTranslationsByPrefix(params string[] prefixes)
    {
        if (prefixes == null || prefixes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedPrefixes = prefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .ToArray();

        if (normalizedPrefixes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in _defaultTranslations)
        {
            if (normalizedPrefixes.Any(prefix => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                result[entry.Key] = entry.Value;
            }
        }

        foreach (var entry in _activeTranslations)
        {
            if (normalizedPrefixes.Any(prefix => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                result[entry.Key] = entry.Value;
            }
        }

        return result;
    }

    private IReadOnlyDictionary<string, string> LoadLanguage(string languageCode)
    {
        return _translationsByLanguage.GetOrAdd(languageCode, code =>
        {
            var loaded = ReadLanguageFile(code);
            if (loaded.Count == 0)
            {
                return _defaultTranslations;
            }

            return loaded;
        });
    }

    private IReadOnlyDictionary<string, string> ReadLanguageFile(string languageCode)
    {
        var path = Path.Combine(_translationsPath, $"{languageCode}.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(path);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguage;
        }

        var normalized = languageCode.Trim();
        var primary = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
        return string.IsNullOrWhiteSpace(primary) ? DefaultLanguage : primary;
    }
}
