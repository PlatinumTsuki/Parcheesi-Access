using System.IO;

namespace Parcheesi.App;

/// <summary>
/// Centralise les chemins de données utilisateur.
///
/// Avant 1.0.1 : chaque .exe stockait ses .json à côté de lui-même (modèle portable).
/// → Inconvénient : changer de version = perdre stats / réglages / sauvegarde.
///
/// Depuis 1.0.2 : tout est dans %APPDATA%\Parcheesi-Access\ — partagé entre toutes les
/// versions du jeu sur le même compte Windows. À la première exécution d'une version
/// récente, les anciens fichiers stockés à côté du .exe sont automatiquement copiés
/// vers AppData (les originaux sont conservés pour permettre un rollback éventuel).
/// </summary>
public static class UserDataPaths
{
    /// <summary>Liste des fichiers gérés (pour la migration). À tenir à jour.</summary>
    private static readonly string[] ManagedFiles =
    {
        "settings.json",
        "stats.json",
        "achievements.json",
        "current_game.json",
        "last_game.log",
        "audio_load.log",
        "crash.log",
    };

    private static readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Parcheesi-Access");

    public static string Root => _root;

    /// <summary>
    /// Crée le dossier de données si nécessaire et renvoie le chemin complet d'un fichier.
    /// </summary>
    public static string Get(string filename)
    {
        Directory.CreateDirectory(_root);
        return Path.Combine(_root, filename);
    }

    /// <summary>
    /// Migre une fois pour toutes les anciens fichiers stockés à côté du .exe vers AppData.
    /// Idempotent : si un fichier AppData existe déjà, on ne le touche pas (ne pas écraser
    /// les données récentes par celles d'un .exe plus ancien lancé entre temps).
    /// Échecs silencieux : si la copie rate (fichier verrouillé, droits…), on continue
    /// sans crasher — l'utilisateur partira sur des données vierges en pire des cas.
    /// </summary>
    public static void MigrateFromLegacyIfNeeded()
    {
        Directory.CreateDirectory(_root);
        foreach (var filename in ManagedFiles)
        {
            var newPath = Path.Combine(_root, filename);
            if (File.Exists(newPath)) continue;
            var legacyPath = Path.Combine(AppContext.BaseDirectory, filename);
            if (!File.Exists(legacyPath)) continue;
            try { File.Copy(legacyPath, newPath); }
            catch { /* silent — données vierges en pire des cas */ }
        }
    }
}
