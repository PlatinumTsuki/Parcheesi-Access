using Parcheesi.Core.Localization;

namespace Parcheesi.Core;

public enum AIPersonality
{
    Standard,    // Comportement neutre, équilibré
    Aggressive,  // Capture à tout prix, prend des risques
    Prudent,     // Préfère les cases sûres, évite les risques
    Coureur,     // Fonce vers la maison, ignore les captures
}

public static class AIPersonalityExtensions
{
    public static string DisplayLabel(this AIPersonality p) => p switch
    {
        AIPersonality.Aggressive => Loc.Get("personality.aggressive_short"),
        AIPersonality.Prudent    => Loc.Get("personality.prudent_short"),
        AIPersonality.Coureur    => Loc.Get("personality.coureur_short"),
        _ => Loc.Get("personality.standard_short"),
    };

    public static string Description(this AIPersonality p) => p switch
    {
        AIPersonality.Aggressive => Loc.Get("personality.aggressive_desc"),
        AIPersonality.Prudent    => Loc.Get("personality.prudent_desc"),
        AIPersonality.Coureur    => Loc.Get("personality.coureur_desc"),
        _ => Loc.Get("personality.standard_desc"),
    };
}
