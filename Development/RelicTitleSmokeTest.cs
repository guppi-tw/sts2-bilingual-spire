#if DEBUG
using BilingualSpire.Localization;
using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Development;

internal static class RelicTitleSmokeTest
{
    internal static async Task Run(NRewardButton reward)
    {
        var source = reward.Reward!.Description;
        CheckText(reward.GetNode<MegaRichTextLabel>("%Label").Text, source);
        reward.Call("OnFocus");
        await Settle();
        var rewardTip = Descendants(NGame.Instance!.HoverTipsContainer!).OfType<MegaLabel>().First(l => l.Name == "Title");
        CheckText(rewardTip.Text, source);
        LogSize(rewardTip, "reward hover");
        await Screenshot("reward");
        foreach (var title in Descendants(NGame.Instance!.HoverTipsContainer!).OfType<MegaLabel>().Where(l => l.Name == "Title"))
        {
            Require(title.GetThemeFontSize("font_size") >= 20 && title.GetLineCount() == 1, "Related effect tooltip title remains readable: " + title.Text);
            CheckBounds(title);
        }
        reward.Call("OnUnfocus");
        await Settle();
        var titles = LocManager.Instance.GetTable("relics").Keys.Where(k => k.EndsWith(".title", StringComparison.Ordinal)).ToArray();
        LanguageSwitcher.SwitchTo("eng");
        var longest = titles.OrderByDescending(k => new LocString("relics", k).GetFormattedText().Length).Take(2).ToArray();
        foreach (var key in new[] { "BURNING_BLOOD.title", "SMALL_CAPSULE.title", "LEAD_PAPERWEIGHT.title" }.Concat(longest).Distinct())
        {
            LanguageSwitcher.SwitchTo("eng");
            var title = new LocString("relics", key);
            var tip = NHoverTipSet.CreateAndShow(reward, new HoverTip(title, "Title layout check"), HoverTipAlignment.Left)!;
            await Settle();
            var label = Descendants(tip).OfType<MegaLabel>().Single(l => l.Name == "Title");
            var single = label.GetThemeFontSize("font_size");
            LanguageSwitcher.ShowBilingual();
            await Settle();
            CheckText(label.Text, title);
            LogSize(label, key + $" (single={single})");
            Require(label.GetThemeFontSize("font_size") >= 16, key + ": title remains readable");
            Require(label.GetLineCount() == 1, key + ": title shares one line");
            CheckBounds(label);
            await Screenshot(key.Replace(".title", ""));
            LanguageSwitcher.SwitchTo("jpn");
            await Settle();
            Require(label.Text == title.GetFormattedText(), key + ": returns to a single-language title");
            NHoverTipSet.Remove(reward);
            await Settle();
        }
        LanguageSwitcher.SetPair("fra", "kor");
        LanguageSwitcher.ShowBilingual();
        reward.Call("OnFocus");
        await Settle();
        rewardTip = Descendants(NGame.Instance!.HoverTipsContainer!).OfType<MegaLabel>().First(l => l.Name == "Title");
        CheckText(rewardTip.Text, source);
        CheckBounds(rewardTip);
        await Screenshot("french-korean");
        reward.Call("OnUnfocus");
        LanguageSwitcher.SetPair("eng", "jpn");
        LanguageSwitcher.ShowBilingual();
        ModEntry.Logger.Info("RELIC TITLE PASS: native reward, hover titles including long names, single-language restoration");
    }

    private static void CheckText(string actual, LocString source)
    {
        var first = ScopedLocale.Render(LanguageSwitcher.Preferences.First, source.GetFormattedText);
        var second = ScopedLocale.Render(LanguageSwitcher.Preferences.Second, source.GetFormattedText);
        Require(actual == (first == second ? first : first + " / " + second), "Relic title shows selected languages side by side: " + actual);
    }

    private static void LogSize(MegaLabel label, string where) => ModEntry.Logger.Info(
        $"READABILITY {where}: font={label.GetThemeFontSize("font_size")}, box={label.Size}, text={label.Text}");

    private static void CheckBounds(MegaLabel label)
    {
        var background = label.GetNode<Control>("%Bg").GetGlobalRect();
        Require(background.Grow(1).Encloses(label.GetGlobalRect()), "Title fits the tooltip background");
        var description = label.GetNode<MegaRichTextLabel>("%Description");
        // The native background includes an outer shadow; validate the readable content.
        Require(label.GetViewportRect().Grow(1).Encloses(label.GetGlobalRect())
            && label.GetViewportRect().Grow(1).Encloses(description.GetGlobalRect()), "Tooltip text fits the viewport: " + label.Text);
    }

    private static async Task Settle()
    {
        var tree = NGame.Instance!.GetTree();
        await tree.ToSignal(tree.CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
    }

    private static async Task Screenshot(string name)
    {
        if (System.Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SCREENSHOT") is not { } screenshot) return;
        RenderingServer.ForceDraw();
        await Task.CompletedTask;
        var file = Path.Combine(Path.GetDirectoryName(screenshot)!, $"relic-title-{name}.png");
        Require(NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(file) == Error.Ok, "Relic title screenshot saved");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren()) foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
