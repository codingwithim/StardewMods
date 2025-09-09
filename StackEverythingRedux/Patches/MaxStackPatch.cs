using System.Reflection;
using HarmonyLib;
using StardewValley;

namespace StackEverythingRedux.Patches
{
    [HarmonyPatch]
    internal static class MaxStackPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Assembly asm = typeof(Item).Assembly;
            Type itemType = typeof(Item);

            foreach (Type t in asm.GetTypes())
            {
                if (t.IsAbstract)
                {
                    continue;
                }

                if (!itemType.IsAssignableFrom(t))
                {
                    continue;
                }

                MethodInfo m = t.GetMethod(nameof(Item.maximumStackSize),
                    BindingFlags.Instance | BindingFlags.Public);
                if (m == null)
                {
                    continue;
                }

                if (m.IsAbstract)
                {
                    continue;
                }

                if (m.DeclaringType != t)
                {
                    continue;
                }

                yield return m;
            }
        }

        private static void Postfix(Item __instance, ref int __result)
        {
            try
            {
                if (!StackEverythingRedux.Config.EnableTackleSplit && __instance is StardewValley.Object obj && obj.Category == -22)
                {
                    return;
                }
                else if (StaticConfig.NoStackQualifiedIds != null && StaticConfig.NoStackQualifiedIds.Contains(__instance.QualifiedItemId))
                {
                    return;
                }

                __result = StackEverythingRedux.Config.MaxStackingNumber;

            }
            catch (Exception ex)
            {
                Log.Error($"[Patch] Fail to adjust max stack: {ex}");
            }
        }

    }
}
