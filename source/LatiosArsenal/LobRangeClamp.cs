using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LatiosArsenal
{
    /// <summary>
    /// 射程夹紧：CE 的 EffectiveRange 会被角色的 RangedWeapon_RangeMultiplier（基因/植入物/武器特性）
    /// 放大，可能超过当前弹头的弹道上限。超过时 CE 的 GetShotAngle 判别式为负，会逐发打印
    /// "[CE] Tried to fire projectile to unreachable target cell, truncating to maximum distance."
    /// 并按 45° 发射，炮弹落在最大射程处（打不到目标、可能砸到沿途目标）。
    ///
    /// 这里把本模组武器的 EffectiveRange 夹到「当前弹头实际能打到的距离」，射程环、
    /// 目标选择与开火判定都会跟着变小，从源头上避免不可达目标。
    /// 直射模式弹道余量极大（约 1900 格），夹紧不会生效；只有高抛弹头（gravityFactor 10）会真正用到。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LobRangeClamp
    {
        private const float GravityBase = 9.8f;

        /// <summary>CE 的 ProjectilePropertiesCE.GravityPerWidth = Gravity / 5。</summary>
        private const float GravityPerWidthDivisor = 5f;

        /// <summary>估算用的射击高度（取偏保守值，宁可比实际少算一点）。</summary>
        private const float AssumedShotHeight = 2f;

        /// <summary>留出的安全余量（格），避免正好卡在判别式边界上。</summary>
        private const float SafetyMargin = 1f;

        private static readonly Dictionary<string, float> CapCache = new Dictionary<string, float>();
        private static readonly Dictionary<string, FieldInfo> GravityFieldCache = new Dictionary<string, FieldInfo>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        static LobRangeClamp()
        {
            Harmony harmony = new Harmony("unscLatios.LatiosArsenal.lobrange");
            System.Type verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
            MethodInfo getter = verbType == null ? null : AccessTools.PropertyGetter(verbType, "EffectiveRange");
            if (getter != null)
            {
                harmony.Patch(getter, postfix: new HarmonyMethod(
                    typeof(LobRangeClamp).GetMethod(nameof(EffectiveRangePostfix), BindingFlags.Static | BindingFlags.Public)));
            }
            else
            {
                Log.Warning("[LatiosArsenal] 未找到 CombatExtended.Verb_LaunchProjectileCE.EffectiveRange，射程夹紧未启用");
            }
        }

        public static void EffectiveRangePostfix(Verb __instance, ref float __result)
        {
            Thing equipment = __instance?.EquipmentSource;
            if (equipment == null)
            {
                return;
            }
            // 仅处理挂了高抛组件的武器（本模组的五把发射器）
            if (CompLobMode.GetFor(equipment) == null)
            {
                return;
            }
            if (!(__instance is Verb_LaunchProjectile launcher))
            {
                return;
            }
            ThingDef projectile = launcher.Projectile;
            if (projectile?.projectile == null)
            {
                return;
            }

            float cap = BallisticRangeCap(projectile);
            if (cap <= 0f || __result <= cap)
            {
                return;
            }

            __result = cap;
            if (Warned.Add(equipment.def.defName + "|" + projectile.defName))
            {
                Log.Message("[LatiosArsenal] " + equipment.def.defName + " 的有效射程已按弹头弹道上限夹紧到 "
                            + cap.ToString("0.0") + " 格（弹头 " + projectile.defName + "）");
            }
        }

        /// <summary>
        /// 由弹头速度与重力系数算出能打到的最大水平距离：
        /// 发射角 45° 时的抛物线射程，并计入射击高度带来的额外距离。
        /// 与 CE 的 CE_Utility.MaxProjectileRange 同一套公式。
        /// </summary>
        public static float BallisticRangeCap(ThingDef projectileDef)
        {
            if (projectileDef == null)
            {
                return -1f;
            }
            if (CapCache.TryGetValue(projectileDef.defName, out float cached))
            {
                return cached;
            }

            float speed = projectileDef.projectile.speed;
            float gravityFactor = ReadGravityFactor(projectileDef.projectile);
            float g = GravityBase * gravityFactor / GravityPerWidthDivisor;
            float cap;
            if (speed <= 0.01f || g <= 0.01f)
            {
                cap = -1f;
            }
            else
            {
                // 45°: R = v·cos45/g · (v·sin45 + sqrt((v·sin45)² + 2·g·h))
                float sin = Mathf.Sin(Mathf.PI / 4f);
                float cos = Mathf.Cos(Mathf.PI / 4f);
                float range = speed * cos / g
                              * (speed * sin + Mathf.Sqrt(Mathf.Pow(speed * sin, 2f) + 2f * g * AssumedShotHeight));
                cap = Mathf.Max(0f, range - SafetyMargin);
            }

            CapCache[projectileDef.defName] = cap;
            return cap;
        }

        /// <summary>gravityFactor 是 CE 的字段，用反射读取（不引用 CE 程序集）；读不到按 1 处理。</summary>
        private static float ReadGravityFactor(ProjectileProperties projectile)
        {
            System.Type type = projectile.GetType();
            string key = type.FullName;
            if (!GravityFieldCache.TryGetValue(key, out FieldInfo field))
            {
                field = type.GetField("gravityFactor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                GravityFieldCache[key] = field;
            }
            if (field == null)
            {
                return 1f;
            }
            object value = field.GetValue(projectile);
            return value is float f && f > 0.01f ? f : 1f;
        }
    }
}
