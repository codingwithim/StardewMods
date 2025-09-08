using HarmonyLib;
using StardewValley;
using StardewValley.Tools;
using SObject = StardewValley.Object;

namespace StackEverythingRedux.Patches
{
    [HarmonyPatch(typeof(Item), nameof(Item.addToStack))]
    internal static class TackleStackPatch
    {
        private static int preDestStack, preSrcStack;
        private static int preDestUses, preSrcUses;
        private static SObject preDestObj, preSrcObj;

        private static void Prefix(Item __instance, Item otherStack)
        {
            preDestObj = __instance as SObject;
            preSrcObj = otherStack as SObject;

            if (!IsSameTackle(preDestObj, preSrcObj))
            {
                return;
            }

            preDestStack = preDestObj.Stack;
            preSrcStack = preSrcObj.Stack;
            preDestUses = SafeUses(preDestObj);
            preSrcUses = SafeUses(preSrcObj);

            Log.Debug($"[TackleMerge.Prefix] dest(S={preDestStack}, uses={preDestUses}) src(S={preSrcStack}, uses={preSrcUses}) id={preDestObj.QualifiedItemId}");
        }

        private static void Postfix(Item __instance, Item otherStack)
        {
            SObject dest = __instance as SObject;
            SObject src = otherStack as SObject;

            if (!IsSameTackle(dest, src))
            {
                return;
            }

            int moved = dest.Stack - preDestStack;
            if (moved <= 0)
            {
                return;
            }

            int M = FishingRod.maxTackleUses;
            int destRemainingBefore = (preDestStack * M) - preDestUses;

            int remainingMoved = (moved * M) - ((moved > 0) ? preSrcUses : 0);
            if (remainingMoved < 0)
            {
                remainingMoved = 0;
            }

            int totalRemaining = destRemainingBefore + remainingMoved;

            int full = totalRemaining / M;
            int rem = totalRemaining % M;
            int newDestStack = (totalRemaining > 0) ? 1 : 0;
            int newDestUses = (totalRemaining > 0) ? (M - (totalRemaining % M == 0 ? M : totalRemaining % M)) : 0;

            if (newDestStack == 1 && totalRemaining % M == 0)
            {
                newDestUses = 0;
            }

            Log.Debug($"[TackleMerge.Dest] moved={moved} M={M} totalRemaining={totalRemaining} -> full={full} rem={rem} => newStack={newDestStack} newUses(consumidos)={newDestUses}");

            dest.Stack = newDestStack;
            try { dest.uses.Value = newDestUses; } catch { }

            if (src.Stack > 0)
            {
                int srcRemainingBefore = (preSrcStack * M) - preSrcUses;
                int remainingAfter = srcRemainingBefore - remainingMoved;
                if (remainingAfter < 0)
                {
                    remainingAfter = 0;
                }

                int SA = src.Stack;
                int topRemainingAfter = remainingAfter - ((SA - 1) * M);
                if (topRemainingAfter < 0)
                {
                    topRemainingAfter = 0;
                }

                if (topRemainingAfter > M)
                {
                    topRemainingAfter = M;
                }

                int newSrcUses = M - topRemainingAfter;
                try { src.uses.Value = newSrcUses; } catch { }

                Log.Debug($"[TackleMerge.Src] after: S={SA} remaining={remainingAfter} -> topRemaining={topRemainingAfter} newUses(consumidos)={newSrcUses}");
            }
        }

        private static bool IsSameTackle(SObject a, SObject b)
        {
            return a != null && b != null &&
            a.Category == -22 && b.Category == -22 &&
            a.QualifiedItemId == b.QualifiedItemId;
        }

        private static int SafeUses(SObject o)
        {
            try { return o.uses.Value; }
            catch { return 0; }
        }
    }
}
