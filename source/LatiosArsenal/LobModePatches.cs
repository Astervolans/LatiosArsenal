using System.Reflection;
using HarmonyLib;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 高抛模式的两处运行时注入：
    ///  1) CombatExtended.Verb_ShootCE.get_Projectile：CE 武器的弹头来自已装填弹药，
    ///     这里在取弹头时按模式替换成 _Lob 弹头（flyOverhead + 降速 + spreadMult 1.5）。
    ///  2) Verse.Verb.WarmupTime：高抛模式瞄准时间乘倍数。
    /// 两者都用 AccessTools 按名字/类型取方法，不直接引用 CombatExtended 程序集。
    /// 冷却由 CompLobMode.GetStatFactor 处理；散布由 _Lob 弹头的 spreadMult 处理。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LobModePatches
    {
        static LobModePatches()
        {
            Harmony harmony = new Harmony("unscLatios.LatiosArsenal.lob");

            MethodInfo getProjectile = AccessTools.Method("CombatExtended.Verb_ShootCE:get_Projectile");
            if (getProjectile != null)
            {
                harmony.Patch(getProjectile, postfix: new HarmonyMethod(
                    typeof(LobModePatches).GetMethod(nameof(ProjectilePostfix), BindingFlags.Static | BindingFlags.Public)));
            }
            else
            {
                Log.Warning("[LatiosArsenal] 未找到 CombatExtended.Verb_ShootCE.get_Projectile，高抛模式将不会替换弹头");
            }

            MethodInfo getWarmup = AccessTools.PropertyGetter(typeof(Verb), nameof(Verb.WarmupTime));
            if (getWarmup != null)
            {
                harmony.Patch(getWarmup, postfix: new HarmonyMethod(
                    typeof(LobModePatches).GetMethod(nameof(WarmupPostfix), BindingFlags.Static | BindingFlags.Public)));
            }
        }

        public static void ProjectilePostfix(Verb __instance, ref ThingDef __result)
        {
            Thing equipment = __instance?.EquipmentSource;
            if (equipment == null || !LobModeUtility.IsLobMode(equipment))
            {
                return;
            }
            __result = LobModeUtility.LobProjectileFor(equipment, __result);
        }

        public static void WarmupPostfix(Verb __instance, ref float __result)
        {
            Thing equipment = __instance?.EquipmentSource;
            if (equipment == null || __instance.verbProps == null || __instance.verbProps.IsMeleeAttack)
            {
                return;
            }
            CompLobMode comp = CompLobMode.GetFor(equipment);
            if (comp != null && comp.LobMode)
            {
                __result *= comp.Props.warmupMultiplier;
            }
        }
    }
}
