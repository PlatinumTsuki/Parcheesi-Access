namespace Parcheesi.Core;

/// <summary>
/// Constantes géométriques du plateau Parcheesi : anneau de 68 cases,
/// couloir final de 8 cases (la case 7 = maison), 4 pions par couleur.
/// </summary>
public static class BoardLayout
{
    public const int RingSize = 68;
    public const int HomeLaneSize = 8;
    public const int PiecesPerPlayer = 4;

    /// <summary>Position d'entrée sur l'anneau pour chaque couleur.</summary>
    public static int StartPos(PlayerColor c) => c switch
    {
        PlayerColor.Rouge => 0,
        PlayerColor.Jaune => 17,
        PlayerColor.Bleu  => 34,
        PlayerColor.Vert  => 51,
        _ => 0,
    };

    /// <summary>Case juste avant l'entrée du couloir final pour chaque couleur.</summary>
    public static int HomeEntry(PlayerColor c) => c switch
    {
        PlayerColor.Rouge => 67,
        PlayerColor.Jaune => 16,
        PlayerColor.Bleu  => 33,
        PlayerColor.Vert  => 50,
        _ => 0,
    };

    private static readonly HashSet<int> _safeSquares = new()
    {
        // Sorties
        0, 17, 34, 51,
        // Cases sûres intermédiaires (à mi-parcours entre deux sorties)
        7, 12, 24, 29, 41, 46, 58, 63,
    };

    public static bool IsSafe(int ringPos) => _safeSquares.Contains(ringPos);

    /// <summary>Distance restante (sur l'anneau) avant l'entrée du couloir final.</summary>
    public static int RingDistanceToHomeEntry(PlayerColor c, int pos)
    {
        var entry = HomeEntry(c);
        return ((entry - pos) + RingSize) % RingSize;
    }

    /// <summary>Progression d'un pion sur l'anneau, comptée depuis sa case de sortie.</summary>
    public static int RingProgress(PlayerColor c, int pos)
    {
        var start = StartPos(c);
        return ((pos - start) + RingSize) % RingSize;
    }
}
