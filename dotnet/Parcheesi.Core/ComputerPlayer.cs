namespace Parcheesi.Core;

public enum AIDifficulty
{
    Facile,    // Aléatoire parmi les coups légaux
    Moyen,     // Stratégie gloutonne avec heuristique
    Difficile, // Recherche 2 coups d'avance, anticipe les captures
}

public static class ComputerPlayer
{
    public record AIAction(Piece Piece, DiceUsage Usage, int Steps);

    private static readonly Random _rng = new();

    public static AIAction? DecideNextAction(Game game, AIDifficulty difficulty = AIDifficulty.Moyen)
    {
        var options = EnumerateOptions(game);
        if (options.Count == 0) return null;

        var personality = game.Current.Personality;
        return difficulty switch
        {
            AIDifficulty.Facile    => PickWeightedRandom(game, options, personality),
            AIDifficulty.Moyen     => PickGreedy(game, options, personality),
            AIDifficulty.Difficile => PickWithLookahead(game, options, personality),
            _ => PickGreedy(game, options, personality),
        };
    }

    /// <summary>
    /// Niveau Facile : tirage AU HASARD pondéré par la personnalité.
    /// Tous les coups restent possibles (l'IA reste forgiving), mais les coups préférés
    /// par la personnalité ont une probabilité plus élevée d'être choisis.
    /// Ainsi L'Aggressive Facile capture plus souvent qu'un coup neutre, sans devenir mécanique.
    /// </summary>
    private static AIAction PickWeightedRandom(Game game, List<AIAction> options, AIPersonality personality)
    {
        if (personality == AIPersonality.Standard)
            return options[_rng.Next(options.Count)];

        var scored = options.Select(o => (Option: o, Score: ScoreOption(game, o, personality))).ToList();
        var minScore = scored.Min(x => x.Score);

        // Poids = 1 (baseline) + écart au pire score / 100. Le meilleur coup a un poids
        // ~5x plus élevé que le pire, donc bias léger sans dominer le hasard.
        var weights = scored.Select(x => 1.0 + Math.Max(0, x.Score - minScore) / 100.0).ToList();
        var total = weights.Sum();
        var roll = _rng.NextDouble() * total;
        double acc = 0;
        for (int i = 0; i < scored.Count; i++)
        {
            acc += weights[i];
            if (roll <= acc) return scored[i].Option;
        }
        return scored[^1].Option;
    }

    private static AIAction PickGreedy(Game game, List<AIAction> options, AIPersonality personality)
    {
        return options
            .Select(o => (Option: o, Score: ScoreOption(game, o, personality)))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Option.Steps)
            .First().Option;
    }

    /// <summary>
    /// Difficile : pour chaque coup possible, simule l'application + évalue
    /// le risque d'être capturé en retour par les adversaires au tour suivant.
    /// Choisit le coup qui maximise (gain immédiat - risque de représailles).
    /// </summary>
    private static AIAction PickWithLookahead(Game game, List<AIAction> options, AIPersonality personality)
    {
        AIAction? best = null;
        int bestScore = int.MinValue;
        foreach (var opt in options)
        {
            var immediate = ScoreOption(game, opt, personality);
            var risk = EstimateCounterCaptureRisk(game, opt);
            // La Prudente déteste le risque, l'Aggressive s'en moque presque
            var riskMultiplier = personality switch
            {
                AIPersonality.Prudent    => 2.0,
                AIPersonality.Aggressive => 0.4,
                AIPersonality.Coureur    => 1.0,
                _ => 1.0,
            };
            var total = immediate - (int)(risk * riskMultiplier);
            if (total > bestScore) { bestScore = total; best = opt; }
        }
        return best ?? options[0];
    }

    /// <summary>
    /// Estime le risque qu'un adversaire capture notre pion au prochain tour
    /// après qu'on ait appliqué ce coup. Plus le risque est élevé, plus le score baisse.
    /// </summary>
    private static int EstimateCounterCaptureRisk(Game game, AIAction action)
    {
        var preview = game.Preview(action.Piece, action.Steps);
        if (!preview.Ok || !preview.NewPosition.HasValue) return 0;
        if (preview.NewStatus != PieceStatus.Ring) return 0;
        var landingPos = preview.NewPosition.Value;
        if (BoardLayout.IsSafe(landingPos)) return 0; // pas de risque sur case sûre

        int risk = 0;
        var us = game.Current;
        foreach (var opp in game.Players)
        {
            if (opp == us) continue;
            foreach (var piece in opp.Pieces)
            {
                if (piece.Status != PieceStatus.Ring || !piece.Position.HasValue) continue;
                var dist = ((landingPos - piece.Position.Value) + BoardLayout.RingSize) % BoardLayout.RingSize;
                // Distance accessible avec un dé classique 1-12 (1 ou 2 dés)
                if (dist >= 1 && dist <= 12)
                {
                    // Probabilité approximative qu'un seul lancer atteigne pile ce nombre
                    // Plus la distance est dans la "zone optimale" (5-9), plus le risque est haut.
                    int p = dist switch
                    {
                        >= 5 and <= 9 => 30,
                        >= 2 and <= 4 => 18,
                        >= 10 and <= 12 => 12,
                        _ => 6,
                    };
                    risk += p;
                }
            }
        }
        return risk;
    }

    private static List<AIAction> EnumerateOptions(Game game)
    {
        var list = new List<AIAction>();
        var player = game.Current;
        foreach (var piece in player.Pieces)
        {
            if (game.Bonus > 0)
            {
                if (game.Preview(piece, game.Bonus).Ok)
                    list.Add(new AIAction(piece, DiceUsage.Bonus, game.Bonus));
                continue;
            }
            if (!game.Die1Used && game.Die1.HasValue && game.Preview(piece, game.Die1.Value).Ok)
                list.Add(new AIAction(piece, DiceUsage.Die1, game.Die1.Value));
            if (!game.Die2Used && game.Die2.HasValue && game.Preview(piece, game.Die2.Value).Ok)
                list.Add(new AIAction(piece, DiceUsage.Die2, game.Die2.Value));
            if (!game.Die1Used && !game.Die2Used && game.Die1.HasValue && game.Die2.HasValue)
            {
                var sum = game.Die1.Value + game.Die2.Value;
                if (game.Preview(piece, sum).Ok)
                    list.Add(new AIAction(piece, DiceUsage.Sum, sum));
            }
        }
        return list;
    }

    private static int ScoreOption(Game game, AIAction action, AIPersonality personality)
    {
        var preview = game.Preview(action.Piece, action.Steps);
        int score = 0;

        // Poids de base par personnalité
        var captureWeight = personality switch
        {
            AIPersonality.Aggressive => 400,
            AIPersonality.Prudent    => 100,
            AIPersonality.Coureur    => 50,
            _ => 200,
        };
        var homeWeight = personality switch
        {
            AIPersonality.Coureur    => 250,
            AIPersonality.Aggressive => 80,
            _ => 120,
        };
        var advanceWeight = personality switch
        {
            AIPersonality.Coureur    => 5,
            AIPersonality.Prudent    => 1,
            _ => 2,
        };
        var safeBonus = personality switch
        {
            AIPersonality.Prudent    => 25,
            AIPersonality.Aggressive => 2,
            _ => 8,
        };
        var leaveSafePenalty = personality switch
        {
            AIPersonality.Prudent    => 35,
            AIPersonality.Aggressive => 3,
            _ => 15,
        };

        if (preview.Captures != null) score += captureWeight;
        if (preview.ReachedHome) score += homeWeight;
        if (action.Piece.Status == PieceStatus.Base) score += 60;
        if (preview.NewStatus == PieceStatus.Lane) score += 40;
        score += action.Steps * advanceWeight;
        score += action.Piece.ProgressScore() / 5;

        if (action.Piece.Status == PieceStatus.Ring
            && BoardLayout.IsSafe(action.Piece.Position!.Value)
            && preview.NewStatus == PieceStatus.Ring
            && preview.NewPosition.HasValue
            && !BoardLayout.IsSafe(preview.NewPosition.Value))
        {
            score -= leaveSafePenalty;
        }
        if (preview.NewStatus == PieceStatus.Ring
            && preview.NewPosition.HasValue
            && BoardLayout.IsSafe(preview.NewPosition.Value)
            && action.Piece.Status != PieceStatus.Base)
        {
            score += safeBonus;
        }
        return score;
    }
}
