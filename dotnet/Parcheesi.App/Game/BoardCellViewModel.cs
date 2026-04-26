using System.ComponentModel;
using System.Runtime.CompilerServices;
using Parcheesi.Core;

namespace Parcheesi.App.Game;

/// <summary>
/// ViewModel d'une case du plateau. Notifie l'UI (et UI Automation) quand
/// son contenu change pour que NVDA annonce automatiquement les modifications.
/// </summary>
public class BoardCellViewModel : INotifyPropertyChanged
{
    public BoardCell Cell { get; }

    private string _occupantSummary = "vide";
    public string OccupantSummary
    {
        get => _occupantSummary;
        set { if (_occupantSummary != value) { _occupantSummary = value; OnPropertyChanged(); OnPropertyChanged(nameof(AutomationName)); } }
    }

    public string KindLabel => Cell.Kind switch
    {
        CellKind.Ring => Cell.RingPos.HasValue && Parcheesi.Core.BoardLayout.IsSafe(Cell.RingPos.Value)
            ? $"Case {Cell.RingPos} sûre"
            : $"Case {Cell.RingPos}",
        CellKind.Lane => $"Couloir {Cell.Owner!.Value.Label()} case {Cell.LanePos! + 1} sur 7",
        CellKind.Base => $"Base {Cell.Owner!.Value.Label()} slot {Cell.BaseSlot! + 1}",
        CellKind.Home => "Maison centrale",
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
