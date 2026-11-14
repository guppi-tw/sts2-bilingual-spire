using Godot;

namespace BilingualSpire.UI;

internal static class ModStyles
{
    internal static readonly Color Surface = new("202832");
    private static readonly Color Border = new("81745d");

    internal static StyleBoxFlat Panel(Color? background = null) => new()
    {
        BgColor = background ?? Surface, BorderColor = Border,
        BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8,
        ShadowColor = new Color(0, 0, 0, 0.45f), ShadowSize = 6, ShadowOffset = new Vector2(-2, 3)
    };
}
