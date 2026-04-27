using System.IO;
using System.Text.Json;
using Parcheesi.Core.Localization;

namespace Parcheesi.App;

/// <summary>
/// Statistiques persistantes : suivies entre les parties, sauvées dans stats.json.
/// </summary>
public class GameStats
{
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int CapturesMade { get; set; }
    public int CapturesReceived { get; set; }
    public int PiecesBroughtHome { get; set; }
    /// <summary>
    /// Cumul des pions ramenés à la maison uniquement sur les parties perdues.
    /// Permet d'afficher une moyenne de progression même en défaite (un nouveau joueur
    /// qui perd 10 fois mais améliore sa moyenne de 1 à 3 pions saura qu'il progresse).
    /// </summary>
    public int PiecesHomeOnDefeats { get; set; }
    public int? FewestTurnsToWin { get; set; }
    public int TotalTurnsPlayed { get; set; }
    public DateTime? LastPlayed { get; set; }

    // Stats par niveau de difficulté
    public int GamesEasy { get; set; }
    public int WinsEasy { get; set; }
    public int GamesMedium { get; set; }
    public int WinsMedium { get; set; }
    public int GamesHard { get; set; }
    public int WinsHard { get; set; }

    // Stats par personnalité affrontée (incrémentées à chaque partie où l'IA en question est présente)
    public int GamesVsAggressive { get; set; }
    public int WinsVsAggressive { get; set; }
    public int GamesVsPrudent { get; set; }
    public int WinsVsPrudent { get; set; }
    public int GamesVsCoureur { get; set; }
    public int WinsVsCoureur { get; set; }

    public double WinRate => GamesPlayed == 0 ? 0 : (double)GamesWon / GamesPlayed;
    public double AverageTurnsPerGame => GamesPlayed == 0 ? 0 : (double)TotalTurnsPlayed / GamesPlayed;
    public double AveragePiecesHomeOnDefeats => GamesLost == 0 ? 0 : (double)PiecesHomeOnDefeats / GamesLost;

    private static string FilePath => UserDataPaths.Get("stats.json");

    public static GameStats Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new GameStats();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<GameStats>(json) ?? new GameStats();
        }
        catch { return new GameStats(); }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { /* silencieux */ }
    }

    public string Summary()
    {
        if (GamesPlayed == 0) return Loc.Get("stats.no_games");
        var winRatePct = $"{WinRate * 100:F0}";
        var avgTurns = $"{AverageTurnsPerGame:F1}";
        var parts = new List<string>
        {
            Loc.Format("stats.games_played", GamesPlayed),
            Loc.Format("stats.wins", GamesWon, winRatePct),
            Loc.Format("stats.losses", GamesLost),
            Loc.Format("stats.captures_made", CapturesMade),
            Loc.Format("stats.captures_received", CapturesReceived),
            Loc.Format("stats.pieces_home", PiecesBroughtHome),
            Loc.Format("stats.average_turns", avgTurns),
        };
        if (FewestTurnsToWin.HasValue) parts.Add(Loc.Format("stats.best_record", FewestTurnsToWin));
        if (GamesLost > 0)
            parts.Add(Loc.Format("stats.avg_pieces_home_defeats", $"{AveragePiecesHomeOnDefeats:F1}"));

        // Détail par niveau de difficulté
        var byDifficulty = new List<string>();
        if (GamesEasy > 0)   byDifficulty.Add(Loc.Format("stats.by_difficulty_easy", WinsEasy, GamesEasy));
        if (GamesMedium > 0) byDifficulty.Add(Loc.Format("stats.by_difficulty_medium", WinsMedium, GamesMedium));
        if (GamesHard > 0)   byDifficulty.Add(Loc.Format("stats.by_difficulty_hard", WinsHard, GamesHard));
        if (byDifficulty.Count > 0)
            parts.Add(Loc.Format("stats.by_difficulty_label", string.Join(", ", byDifficulty)));

        // Détail par personnalité affrontée
        var byPersonality = new List<string>();
        if (GamesVsAggressive > 0) byPersonality.Add(Loc.Format("stats.vs_aggressive", WinsVsAggressive, GamesVsAggressive));
        if (GamesVsPrudent > 0)    byPersonality.Add(Loc.Format("stats.vs_prudent", WinsVsPrudent, GamesVsPrudent));
        if (GamesVsCoureur > 0)    byPersonality.Add(Loc.Format("stats.vs_coureur", WinsVsCoureur, GamesVsCoureur));
        if (byPersonality.Count > 0)
            parts.Add(Loc.Format("stats.by_personality_label", string.Join(", ", byPersonality)));

        if (LastPlayed.HasValue) parts.Add(Loc.Format("stats.last_played", LastPlayed.Value.ToString("dd MMMM yyyy")));
        return string.Join(". ", parts) + ".";
    }
}
