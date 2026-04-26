using System.ComponentModel;
using System.Runtime.CompilerServices;
using Parcheesi.Core;
using Parcheesi.Core.Localization;

namespace Parcheesi.App.Game;

/// <summary>
/// ViewModel d'une case du plateau. Notifie l'UI (et UI Automation) quand
/// son contenu change pour que NVDA annonce automatiquement les modifications.
/// </summary>
public class BoardCellViewModel : INotifyPropertyChanged
{
    public BoardCell Cell { get; }

    private string _occupantSummary = Loc.Get("board.cell.empty");
    /// <summary>Description longue localisée des occupants (lue par NVDA).</summary>
    public string OccupantSummary
    {
        get => _occupantSummary;
        set { if (_occupantSummary != value) { _occupantSummary = value; OnPropertyChanged(); OnPropertyChanged(nameof(AutomationName)); } }
    }

    private string _occupantGlyph = "";
    /// <summary>Glyphe court pour l'affichage visuel (ex: "R1", "J2"). Vide si la case est vide.</summary>
    public string OccupantGlyph
    {
        get => _occupantGlyph;
        set { if (_occupantGlyph != value) { _occupantGlyph = value; OnPropertyChanged(); } }
    }

    public string KindLabel => Cell.Kind switch
    {
        CellKind.Ring => Cell.RingPos.HasValue && Parcheesi.Core.BoardLayout.IsSafe(Cell.RingPos.Value)
            ? Loc.Format("board.kind.ring_safe", Cell.RingPos)
            : Loc.Format("board.kind.ring", Cell.RingPos),
        CellKind.Lane => Loc.Format("board.kind.lane", Cell.Owner!.Value.Label(), Cell.LanePos! + 1),
        CellKind.Base => Loc.Format("board.kind.base", Cell.Owner!.Value.Label(), Cell.BaseSlot! + 1),
        CellKind.Home => Loc.Get("board.kind.home"),
        _ => "",
    };

    /// <summary>Nom complet annoncé par NVDA via UI Automation.</summary>
    public string AutomationName => $"{KindLabel}, {OccupantSummary}";

    public bool IsFocusable => Cell.Kind != CellKind.Empty;

    public BoardCellViewModel(BoardCell cell)
    {
        Cell = cell;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
}
