using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 受伤即触发：只挂一个 postfix，平时没有任何逐帧开销。
    /// 目标是 Pawn.PostApplyDamage（Source/Verse/Pawn.cs），游戏内所有对 Pawn 的伤害都会经过这里。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BiofoamHarmony
    {
        static BiofoamHarmony()
        {
            new Harmony("unscLatios.LatiosArsenal").PatchAll(typeof(BiofoamHarmony).Assembly);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_Pawn_PostApplyDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            if (__instance == null)
            {
                return;
            }
            List<Apparel> worn = __instance.apparel?.WornApparel;
            if (worn == null || worn.Count == 0)
            {
                return;
            }
            for (int i = 0; i < worn.Count; i++)
            {
                CompBiofoamInjector injector = worn[i].TryGetComp<CompBiofoamInjector>();
                if (injector != null)
                {
                    injector.TryTrigger();
                }
            }
        }
    }
}
