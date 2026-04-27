using System.IO;
using System.Text.Json;
using Parcheesi.Core.Localization;

namespace Parcheesi.App;

public record Achievement(string Id, string Name, string Description);

/// <summary>
/// Système de succès débloquables. Persistance dans achievements.json.
/// </summary>
public class Achievements
{
    public List<string> UnlockedIds { get; set; } = new();

    /// <summary>
    /// Liste des succès, avec Name et Description résolus à la volée depuis Loc.
    /// On reconstruit la liste à chaque accès pour qu'un changement de langue (au prochain
    /// démarrage) ré-applique la traduction sans persister du texte localisé dans le code.
    /// </summary>
    public static List<Achievement> All => new()
    {
        new("first_win",          Loc.Get("achievement.first_win.name"),          Loc.Get("achievement.first_win.desc")),
        new("speed_run",          Loc.Get("achievement.speed_run.name"),          Loc.Get("achievement.speed_run.desc")),
        new("untouchable",        Loc.Get("achievement.untouchable.name"),        Loc.Get("achievement.untouchable.desc")),
        new("hunter",             Loc.Get("achievement.hunter.name"),             Loc.Get("achievement.hunter.desc")),
        new("hunter_master",      Loc.Get("achievement.hunter_master.name"),      Loc.Get("achievement.hunter_master.desc")),
        new("centurion",          Loc.Get("achievement.centurion.name"),          Loc.Get("achievement.centurion.desc")),
        new("marathon",           Loc.Get("achievement.marathon.name"),           Loc.Get("achievement.marathon.desc")),
        new("dedicated",          Loc.Get("achievement.dedicated.name"),          Loc.Get("achievement.dedicated.desc")),
        new("hard_winner",        Loc.Get("achievement.hard_winner.name"),        Loc.Get("achievement.hard_winner.desc")),
        new("perfect_home",       Loc.Get("achievement.perfect_home.name"),       Loc.Get("achievement.perfect_home.desc")),
        new("triple_hard",        Loc.Get("achievement.triple_hard.name"),        Loc.Get("achievement.triple_hard.desc")),
        new("all_personalities",  Loc.Get("achievement.all_personalities.name"),  Loc.Get("achievement.all_personalities.desc")),
        new("personality_master", Loc.Get("achievement.personality_master.name"), Loc.Get("achievement.personality_master.desc")),
    };

    private static string FilePath => UserDataPaths.Get("achievements.json");

    public static Achievements Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Achievements();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<Achievements>(json) ?? new Achievements();
        }
        catch { return new Achievements(); }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public bool IsUnlocked(string id) => UnlockedIds.Contains(id);

    /// <summary>Tente de débloquer un succès. Retourne le Achievement si nouvellement débloqué.</summary>
    public Achievement? Unlock(string id)
    {
        if (IsUnlocked(id)) return null;
        var ach = All.FirstOrDefault(a => a.Id == id);
        if (ach == null) return null;
        UnlockedIds.Add(id);
        Save();
        return ach;
    }

    public string Summary()
    {
        var unlocked = UnlockedIds.Count;
        var total = All.Count;
        var lines = new List<string> { Loc.Format("achievement.summary_count", unlocked, total) };
        foreach (var a in All)
        {
            var status = IsUnlocked(a.Id) ? Loc.Get("achievement.status_unlocked") : Loc.Get("achievement.status_locked");
            lines.Add(Loc.Format("achievement.summary_entry", status, a.Name, a.Description));
        }
        return string.Join(". ", lines);
    }
}
