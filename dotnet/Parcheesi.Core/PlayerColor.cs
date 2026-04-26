using Parcheesi.Core.Localization;

namespace Parcheesi.Core;

public enum PlayerColor
{
    Rouge = 0,
    Jaune = 1,
    Bleu = 2,
    Vert = 3,
}

public static class PlayerColorExtensions
{
    public static string Label(this PlayerColor c) => c switch
    {
        PlayerColor.Rouge => Loc.Get("color.rouge"),
        PlayerColor.Jaune => Loc.Get("color.jaune"),
        PlayerColor.Bleu => Loc.Get("color.bleu"),
        PlayerColor.Vert => Loc.Get("color.vert"),
        _ => c.ToString(),
    };
}
