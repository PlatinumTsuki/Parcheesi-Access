using Parcheesi.Core;

namespace Parcheesi.App.ViewModels;

/// <summary>
/// Événements enregistrés pendant une partie pour le replay accéléré (touche V sur l'écran de fin).
/// Chaque type contient ce qu'il faut pour rejouer l'audio et l'annonce courte associée
/// — pas l'état complet du jeu, juste les détails sensibles à l'oreille.
/// </summary>
public abstract record ReplayEvent;

/// <summary>Lancer de dés.</summary>
public sealed record ReplayDiceRoll(
    string PlayerLabel,
    int D1,
    int D2,
    bool IsDouble,
    bool HumanLeavingBaseHinted // un 5 a été obtenu et au moins un pion en base
) : ReplayEvent;

/// <summary>Coup appliqué (sortie de base, anneau, couloir, ou rentrée).</summary>
public sealed record ReplayMove(
    string PlayerLabel,
    PlayerColor Color,
    int PieceId,
    int Steps,
    bool WasInBase,
    bool ReachedHome,
    bool Captured,
    bool EnteredLane,
    bool EnteredSafe,
    float StartPan,
    float EndPan,
    string Announce // texte court : "Pion 1 sur case 13" ou "Rouge capture le pion bleu 2"
) : ReplayEvent;

/// <summary>Lancer sans coup légal possible : on annonce et on passe.</summary>
public sealed record ReplayNoLegalMove(string PlayerLabel, int D1, int D2) : ReplayEvent;

/// <summary>Tour passé manuellement (touche T) ou triple double pénalité.</summary>
public sealed record ReplayTurnPassed(string PlayerLabel, string? PenaltyMessage) : ReplayEvent;

/// <summary>Transition vers le joueur suivant (ou rejouer si double).</summary>
public sealed record ReplayTurnChange(string NextPlayerLabel, bool Rerolled) : ReplayEvent;

/// <summary>Fin de partie : vainqueur, ambiance.</summary>
public sealed record ReplayGameEnd(
    string WinnerLabel,
    bool HumanWon,
    string FullEndAnnounce
) : ReplayEvent;
