using HarmonyLib;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace StackEverythingRedux.Patches
{
    [HarmonyPatch(typeof(FishingRod), "doDoneFishing")]
    internal static class TackleDurabilityPatch
    {
        private static bool origLastCatchWasJunk;

        private static void Prefix(FishingRod __instance, ref bool consumeBaitAndTackle)
        {
            if (!consumeBaitAndTackle)
            {
                return;
            }

            origLastCatchWasJunk = __instance.lastCatchWasJunk;
            __instance.lastCatchWasJunk = true;

            Farmer who = __instance.getLastFarmerToUse();
            float consumeChance = 1f;
            if (__instance.hasEnchantmentOfType<PreservingEnchantment>())
            {
                consumeChance = 0.5f;
            }

            int i = 1;
            foreach (SObject tackle in __instance.GetTackle())
            {
                if (tackle != null && !origLastCatchWasJunk && Game1.random.NextDouble() < consumeChance)
                {
                    if (tackle.QualifiedItemId == "(O)789")
                    {
                        break;
                    }

                    int max = FishingRod.maxTackleUses;
                    int used = 0;
                    try { used = tackle.uses.Value; } catch { used = 0; }

                    used++;

                    if (used >= max)
                    {
                        if (tackle.Stack > 1)
                        {
                            tackle.Stack -= 1;
                            try { tackle.uses.Value = 0; } catch { }
                            Log.Debug($"[Tackle] break->shift | slot={i} newStack={tackle.Stack} uses=0 max={max}");
                        }
                        else
                        {
                            __instance.attachments[i] = null;
                            if (who?.IsLocalPlayer == true)
                            {
                                Game1.showGlobalMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:FishingRod.cs.14086"));
                            }

                            Log.Debug($"[Tackle] break->remove | slot={i}");
                        }
                    }
                    else
                    {
                        try { tackle.uses.Value = used; } catch { }
                        Log.Debug($"[Tackle] consume | slot={i} uses={used}/{max} stack={tackle.Stack}");
                    }
                }
                i++;
            }
        }

        private static void Postfix(FishingRod __instance, ref bool consumeBaitAndTackle)
        {
            if (consumeBaitAndTackle)
            {
                __instance.lastCatchWasJunk = origLastCatchWasJunk;
            }
        }
    }
}
