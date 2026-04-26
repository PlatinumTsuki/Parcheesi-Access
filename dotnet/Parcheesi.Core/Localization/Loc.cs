using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Parcheesi.Core.Localization;

/// <summary>
/// Localisation simple : un dictionnaire clé → texte par langue.
/// Utilise un loader externe (passé à <see cref="Init"/>) pour ouvrir les fichiers JSON
/// — ainsi Core n'est pas couplé à l'assembly App qui embarque les ressources.
///
/// Usage :
///   Loc.Init("fr", lang => assembly.GetManifestResourceStream($"Lang.{lang}.json"));
///   Loc.Get("menu.start")              // → "Commencer la partie"
///   Loc.Format("piece.on_cell", 1, 13) // → "Pion 1 sur case 13."
///
/// Si une clé est absente dans la langue active, fallback sur le français (langue de référence).
/// Si absente même en français, retourne la clé entre crochets — utile pour repérer les oublis.
/// </summary>
public static class Loc
{
    public static readonly string[] SupportedLanguages = { "fr", "en", "es" };
    public const string DefaultLanguage = "fr";

    private static Dictionary<string, string> _current = new();
    private static Dictionary<string, string> _fallback = new();
    private static string _currentLanguage = DefaultLanguage;
    private static Func<string, Stream?>? _loader;

    public static string CurrentLanguage => _currentLanguage;

    /// <summary>
    /// Détecte la langue préférée depuis la locale système. Renvoie "fr", "en" ou "es"
    /// selon CurrentUICulture, ou "fr" par défaut.
    /// </summary>
    public static string DetectSystemLanguage()
    {
        var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return SupportedLanguages.Contains(twoLetter) ? twoLetter : DefaultLanguage;
    }

    /// <param name="loader">Fonction qui ouvre un fichier de langue par son nom court ("fr", "en", "es")
    /// et renvoie un Stream JSON, ou null si introuvable.</param>
    public static void Init(string language, Func<string, Stream?> loader)
    {
        _loader = loader;
        _fallback = LoadLanguage(DefaultLanguage) ?? new();
        _currentLanguage = SupportedLanguages.Contains(language) ? language : DefaultLanguage;
        _current = _currentLanguage == DefaultLanguage
            ? _fallback
            : LoadLanguage(_currentLanguage) ?? _fallback;
    }

    private static Dictionary<string, string>? LoadLanguage(string lang)
    {
        if (_loader == null) return null;
        try
        {
            using var stream = _loader(lang);
            if (stream == null) return null;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch { return null; }
    }

    /// <summary>Renvoie le texte localisé pour une clé. Fallback français, puis "[key]".</summary>
    public static string Get(string key)
    {
        if (_current.TryGetValue(key, out var v)) return v;
        if (_fallback.TryGetValue(key, out var fb)) return fb;
        return $"[{key}]";
    }

    /// <summary>Renvoie le texte localisé formaté avec des arguments (string.Format).</summary>
    public static string Format(string key, params object?[] args)
    {
        var template = Get(key);
        try { return string.Format(CultureInfo.InvariantCulture, template, args); }
        catch { return template; }
    }
}
