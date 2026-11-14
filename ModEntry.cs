using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace BilingualSpire;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    internal static readonly Logger Logger = new("BilingualSpire", LogType.Generic);

    public static void Initialize()
    {
        var harmony = new Harmony("guppi-tw.BilingualSpire");
        try
        {
            // Install text capture before patching game UI methods: JIT compilation of
            // those methods can inline localization calls and bypass a later patch.
            var capture = new[] { typeof(Patches.RawTextPatch), typeof(Patches.FormattedTextPatch), typeof(Patches.LabelTextPatch) };
            foreach (var type in capture.Concat(typeof(ModEntry).Assembly.GetTypes()
                         .Where(t => t.IsDefined(typeof(HarmonyPatch), false)).Except(capture)))
                harmony.CreateClassProcessor(type).Patch();
        }
        catch
        {
            harmony.UnpatchAll(harmony.Id);
            throw;
        }
        Logger.Info("BilingualSpire v0.5.6 loaded successfully");
        LanguageSwitcher.Start();
#if DEBUG
        if (Environment.GetEnvironmentVariable("BILINGUAL_SPIRE_SMOKE_TEST") == "1")
            _ = Development.SmokeTest.Run();
#endif
    }
}
