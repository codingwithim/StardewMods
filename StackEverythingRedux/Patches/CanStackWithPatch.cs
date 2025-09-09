using HarmonyLib;
using StardewValley;
using StardewValley.Objects;

namespace StackEverythingRedux.Patches
{
    [HarmonyPatch(typeof(Item))]
    internal static class CanStackWithPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Item.canStackWith), new[] { typeof(ISalable) })]
        public static bool Prefix(Item __instance, ref bool __result, ISalable other)
        {
            if (__instance is null || other is null)
            {
                return true;
            }

            if ((__instance is StorageFurniture dresser1 && dresser1.heldItems is { Count: > 0 })
             || (other is StorageFurniture dresser2 && dresser2.heldItems is { Count: > 0 }))
            {
                __result = false;
                return false;
            }

            if (IsNoStackQualifiedId(__instance) || IsNoStackQualifiedId(other))
            {
                __result = false;
                return false;
            }

            return true;
        }

        private static bool IsNoStackQualifiedId(ISalable f)
        {
            HashSet<string> set = StaticConfig.NoStackQualifiedIds;
            return f is not null && set is not null && set.Contains(f.QualifiedItemId);
        }
    }
}
