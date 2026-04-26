using Parcheesi.Core;

namespace Parcheesi.App.Game;

public enum CellKind
{
    Ring,    // Case sur l'anneau
    Lane,    // Case dans le couloir final
    Base,    // Case dans la base d'un joueur (slot pour pion)
    Home,    // Case maison centrale (commune ou symbolique)
    Empty,   // Cellule vide du Grid (espace vide pour le visuel)
}

public record BoardCell(
    CellKind Kind,
    int GridRow,
    int GridCol,
    int? RingPos = null,
    int? LanePos = null,
    PlayerColor? Owner = null,
    int? BaseSlot = null);

/// <summary>
/// Disposition statique du plateau sur une grille 19×19.
/// Chaque case logique (anneau, couloir, base, maison) est associée à une coordonnée (row, col).
/// </summary>
public static class BoardLayoutData
{
    public const int GridRows = 19;
    public const int GridCols = 19;

    /// <summary>Cases de l'anneau, indexées 0..67 (correspond à BoardLayout.RingSize).</summary>
    public static readonly (int row, int col)[] RingCells;

    /// <summary>Couloir final pour chaque couleur, 7 cases (cell 0 = entrée, cell 6 = avant maison).</summary>
    public static readonly Dictionary<PlayerColor, (int row, int col)[]> LaneCells;

    /// <summary>Cases de la base : 4 slots par couleur.</summary>
    public static readonly Dictionary<PlayerColor, (int row, int col)[]> BaseCells;

    /// <summary>Position de la case maison centrale.</summary>
    public static readonly (int row, int col) HomeCell = (9, 9);

    static BoardLayoutData()
    {
        RingCells = BuildRing();
        LaneCells = new()
        {
            // Couloir Rouge : colonne du milieu du bras sud, du haut (proche centre) vers le bas
            { PlayerColor.Rouge, new[] { (11,9), (12,9), (13,9), (14,9), (15,9), (16,9), (17,9) } },
            // Couloir Jaune : ligne du milieu du bras est, vers la droite
            { PlayerColor.Jaune, new[] { (9,11), (9,12), (9,13), (9,14), (9,15), (9,16), (9,17) } },
            // Couloir Bleu : colonne du milieu du bras nord, vers le haut
            { PlayerColor.Bleu,  new[] { (7,9),  (6,9),  (5,9),  (4,9),  (3,9),  (2,9),  (1,9)  } },
            // Couloir Vert : ligne du milieu du bras ouest, vers la gauche
            { PlayerColor.Vert,  new[] { (9,7),  (9,6),  (9,5),  (9,4),  (9,3),  (9,2),  (9,1)  } },
        };
        BaseCells = new()
        {
            { PlayerColor.Rouge, new[] { (12,1), (12,3), (14,1), (14,3) } }, // SW
            { PlayerColor.Jaune, new[] { (12,15),(12,17),(14,15),(14,17) } }, // SE
            { PlayerColor.Bleu,  new[] { (3,15), (3,17), (5,15), (5,17) } },  // NE
            { PlayerColor.Vert,  new[] { (3,1),  (3,3),  (5,1),  (5,3)  } },  // NW
        };
    }

    private static (int row, int col)[] BuildRing()
    {
        var cells = new List<(int, int)>();

        // Bras sud : rows 11-18, cols 8-10
        for (int r = 11; r <= 18; r++) cells.Add((r, 8));     // 0-7  : edge ouest descendant
        cells.Add((18, 9)); cells.Add((18, 10));               // 8-9  : pointe sud
        for (int r = 17; r >= 11; r--) cells.Add((r, 10));    // 10-16: edge est remontant

        // Bras est : rows 8-10, cols 11-18
        for (int c = 11; c <= 18; c++) cells.Add((10, c));    // 17-24: edge bas vers droite
        cells.Add((9, 18)); cells.Add((8, 18));                // 25-26: pointe est
        for (int c = 17; c >= 11; c--) cells.Add((8, c));     // 27-33: edge haut vers gauche

        // Bras nord : rows 0-7, cols 8-10
        for (int r = 7; r >= 0; r--) cells.Add((r, 10));      // 34-41: edge est montant
        cells.Add((0, 9)); cells.Add((0, 8));                  // 42-43: pointe nord
        for (int r = 1; r <= 7; r++) cells.Add((r, 8));       // 44-50: edge ouest descendant

        // Bras ouest : rows 8-10, cols 0-7
        for (int c = 7; c >= 0; c--) cells.Add((8, c));       // 51-58: edge haut vers gauche
        cells.Add((9, 0)); cells.Add((10, 0));                 // 59-60: pointe ouest
        for (int c = 1; c <= 7; c++) cells.Add((10, c));      // 61-67: edge bas vers droite

        if (cells.Count != 68)
            throw new InvalidOperationException($"Ring construction error: {cells.Count} cells, expected 68");

        return cells.ToArray();
    }

    /// <summary>Retourne le panoramique stéréo [-1, +1] correspondant à la position X d'une case.</summary>
    public static float StereoPan(int gridCol)
    {
        var center = (GridCols - 1) / 2.0f;
        var pan = (gridCol - center) / center;
        return Math.Clamp(pan, -1f, 1f);
    }
}
