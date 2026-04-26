using Parcheesi.Core.Localization;

namespace Parcheesi.Core;

public enum PieceStatus
{
    Base,   // Au départ, dans la base
    Ring,   // Sur l'anneau
    Lane,   // Dans le couloir final
    Home,   // Rentré à la maison
}

public class Piece
{
    public int Id { get; }
    public PlayerColor Color { get; }
    public PieceStatus Status { get; set; } = PieceStatus.Base;
    /// <summary>
    /// Position : indice 0..67 sur l'anneau si Status == Ring,
    /// indice 0..HomeLaneSize-2 dans le couloir si Status == Lane,
    /// null sinon (Base ou Home).
    /// </summary>
    public int? Position { get; set; }

    public Piece(int id, PlayerColor color)
    {
        Id = id;
        Color = color;
    }

    /// <summary>Score de progression utilisé pour identifier le pion le plus avancé.</summary>
    public int ProgressScore() => Status switch
    {
        PieceStatus.Base => -1,
        PieceStatus.Home => 1000,
        PieceStatus.Lane => 100 + (Position ?? 0),
        PieceStatus.Ring => BoardLayout.RingProgress(Color, Position ?? 0),
        _ => 0,
    };

    /// <summary>Description localisée pour annonce vocale / texte.</summary>
    public string Describe()
    {
        return Status switch
        {
            PieceStatus.Base => Loc.Format("piece.describe_base", Id),
            PieceStatus.Home => Loc.Format("piece.describe_home", Id),
            PieceStatus.Lane => DescribeLane(),
            PieceStatus.Ring => DescribeRing(),
            _ => Loc.Format("piece.describe_unknown", Id),
        };

        string DescribeLane()
        {
            var left = BoardLayout.HomeLaneSize - 1 - (Position ?? 0);
            return Loc.Format(left > 1 ? "piece.describe_lane_plural" : "piece.describe_lane_singular",
                              Id, left);
        }

        string DescribeRing()
        {
            var pos = Position ?? 0;
            var progress = BoardLayout.RingProgress(Color, pos);
            return Loc.Format(BoardLayout.IsSafe(pos) ? "piece.describe_ring_safe" : "piece.describe_ring",
                              Id, pos, progress);
        }
    }
}
