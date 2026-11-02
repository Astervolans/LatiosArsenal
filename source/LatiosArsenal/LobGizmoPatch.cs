using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 高抛模式按钮的显示位置。
    ///
    /// RimWorld 1.7 在「装备」状态下不会调用武器组件自己的 CompGetGizmosExtra（只有服饰有 CompGetWornGizmosExtra）；
    /// CE 的弹药/射击模式按钮是靠 CE 的 CompPawnGizmo 转发，且只转发继承自 CombatExtended.CompRangedGizmoGiver 的组件。
    /// 这里直接在 Pawn.GetGizmos 上做后置拼接：只要小人主手武器带 CompLobMode，就补一个「切换高抛/直射」按钮，
    /// 与种族、是否继承 CE 的组件基类无关。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LobGizmoPatch
    {
        static LobGizmoPatch()
        {
            new Harmony("unscLatios.LatiosArsenal.lobgizmo")
                .PatchAll(typeof(LobGizmoPatch).Assembly);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null || __instance.Faction != Faction.OfPlayer)
            {
                return;
            }
            CompLobMode comp = CompLobMode.GetFor(__instance.equipment?.Primary);
            if (comp == null)
            {
                return;
            }
            __result = WithLobGizmo(comp, __result);
        }

        private static IEnumerable<Gizmo> WithLobGizmo(CompLobMode comp, IEnumerable<Gizmo> rest)
        {
            foreach (Gizmo gizmo in comp.LobModeGizmos())
            {
                yield return gizmo;
            }
            if (rest == null)
            {
                yield break;
            }
            foreach (Gizmo gizmo in rest)
            {
                yield return gizmo;
            }
        }
    }
}
