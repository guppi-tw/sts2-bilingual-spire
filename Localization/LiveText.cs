using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.addons.mega_text;

namespace BilingualSpire.Localization;

internal static class LiveText
{
    private const int RecentLimit = 512;
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<string, TextRecipe> Captured = new();
    private static readonly LinkedList<(string Text, TextRecipe Recipe)> Recent = new();
    private static readonly ConditionalWeakTable<Control, Binding> Bindings = new();
    private static readonly MethodInfo ShallowClone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
    [ThreadStatic] private static bool _rendering;
    private static readonly HashSet<string> ReportedErrors = new();

    internal static void Capture(LocString source, string text, bool raw)
    {
        if (_rendering || ScopedLocale.IsActive || string.IsNullOrWhiteSpace(text) || text.Length > 16000 || !text.Any(char.IsLetter)
            || (raw && text.Contains('{'))) return;
        lock (Gate)
        {
            var values = source.Variables.ToDictionary(p => p.Key, p => Snapshot(p.Value));
            TextRecipe recipe = new Localized(source.LocTable, source.LocEntryKey, values, raw);
            Captured.Remove(text);
            Captured.Add(text, recipe);
            Recent.AddFirst((text, recipe));
            if (Recent.Count > RecentLimit) Recent.RemoveLast();
        }
    }

    private static object Snapshot(object value) => value switch
    {
        StringVar variable when Captured.TryGetValue(variable.StringValue, out var recipe)
            => new LocalizedStringVar((StringVar)ShallowClone.Invoke(variable, null)!, recipe),
        // DynamicVar.Clone resets preview values; preserve the values actually shown on this label.
        DynamicVar variable => ShallowClone.Invoke(variable, null)!,
        string text when Captured.TryGetValue(text, out var recipe) => recipe,
        IList<string> strings => new LocalizedList(strings.Select(s => Snapshot(s)).ToArray()),
        _ => value
    };

    internal static string Prepare(Control control, string text)
    {
        if (_rendering) return text;
        Binding? binding = null;
        lock (Gate)
        {
            Bindings.Remove(control);
            var recipe = FindRecipe(text, control is MegaLabel or MegaRichTextLabel);
            if (recipe is not null)
            {
                binding = new Binding(recipe, text);
                Bindings.Add(control, binding);
            }
        }
        if (binding is null) { BilingualFonts.Restore(control); return text; }
        if (!LanguageSwitcher.IsBilingual) { BilingualFonts.Restore(control); return text; }
        _rendering = true;
        try
        {
            var translated = Render(control, binding.Recipe);
            BilingualFonts.Apply(control, translated);
            binding.LastText = translated;
            return translated;
        }
        finally { _rendering = false; }
    }

    internal static void BindRenderer(Control control, Func<string> render)
    {
        var binding = new Binding(new Dynamic(render), GetText(control) ?? "");
        lock (Gate)
        {
            Bindings.Remove(control);
            Bindings.Add(control, binding);
        }
        _rendering = true;
        try { Apply(control, binding); }
        finally { _rendering = false; }
    }

    internal static string Combine(string first, string second, bool richText = true) => ParagraphText.Combine(first, second, richText);

    internal static string CombineFor(Control control, string first, string second) => ParagraphText.Combine(first, second,
        control is RichTextLabel, SecondaryTextStyle.AppliesTo(control) ? SecondaryTextStyle.FontSize(control) : null);

    internal static bool UsesInlineTitle(Control control)
    {
        if (Patches.MenuReadabilityPatch.IsMenuLabel(control)) return true;
        if (Bindings.TryGetValue(control, out var binding) && binding.Recipe.IsRelicTitle) return true;
        if (control.Name != "Title") return false;
        for (var parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is NHoverTipSet) return true;
        return false;
    }

    internal static void RefreshControl(Control control)
    {
        if (!Bindings.TryGetValue(control, out var binding) || GetText(control) != binding.LastText) return;
        var wasRendering = _rendering;
        _rendering = true;
        try
        {
            if (Render(control, binding.Recipe) != binding.LastText) Apply(control, binding);
        }
        finally { _rendering = wasRendering; }
    }

    private static string Render(Control control, TextRecipe recipe)
    {
        if (!LanguageSwitcher.IsBilingual) return recipe.Render();
        var first = ScopedLocale.Render(LanguageSwitcher.Preferences.First, recipe.Render);
        var second = ScopedLocale.Render(LanguageSwitcher.Preferences.Second, recipe.Render);
        if (first == second) return first;
        return control.Name == "TypeLabel" || UsesInlineTitle(control) ? $"{first} / {second}" : CombineFor(control, first, second);
    }

    private static void Apply(Control control, Binding binding)
    {
        var text = Render(control, binding.Recipe);
        if (!LanguageSwitcher.IsBilingual) BilingualFonts.Restore(control);
        switch (control)
        {
            case MegaLabel label:
                label.RefreshFont();
                BilingualFonts.Apply(label, text);
                label.SetTextAutoSize(text);
                break;
            case MegaRichTextLabel rich:
                rich.RefreshFont();
                BilingualFonts.Apply(rich, text);
                rich.SetTextAutoSize(text);
                break;
            case Label label: BilingualFonts.Apply(label, text); label.Text = text; break;
            case RichTextLabel rich: BilingualFonts.Apply(rich, text); rich.Text = text; break;
            case Button button: BilingualFonts.Apply(button, text); button.Text = text; break;
        }
        binding.LastText = text;
    }

    private static TextRecipe? FindRecipe(string text, bool allowComposition)
    {
        if (Captured.TryGetValue(text, out var exact)) return exact;
        if (!allowComposition || text.Length == 0 || text.Length > 16000) return null;

        var pieces = new List<TextRecipe>();
        var literal = new StringBuilder();
        var found = false;
        for (var position = 0; position < text.Length;)
        {
            (string Text, TextRecipe Recipe)? best = null;
            foreach (var entry in Recent)
            {
                if (entry.Text.Length <= (best?.Text.Length ?? 0) || entry.Text.Length > text.Length - position) continue;
                if (text.AsSpan(position).StartsWith(entry.Text, StringComparison.Ordinal)) best = entry;
            }
            if (best is { } match)
            {
                if (literal.Length > 0) { pieces.Add(new Literal(literal.ToString())); literal.Clear(); }
                pieces.Add(match.Recipe);
                position += match.Text.Length;
                found = true;
                continue;
            }
            // Only combine complete localized phrases with markup, punctuation and numbers.
            // An unknown word makes the whole label ineligible (e.g. a player's name).
            var end = position;
            if (text[position] == '[')
            {
                end = text.IndexOf(']', position);
                if (end < 0) return null;
                if (text.AsSpan(position).StartsWith("[img", StringComparison.Ordinal))
                {
                    end = text.IndexOf("[/img]", end, StringComparison.Ordinal);
                    if (end < 0) return null;
                    end += 5;
                }
            }
            else if (char.IsLetter(text[position])) return null;
            literal.Append(text, position, end - position + 1);
            position = end + 1;
        }
        if (!found) return null;
        if (literal.Length > 0) pieces.Add(new Literal(literal.ToString()));
        return new Composite(pieces);
    }

    internal static int Refresh()
    {
        KeyValuePair<Control, Binding>[] bindings;
        lock (Gate) bindings = Bindings.ToArray();
        var updated = 0;
        _rendering = true;
        try
        {
            foreach (var (control, binding) in bindings)
            {
                if (!GodotObject.IsInstanceValid(control) || control.IsQueuedForDeletion()) continue;
                if (GetText(control) != binding.LastText) continue;
                try
                {
                    Apply(control, binding);
                    updated++;
                }
                catch (Exception error) { Report(error); }
            }
        }
        finally { _rendering = false; }
        return updated;
    }

    internal static string? GetText(Control control) => control switch
    {
        Label label => label.Text,
        RichTextLabel rich => rich.Text,
        Button button => button.Text,
        _ => null
    };

    internal static void Report(Exception error)
    {
        lock (Gate)
            if (ReportedErrors.Add(error.Message))
                ModEntry.Logger.Warn($"Could not refresh localized text; keeping its current text: {error}");
    }

    private sealed class Binding(TextRecipe recipe, string text)
    {
        public TextRecipe Recipe { get; } = recipe;
        public string LastText { get; set; } = text;
    }

    private abstract record TextRecipe
    {
        public virtual bool IsRelicTitle => false;
        public abstract string Render();
    }

    private sealed record Literal(string Text) : TextRecipe
    {
        public override string Render() => Text;
    }

    private sealed record Dynamic(Func<string> Formatter) : TextRecipe
    {
        public override string Render() => Formatter();
    }

    private sealed record Composite(List<TextRecipe> Pieces) : TextRecipe
    {
        public override string Render() => string.Concat(Pieces.Select(p => p.Render()));
    }

    private sealed record LocalizedList(object[] Items)
    {
        public List<string> Render() => Items.Select(item => item is TextRecipe recipe ? recipe.Render() : (string)item).ToList();
    }

    private sealed record LocalizedStringVar(StringVar Original, TextRecipe Text)
    {
        public StringVar Render()
        {
            var value = (StringVar)ShallowClone.Invoke(Original, null)!;
            value.StringValue = Text.Render();
            return value;
        }
    }

    private sealed record Localized(string Table, string Key, Dictionary<string, object> Values, bool Raw) : TextRecipe
    {
        public override bool IsRelicTitle => Table == "relics"
            && (Key.EndsWith(".title", StringComparison.Ordinal) || Key == "TOY_BOX.waxRelicPrefix");
        public override string Render()
        {
            var source = new LocString(Table, Key);
            foreach (var (name, value) in Values)
                source.AddObj(name, value switch
                {
                    TextRecipe nested => nested.Render(),
                    LocalizedList list => list.Render(),
                    LocalizedStringVar variable => variable.Render(),
                    _ => value
                });
            return Raw ? source.GetRawText() : source.GetFormattedText();
        }
    }
}
