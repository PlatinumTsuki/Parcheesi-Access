namespace Parcheesi.Core;

public class Player
{
    public PlayerColor Color { get; }
    public string ColorLabel => Color.Label();
    public List<Piece> Pieces { get; }
    public bool IsComputer { get; set; }
    public AIPersonality Personality { get; set; } = AIPersonality.Standard;
    public string? CustomName { get; set; }

    /// <summary>Nom affiché : custom > personnalité IA > couleur.</summary>
    public string Label
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomName)) return CustomName;
            if (IsComputer && Personality != AIPersonality.Standard)
                return $"{Personality.DisplayLabel()} ({Color.Label()})";
            return Color.Label();
        }
    }

    public Player(PlayerColor color, bool isComputer = false)
    {
        Color = color;
        IsComputer = isComputer;
        Pieces = Enumerable.Range(1, BoardLayout.PiecesPerPlayer)
                           .Select(i => new Piece(i, color))
                           .ToList();
    }

    public bool HasFinished() => Pieces.All(p => p.Status == PieceStatus.Home);
}
