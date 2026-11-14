using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace BilingualSpire.Localization;

internal static class ScopedLocale
{
    private sealed record Locale(string Language, CultureInfo Culture, Dictionary<string, LocTable> Tables);
    private static readonly Dictionary<string, Locale> Locales = new();
    [ThreadStatic] private static Locale? _current;
    internal static bool IsActive => _current is not null;
    internal static string? Language => _current?.Language;
    internal static CultureInfo? Culture => _current?.Culture;

    internal static string Render(string language, Func<string> render)
    {
        if (!Locales.TryGetValue(language, out var locale))
        {
            // Use the game's loader so fallback strings and mod translations follow its normal rules.
            var loaded = (ITuple)AccessTools.Method(typeof(LocManager), "LoadTablesFromPath")
                .Invoke(null, [language, true])!;
            var culture = (CultureInfo)AccessTools.Method(typeof(LocManager), "CultureInfoFromThreeLetterCode")
                .Invoke(LocManager.Instance, [language])!;
            locale = new(language, culture, (Dictionary<string, LocTable>)loaded[0]!);
            Locales.Add(language, locale);
        }
        var previous = _current;
        _current = locale;
        try { return render(); }
        finally { _current = previous; }
    }

    internal static bool TryGetTable(string name, out LocTable table)
    {
        table = null!;
        return _current is not null && _current.Tables.TryGetValue(name, out table!);
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class ScopedTablePatch
{
    private static bool Prefix(string __0, ref LocTable __result)
    {
        if (!ScopedLocale.TryGetTable(__0, out var table)) return true;
        __result = table;
        return false;
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.Language), MethodType.Getter)]
internal static class ScopedLanguagePatch
{
    private static bool Prefix(ref string __result)
    {
        if (ScopedLocale.Language is not { } language) return true;
        __result = language;
        return false;
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.CultureInfo), MethodType.Getter)]
internal static class ScopedCulturePatch
{
    private static bool Prefix(ref CultureInfo __result)
    {
        if (ScopedLocale.Culture is not { } culture) return true;
        __result = culture;
        return false;
    }
}
