namespace Parcheesi.Core;

public record MovePreview(
    bool Ok,
    string? Reason = null,
    PieceStatus? NewStatus = null,
    int? NewPosition = null,
    Occupant? Captures = null,
    bool ReachedHome = false);

public record Occupant(Player Player, Piece Piece);

public record MoveResult(
    bool Ok,
    string? Reason = null,
    string? CaptureMessage = null,
    string? BonusMessage = null,
    bool ReachedHome = false,
    int BonusGained = 0);

public enum DiceUsage
{
    Die1,
    Die2,
    Sum,
    Bonus,
}
