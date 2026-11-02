using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 高抛模式的落点散布：改成「以目标为中心的圆形位置误差」。
    ///
    /// CE 的 ShiftVecReport 有两套误差：
    ///   1) distShift（只沿距离方向）：shotDist² / maxRange * min(accuracyFactor*0.5, 0.8)，
    ///      由 GetRandDist() 用来抖动“瞄准距离”，高抛时会把距离误差放大（46 格目标能打出 23~66 格）；
    ///   2) circularMissRadius（目标点的圆形偏移）+ spreadDegrees/swayDegrees（角度误差，横向）。
    /// 高抛模式下把 (2) 的角度项清零、把圆形半径设为 lobCircularMissRadius，
    /// 并反解 maxRange 使 distShift 恰好等于同一半径，于是径向与横向误差同量级，落点呈圆形分布。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LobSpreadPatches
    {
        static LobSpreadPatches()
        {
            Harmony harmony = new Harmony("unscLatios.LatiosArsenal.lobspread");
            System.Type verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
            MethodInfo method = verbType == null
                ? null
                : AccessTools.Method(verbType, "ShiftVecReportFor", new[] { typeof(LocalTargetInfo), typeof(IntVec3) });
            if (method != null)
            {
                harmony.Patch(method, postfix: new HarmonyMethod(
                    typeof(LobSpreadPatches).GetMethod(nameof(ShiftVecReportPostfix), BindingFlags.Static | BindingFlags.Public)));
            }
            else
            {
                Log.Warning("[LatiosArsenal] 未找到 CombatExtended.Verb_LaunchProjectileCE.ShiftVecReportFor，高抛落点将沿用 CE 原生散布模型");
            }
        }

        private static FieldInfo fieldSpread;
        private static FieldInfo fieldSway;
        private static FieldInfo fieldMaxRange;
        private static FieldInfo fieldCircular;
        private static FieldInfo fieldShotDist;
        private static FieldInfo fieldAiming;
        private static FieldInfo fieldSights;
        private static bool fieldsResolved;

        private static void ResolveFields(System.Type type)
        {
            if (fieldsResolved || type == null)
            {
                return;
            }
            fieldsResolved = true;
            fieldSpread = AccessTools.Field(type, "spreadDegrees");
            fieldSway = AccessTools.Field(type, "swayDegrees");
            fieldMaxRange = AccessTools.Field(type, "maxRange");
            fieldCircular = AccessTools.Field(type, "circularMissRadius");
            fieldShotDist = AccessTools.Field(type, "shotDist");
            fieldAiming = AccessTools.Field(type, "aimingAccuracy");
            fieldSights = AccessTools.Field(type, "sightsEfficiency");
            if (fieldSpread == null || fieldSway == null || fieldMaxRange == null || fieldCircular == null || fieldShotDist == null)
            {
                Log.Warning("[LatiosArsenal] ShiftVecReport 字段解析失败，高抛落点将沿用 CE 原生散布模型");
            }
        }

        public static void ShiftVecReportPostfix(Verb __instance, ref object __result)
        {
            if (__result == null)
            {
                return;
            }
            CompLobMode comp = CompLobMode.GetFor(__instance?.EquipmentSource);
            if (comp == null || !comp.LobMode)
            {
                return;
            }
            ResolveFields(__result.GetType());
            if (fieldSpread == null || fieldSway == null || fieldMaxRange == null || fieldCircular == null || fieldShotDist == null)
            {
                return;
            }

            float shotDist = (float)fieldShotDist.GetValue(__result);

            // 半径随射程线性增长：base + perTile × 距离，超过上限则截断
            float radius = comp.Props.lobCircularMissBase + comp.Props.lobCircularMissPerTile * shotDist;
            if (comp.Props.lobCircularMissMax > 0f)
            {
                radius = Mathf.Min(radius, comp.Props.lobCircularMissMax);
            }
            radius = Mathf.Max(0.1f, radius);

            // 角度误差清零，误差全部改成位置误差
            fieldSpread.SetValue(__result, 0f);
            fieldSway.SetValue(__result, 0f);
            fieldCircular.SetValue(__result, radius);

            // 反解 maxRange，让 distShift（只沿距离方向的偏差）也等于 radius：
            // distShift = shotDist * (shotDist / max(maxRange, 20)) * k   ⇒   maxRange = shotDist² * k / radius
            float aim = fieldAiming != null ? (float)fieldAiming.GetValue(__result) : 1f;
            float sights = fieldSights != null ? (float)fieldSights.GetValue(__result) : 1f;
            float accuracyFactor = sights > 0.01f ? (1.5f - aim) / sights : 0f;
            float k = Mathf.Min(accuracyFactor * 0.5f, 0.8f);
            if (k > 0.01f && shotDist > 0.1f)
            {
                fieldMaxRange.SetValue(__result, shotDist * shotDist * k / radius);
            }
        }
    }
}
