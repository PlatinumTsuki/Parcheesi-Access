namespace Parcheesi.Core;

/// <summary>
/// Photographie complète d'une partie en cours, sérialisable en JSON
/// pour sauvegarde et reprise.
/// </summary>
public class GameSnapshot
{
    public List<PlayerSnapshot> Players { get; set; } = new();
    public int CurrentIndex { get; set; }
    public int? Die1 { get; set; }
    public int? Die2 { get; set; }
    public bool Die1Used { get; set; }
    public bool Die2Used { get; set; }
    public int Bonus { get; set; }
    public int DoubleStreak { get; set; }
    public bool AwaitingRoll { get; set; }
    public bool Finished { get; set; }
    public PlayerColor? Winner { get; set; }
    public List<PlayerColor> Rankings { get; set; } = new();
    public AIDifficulty Difficulty { get; set; }
    public DateTime SavedAt { get; set; }
}

public class PlayerSnapshot
{
    public PlayerColor Color { get; set; }
    public bool IsComputer { get; set; }
    public List<PieceSnapshot> Pieces { get; set; } = new();
}

public class PieceSnapshot
{
    public int Id { get; set; }
    public PieceStatus Status { get; set; }
    public int? Position { get; set; }
}
