using Parcheesi.Core;
using Parcheesi.Core.Localization;

namespace Parcheesi.App.Game;

/// <summary>
/// Construit la liste des BoardCellViewModel à partir de la mise en page statique
/// et fournit des aides pour mettre à jour les cases en fonction de l'état du jeu.
/// </summary>
public class BoardModel
{
    public List<BoardCellViewModel> Cells { get; }
    private readonly Dictionary<(int row, int col), BoardCellViewModel> _byCoord;

    public BoardModel(int numPlayers)
    {
        Cells = new List<BoardCellViewModel>();

        // Cases de l'anneau (toujours toutes affichées).
        for (int i = 0; i < BoardLayoutData.RingCells.Length; i++)
        {
            var (r, c) = BoardLayoutData.RingCells[i];
            Cells.Add(new BoardCellViewModel(
                new BoardCell(CellKind.Ring, r, c, RingPos: i)));
        }

        // Couloirs et bases pour chaque joueur actif.
        var activeColors = Enumerable.Range(0, numPlayers).Cast<PlayerColor>().ToList();
        foreach (var color in activeColors)
        {
            var lane = BoardLayoutData.LaneCells[color];
            for (int i = 0; i < lane.Length; i++)
            {
                var (r, c) = lane[i];
                Cells.Add(new BoardCellViewModel(
                    new BoardCell(CellKind.Lane, r, c, LanePos: i, Owner: color)));
            }
            var bases = BoardLayoutData.BaseCells[color];
            for (int i = 0; i < bases.Length; i++)
            {
                var (r, c) = bases[i];
                Cells.Add(new BoardCellViewModel(
                    new BoardCell(CellKind.Base, r, c, BaseSlot: i, Owner: color)));
            }
        }

        // Case maison centrale (commune).
        var (hr, hc) = BoardLayoutData.HomeCell;
        Cells.Add(new BoardCellViewModel(new BoardCell(CellKind.Home, hr, hc)));

        _byCoord = Cells.ToDictionary(cv => (cv.Cell.GridRow, cv.Cell.GridCol));
    }

    public BoardCellViewModel? At(int row, int col)
        => _byCoord.TryGetValue((row, col), out var v) ? v : null;

    /// <summary>
    /// Trouve la case du plateau qui correspond à un pion donné.
    /// </summary>
    public BoardCellViewModel? FindCellForPiece(Piece piece)
    {
        foreach (var cv in Cells)
        {
            var cell = cv.Cell;
            switch (cell.Kind)
            {
                case CellKind.Ring:
                    if (piece.Status == PieceStatus.Ring && piece.Position == cell.RingPos) return cv;
                    break;
                case CellKind.Lane:
                    if (piece.Status == PieceStatus.Lane && piece.Position == cell.LanePos
                        && cell.Owner == piece.Color) return cv;
                    break;
                case CellKind.Base:
                    if (piece.Status == PieceStatus.Base && cell.Owner == piece.Color
                        && cell.BaseSlot == piece.Id - 1) return cv;
                    break;
                case CellKind.Home:
                    if (piece.Status == PieceStatus.Home) return cv;
                    break;
            }
        }
        return null;
    }

    /// <summary>
    /// Met à jour le résumé d'occupation de chaque case en fonction de l'état actuel du jeu.
    /// Doit être appelée après chaque modification de l'état du jeu.
    /// </summary>
    public void RefreshOccupancy(Parcheesi.Core.Game game)
    {
        var emptyText = Loc.Get("board.cell.empty");
        var emptyBaseSlot = Loc.Get("board.cell.base_slot_empty");
        var emptyHome = Loc.Get("board.cell.home_empty");
        var separator = Loc.Get("board.cell.piece_separator");

        // 1. Réinitialise toutes les cases : texte localisé pour NVDA, glyphe vide pour le visuel.
        foreach (var cv in Cells)
        {
            cv.OccupantSummary = cv.Cell.Kind switch
            {
                CellKind.Base => emptyBaseSlot,
                CellKind.Home => emptyHome,
                _ => emptyText,
            };
            cv.OccupantGlyph = "";
        }

        // 2. Place chaque pion sur sa case.
        var homeOccupants = new List<string>();
        foreach (var player in game.Players)
        {
            foreach (var piece in player.Pieces)
            {
                var label = Loc.Format("board.cell.piece_label", player.Label, piece.Id);
                var glyph = ColorGlyphPrefix(piece.Color) + piece.Id;
                switch (piece.Status)
                {
                    case PieceStatus.Base:
                        var baseCell = Cells.FirstOrDefault(c =>
                            c.Cell.Kind == CellKind.Base
                            && c.Cell.Owner == player.Color
                            && c.Cell.BaseSlot == piece.Id - 1);
                        if (baseCell != null)
                        {
                            baseCell.OccupantSummary = label;
                            baseCell.OccupantGlyph = glyph;
                        }
                        break;
                    case PieceStatus.Ring:
                        var ringCell = Cells.FirstOrDefault(c =>
                            c.Cell.Kind == CellKind.Ring && c.Cell.RingPos == piece.Position);
                        if (ringCell != null)
                        {
                            ringCell.OccupantSummary =
                                ringCell.OccupantSummary == emptyText
                                    ? label
                                    : ringCell.OccupantSummary + separator + label;
                            ringCell.OccupantGlyph = string.IsNullOrEmpty(ringCell.OccupantGlyph)
                                ? glyph
                                : ringCell.OccupantGlyph + "+" + glyph;
                        }
                        break;
                    case PieceStatus.Lane:
                        var laneCell = Cells.FirstOrDefault(c =>
                            c.Cell.Kind == CellKind.Lane
                            && c.Cell.Owner == player.Color
                            && c.Cell.LanePos == piece.Position);
                        if (laneCell != null)
                        {
                            laneCell.OccupantSummary = label;
                            laneCell.OccupantGlyph = glyph;
                        }
                        break;
                    case PieceStatus.Home:
                        homeOccupants.Add(label);
                        break;
                }
            }
        }

        var home = Cells.FirstOrDefault(c => c.Cell.Kind == CellKind.Home);
        if (home != null)
        {
            if (homeOccupants.Count == 0)
            {
                home.OccupantSummary = emptyHome;
                home.OccupantGlyph = "";
            }
            else
            {
                var summaryKey = homeOccupants.Count == 1
                    ? "board.cell.home_summary_singular"
                    : "board.cell.home_summary_plural";
                home.OccupantSummary = Loc.Format(summaryKey, homeOccupants.Count, string.Join(", ", homeOccupants));
                home.OccupantGlyph = "★" + homeOccupants.Count;
            }
        }
    }

    private static string ColorGlyphPrefix(PlayerColor color) => color switch
    {
        PlayerColor.Rouge => "R",
        PlayerColor.Jaune => "J",
        PlayerColor.Bleu => "B",
        PlayerColor.Vert => "V",
        _ => "?",
    };
}
