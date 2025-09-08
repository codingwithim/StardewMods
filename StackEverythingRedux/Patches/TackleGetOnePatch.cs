using HarmonyLib;
using StardewValley;
using SObject = StardewValley.Object;

namespace StackEverythingRedux.Patches
{
    [HarmonyPatch(typeof(Item), nameof(Item.getOne))]
    internal static class TackleGetOnePatch
    {
        private static void Postfix(Item __instance, ref Item __result)
        {
            if (__result is not SObject || __instance is not SObject src)
            {
                return;
            }

            if (src.Category != -22)
            {
                return;
            }

            int max = 20;
            int used = 0;
            try { used = src.uses.Value; } catch { }

            Log.Debug($"[TackleGetOne] clone from src uses={used}/{max} srcStack={src.Stack}");
        }
    }
}
