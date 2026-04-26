namespace Parcheesi.Audio;

public enum SoundEffect
{
    // Dés (variations choisies aléatoirement)
    DiceShake,
    DiceThrow,

    // Sélection / annonces
    PieceSelect,
    TurnChange,
    Error,

    // Déplacement de pion (un son par couleur — joué une fois par case parcourue)
    PieceMoveRouge,
    PieceMoveJaune,
    PieceMoveBleu,
    PieceMoveVert,

    // Événements de jeu
    SafeEntry,        // pion atterrit sur une case sûre
    LaneEntry,        // pion entre dans son couloir final
    BaseExit,         // pion sort de la base
    Blocked,          // mouvement bloqué (case sûre adverse, etc.)
    CanLeaveBase,     // un 5 a été obtenu et au moins un pion peut sortir
    AIThinking,       // pulsation discrète pendant que l'IA réfléchit
    Capture,          // pion adverse capturé
    HomeArrive,       // pion rentré à la maison
    Victory,          // partie gagnée

    // Timer de tour (sons dédiés)
    TimerTick,        // tic-tac d'horloge à mi-temps
    TimerWarn10s,     // alerte 10 secondes restantes
    TimerWarn5s,      // alerte 5 secondes restantes
    TimerExpired,     // gong de fin de timer

    // Pause / reprise
    Pause,            // tonalité descendante
    Resume,           // tonalité ascendante miroir

    // Urgence (adversaire à 3 pions de la victoire)
    Urgency,          // cloche grave dramatique

    // Ambiance salon victorien (gated par Settings.ImmersiveAmbience)
    FireplaceLoop,    // crépitement de cheminée
    MantelClockTick,  // tic-tac de pendulette de cheminée
    PoliteApplause,   // applaudissements polis (victoire standard)
    StandingApplause, // ovation enthousiaste (triomphe éclatant)
    ScatteredApplause,// applaudissements clairsemés (victoire au coude-à-coude)
}
