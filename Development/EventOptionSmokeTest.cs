#if DEBUG
using Godot;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace BilingualSpire.Development;

internal static class EventOptionSmokeTest
{
    internal sealed record Geometry(NEventOptionButton Button, float Height, Vector2 Pivot,
        Dictionary<NinePatchRect, (Rect2 Rect, Vector2 Pivot)> Graphics);

    internal static Geometry[] Capture(NEventLayout layout) => layout.OptionButtons.Select(button =>
        new Geometry(button, button.Size.Y, button.PivotOffset, button.GetChildren().OfType<NinePatchRect>()
            .ToDictionary(graphic => graphic, graphic => (new Rect2(graphic.Position, graphic.Size), graphic.PivotOffset)))).ToArray();

    internal static void Check(Geometry[] originals)
    {
        foreach (var original in originals)
        {
            var extra = original.Button.Size.Y - original.Height;
            Require(Math.Abs(original.Button.PivotOffset.Y - original.Pivot.Y - extra / 2) < 1, "Hover scale stays centered vertically");
            foreach (var (graphic, geometry) in original.Graphics)
            {
                var rect = new Rect2(graphic.Position, graphic.Size);
                Require(Math.Abs(rect.Size.Y - geometry.Rect.Size.Y - extra) < 1,
                    $"{graphic.Name}: highlight/shadow height follows button ({rect.Size.Y} vs {geometry.Rect.Size.Y} + {extra})");
                Require(Math.Abs(rect.Position.Y - geometry.Rect.Position.Y) < 1, $"{graphic.Name}: original top margin preserved");
                Require(Math.Abs(graphic.PivotOffset.Y - geometry.Pivot.Y - extra / 2) < 1, $"{graphic.Name}: effect pivot follows button");
            }
        }
    }

    internal static void CheckFocused(NEventOptionButton button)
    {
        var outline = button.GetNode<NinePatchRect>("Outline");
        var image = button.GetNode<NinePatchRect>("Image");
        Require(outline.Modulate.A > 0.05f, $"Native hover highlight is visible (alpha={outline.Modulate.A})");
        var glow = outline.GetGlobalRect();
        var body = image.GetGlobalRect();
        Require(glow.Position.Y <= body.Position.Y + 1 && glow.End.Y >= body.End.Y - 1,
            "Hover highlight spans the entire translated option");
        Require(button.MouseFilter != Control.MouseFilterEnum.Ignore, "Expanded option accepts input");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
