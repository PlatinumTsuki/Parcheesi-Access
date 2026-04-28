using Parcheesi.Core.Localization;

namespace Parcheesi.Core;

/// <summary>
/// Moteur de partie. Toute la logique des règles tient ici, sans dépendance UI.
/// </summary>
public class Game
{
    private readonly Random _rng;
    private Queue<(int d1, int d2)>? _forcedDice;

    public List<Player> Players { get; }
    public int CurrentIndex { get; private set; }
    public Player Current => Players[CurrentIndex];

    public int? Die1 { get; private set; }
    public int? Die2 { get; private set; }
    public bool Die1Used { get; private set; }
    public bool Die2Used { get; private set; }

    /// <summary>Cases de bonus restantes (capture +20, retour maison +10).</summary>
    public int Bonus { get; private set; }

    /// <summary>Doubles consécutifs (3 = pénalité).</summary>
    public int DoubleStreak { get; private set; }

    public bool AwaitingRoll { get; private set; } = true;
    public bool Finished { get; private set; }
    public PlayerColor? Winner { get; private set; }
    public List<PlayerColor> Rankings { get; } = new();

    public Game(int numPlayers, Random? rng = null)
        : this(Enumerable.Repeat(false, numPlayers).ToArray(), rng) { }

    public Game(bool[] isComputerPerSlot, Random? rng = null)
    {
        var numPlayers = isComputerPerSlot.Length;
        if (numPlayers < 2 || numPlayers > 4)
            throw new ArgumentOutOfRangeException(nameof(isComputerPerSlot));
        _rng = rng ?? new Random();
        Players = Enumerable.Range(0, numPlayers)
                            .Select(i => new Player((PlayerColor)i, isComputerPerSlot[i]))
                            .ToList();
    }

    public (int d1, int d2, bool isDouble) RollDice()
    {
        if (!AwaitingRoll)
            throw new InvalidOperationException("Les dés ont déjà été lancés ce tour-ci.");

        int d1, d2;
        if (_forcedDice != null && _forcedDice.Count > 0)
        {
            var f = _forcedDice.Dequeue();
            d1 = f.d1;
            d2 = f.d2;
        }
        else
        {
            d1 = _rng.Next(1, 7);
            d2 = _rng.Next(1, 7);
        }
        Die1 = d1;
        Die2 = d2;
        Die1Used = false;
        Die2Used = false;
        AwaitingRoll = false;
        var isDouble = d1 == d2;
        if (isDouble) DoubleStreak++; else DoubleStreak = 0;
        return (d1, d2, isDouble);
    }

    /// <summary>Tutorial-only: queue dice values that override the random RNG on subsequent rolls.</summary>
    public void EnqueueForcedDice(IEnumerable<(int d1, int d2)> rolls)
    {
        _forcedDice ??= new Queue<(int, int)>();
        foreach (var r in rolls) _forcedDice.Enqueue(r);
    }

    /// <summary>Tutorial-only: clear any pending forced dice.</summary>
    public void ClearForcedDice() => _forcedDice = null;

    public Occupant? FindOccupant(int ringPos)
    {
        foreach (var player in Players)
            foreach (var piece in player.Pieces)
                if (piece.Status == PieceStatus.Ring && piece.Position == ringPos)
                    return new Occupant(player, piece);
        return null;
    }

    public MovePreview Preview(Piece piece, int steps)
    {
        if (piece == null) return new MovePreview(false, Loc.Get("preview_reason.piece_not_found"));
        if (steps <= 0) return new MovePreview(false, Loc.Get("preview_reason.invalid_distance"));

        var owner = Players.FirstOrDefault(p => p.Pieces.Contains(piece));
        if (owner == null) return new MovePreview(false, Loc.Get("preview_reason.no_owner"));

        switch (piece.Status)
        {
            case PieceStatus.Home:
                return new MovePreview(false, Loc.Get("preview_reason.already_home"));

            case PieceStatus.Base:
                if (steps != 5)
                    return new MovePreview(false, Loc.Get("preview_reason.need_five"));
                var startPos = BoardLayout.StartPos(owner.Color);
                var blocker = FindOccupant(startPos);
                if (blocker != null && blocker.Player.Color == owner.Color)
                {
                    // Sa propre case de départ : on autorise (empile non géré pour v1).
                }
                else if (blocker != null && BoardLayout.IsSafe(startPos))
                {
                    return new MovePreview(false, Loc.Get("preview_reason.start_blocked_safe"));
                }
                return new MovePreview(true,
                    NewStatus: PieceStatus.Ring,
                    NewPosition: startPos,
                    Captures: blocker != null && blocker.Player.Color != owner.Color ? blocker : null);

            case PieceStatus.Ring:
                return PreviewRingMove(owner, piece, steps);

            case PieceStatus.Lane:
                return PreviewLaneMove(piece, steps);

            default:
                return new MovePreview(false, Loc.Get("preview_reason.unknown_state"));
        }
    }

    private MovePreview PreviewRingMove(Player owner, Piece piece, int steps)
    {
        var pos = piece.Position!.Value;
        var distToHome = BoardLayout.RingDistanceToHomeEntry(owner.Color, pos);

        if (steps <= distToHome)
        {
            // Reste sur l'anneau.
            var newPos = (pos + steps) % BoardLayout.RingSize;
            var occupant = FindOccupant(newPos);
            if (occupant != null && occupant.Piece == piece)
                occupant = null;
            if (occupant != null && occupant.Player.Color == owner.Color)
                return new MovePreview(false, Loc.Get("preview_reason.own_piece_blocking"));
            if (occupant != null && BoardLayout.IsSafe(newPos))
                return new MovePreview(false, Loc.Get("preview_reason.safe_opponent_no_capture"));
            return new MovePreview(true,
                NewStatus: PieceStatus.Ring,
                NewPosition: newPos,
                Captures: occupant != null && occupant.Player.Color != owner.Color ? occupant : null);
        }

        // On entre dans le couloir.
        var intoLane = steps - distToHome - 1;
        if (intoLane > BoardLayout.HomeLaneSize - 1)
            return new MovePreview(false, Loc.Get("preview_reason.overshoot_home"));
        if (intoLane == BoardLayout.HomeLaneSize - 1)
            return new MovePreview(true, NewStatus: PieceStatus.Home, NewPosition: null, ReachedHome: true);
        return new MovePreview(true, NewStatus: PieceStatus.Lane, NewPosition: intoLane);
    }

    private MovePreview PreviewLaneMove(Piece piece, int steps)
    {
        var newLane = piece.Position!.Value + steps;
        if (newLane > BoardLayout.HomeLaneSize - 1)
            return new MovePreview(false, Loc.Get("preview_reason.overshoot_home"));
        if (newLane == BoardLayout.HomeLaneSize - 1)
            return new MovePreview(true, NewStatus: PieceStatus.Home, NewPosition: null, ReachedHome: true);
        return new MovePreview(true, NewStatus: PieceStatus.Lane, NewPosition: newLane);
    }

    public MoveResult ApplyMove(Piece piece, int steps)
    {
        var preview = Preview(piece, steps);
        if (!preview.Ok) return new MoveResult(false, Reason: preview.Reason);

        var owner = Players.First(p => p.Pieces.Contains(piece));
        string? captureMsg = null, bonusMsg = null;
        int bonusGained = 0;

        if (preview.Captures != null)
        {
            var cap = preview.Captures;
            cap.Piece.Status = PieceStatus.Base;
            cap.Piece.Position = null;
            captureMsg = Loc.Format("game.capture_message", cap.Piece.Id, cap.Player.ColorLabel);
            Bonus += 20;
            bonusGained += 20;
        }

        piece.Status = preview.NewStatus!.Value;
        piece.Position = preview.NewPosition;

        if (preview.ReachedHome)
        {
            bonusMsg = Loc.Format("game.home_arrival_message", owner.Label, piece.Id);
            Bonus += 10;
            bonusGained += 10;

            if (owner.HasFinished())
            {
                Rankings.Add(owner.Color);
                Winner ??= owner.Color;
                var stillPlaying = Players.Where(p => !Rankings.Contains(p.Color)).ToList();
                if (stillPlaying.Count <= 1)
                {
                    if (stillPlaying.Count == 1)
                        Rankings.Add(stillPlaying[0].Color);
                    Finished = true;
                }
            }
        }

        return new MoveResult(true, CaptureMessage: captureMsg, BonusMessage: bonusMsg,
                              ReachedHome: preview.ReachedHome, BonusGained: bonusGained);
    }

    public void ConsumeDie(DiceUsage which)
    {
        switch (which)
        {
            case DiceUsage.Die1: Die1Used = true; break;
            case DiceUsage.Die2: Die2Used = true; break;
            case DiceUsage.Sum:  Die1Used = true; Die2Used = true; break;
            case DiceUsage.Bonus: Bonus = 0; break;
        }
    }

    public bool TurnIsOver()
    {
        if (Bonus > 0) return false;
        return Die1Used && Die2Used;
    }

    public int[] AvailableSteps()
    {
        var list = new List<int>();
        if (Bonus > 0) list.Add(Bonus);
        if (!Die1Used && Die1.HasValue) list.Add(Die1.Value);
        if (!Die2Used && Die2.HasValue) list.Add(Die2.Value);
        if (!Die1Used && !Die2Used && Die1.HasValue && Die2.HasValue)
            list.Add(Die1.Value + Die2.Value);
        return list.Distinct().ToArray();
    }

    public bool HasAnyLegalMove()
    {
        var steps = AvailableSteps();
        foreach (var piece in Current.Pieces)
            foreach (var s in steps)
                if (Preview(piece, s).Ok) return true;
        return false;
    }

    public class TurnTransition
    {
        public bool Rerolled { get; init; }
        public string? PenaltyMessage { get; init; }
    }

    public TurnTransition NextTurn()
    {
        string? penalty = null;

        // Triple double = pénalité : pion le plus avancé retourne à la base.
        if (DoubleStreak >= 3)
        {
            var ranked = Current.Pieces
                .Where(p => p.Status != PieceStatus.Base && p.Status != PieceStatus.Home)
                .OrderByDescending(p => p.ProgressScore())
                .ToList();
            if (ranked.Count > 0)
            {
                var top = ranked[0];
                top.Status = PieceStatus.Base;
                top.Position = null;
                penalty = Loc.Format("game.triple_double_penalty", Current.Label, top.Id);
            }
            DoubleStreak = 0;
        }
        else
        {
            // Si on a fait un double et utilisé tous les dés (sans bonus restant), on rejoue.
            var wasDouble = Die1.HasValue && Die2.HasValue && Die1 == Die2;
            if (wasDouble && Die1Used && Die2Used && Bonus == 0)
            {
                ResetDice();
                AwaitingRoll = true;
                return new TurnTransition { Rerolled = true };
            }
        }

        // Joueur suivant (saute ceux qui ont déjà fini).
        do
        {
            CurrentIndex = (CurrentIndex + 1) % Players.Count;
        } while (Rankings.Contains(Players[CurrentIndex].Color));

        ResetDice();
        Bonus = 0;
        DoubleStreak = 0;
        AwaitingRoll = true;
        return new TurnTransition { PenaltyMessage = penalty };
    }

    private void ResetDice()
    {
        Die1 = null;
        Die2 = null;
        Die1Used = false;
        Die2Used = false;
    }

    /// <summary>Photographie l'état actuel pour sérialisation.</summary>
    public GameSnapshot ToSnapshot(AIDifficulty difficulty = AIDifficulty.Moyen) => new GameSnapshot
    {
        Players = Players.Select(p => new PlayerSnapshot
        {
            Color = p.Color,
            IsComputer = p.IsComputer,
            Pieces = p.Pieces.Select(piece => new PieceSnapshot
            {
                Id = piece.Id,
                Status = piece.Status,
                Position = piece.Position,
            }).ToList(),
        }).ToList(),
        CurrentIndex = CurrentIndex,
        Die1 = Die1,
        Die2 = Die2,
        Die1Used = Die1Used,
        Die2Used = Die2Used,
        Bonus = Bonus,
        DoubleStreak = DoubleStreak,
        AwaitingRoll = AwaitingRoll,
        Finished = Finished,
        Winner = Winner,
        Rankings = Rankings.ToList(),
        Difficulty = difficulty,
        SavedAt = DateTime.Now,
    };

    /// <summary>Reconstitue une partie à partir d'une photographie précédemment sérialisée.</summary>
    public static Game FromSnapshot(GameSnapshot snap)
    {
        var game = new Game(snap.Players.Select(p => p.IsComputer).ToArray());
        game.CurrentIndex = snap.CurrentIndex;
        game.Die1 = snap.Die1;
        game.Die2 = snap.Die2;
        game.Die1Used = snap.Die1Used;
        game.Die2Used = snap.Die2Used;
        game.Bonus = snap.Bonus;
        game.DoubleStreak = snap.DoubleStreak;
        game.AwaitingRoll = snap.AwaitingRoll;
        game.Finished = snap.Finished;
        game.Winner = snap.Winner;
        game.Rankings.Clear();
        foreach (var c in snap.Rankings) game.Rankings.Add(c);
        for (int i = 0; i < snap.Players.Count; i++)
        {
            var player = game.Players[i];
            for (int j = 0; j < snap.Players[i].Pieces.Count && j < player.Pieces.Count; j++)
            {
                player.Pieces[j].Status = snap.Players[i].Pieces[j].Status;
                player.Pieces[j].Position = snap.Players[i].Pieces[j].Position;
            }
        }
        return game;
    }
}
