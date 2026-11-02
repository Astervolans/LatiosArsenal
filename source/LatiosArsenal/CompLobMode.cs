using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LatiosArsenal
{
    public class CompProperties_LobMode : CompProperties
    {
        /// <summary>高抛模式下的瞄准时间倍数。</summary>
        public float warmupMultiplier = 1.5f;

        /// <summary>高抛模式下的冷却时间倍数（通过 GetStatFactor 作用于 RangedWeapon_Cooldown）。</summary>
        public float cooldownMultiplier = 1.5f;

        /// <summary>高抛弹头后缀：普通弹头 defName + 该后缀 = 高抛弹头 defName。</summary>
        public string lobProjectileSuffix = "_Lob";

        public CompProperties_LobMode()
        {
            compClass = typeof(CompLobMode);
        }
    }

    /// <summary>
    /// 高抛（迫击炮式）发射模式。
    ///  - 弹头：由 LobModePatches 在 Verb_ShootCE.Projectile 上替换成 _Lob 弹头（flyOverhead + 降速 + spreadMult 1.5）；
    ///  - 瞄准：LobModePatches 对 Verb.WarmupTime 乘倍数；
    ///  - 冷却：这里通过 GetStatFactor 对 RangedWeapon_Cooldown 乘倍数；
    ///  - 散布：由高抛弹头自身的 spreadMult 承担（CE 原生路径）。
    /// </summary>
    public class CompLobMode : ThingComp
    {
        private static readonly Dictionary<string, ThingDef> lobProjectileCache = new Dictionary<string, ThingDef>();

        private bool lobMode;

        public CompProperties_LobMode Props => (CompProperties_LobMode)props;

        public bool LobMode => lobMode;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lobMode, "latiosLobMode", false);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            lobMode = false;
        }

        public void Toggle()
        {
            lobMode = !lobMode;
        }

        /// <summary>普通弹头 → 高抛弹头；找不到就返回 null（此时保持原弹头，不会炸膛）。</summary>
        public ThingDef ResolveLobProjectile(ThingDef normalProjectile)
        {
            if (normalProjectile == null)
            {
                return null;
            }
            string key = normalProjectile.defName;
            if (key.EndsWith(Props.lobProjectileSuffix))
            {
                return normalProjectile;
            }
            if (lobProjectileCache.TryGetValue(key, out ThingDef cached))
            {
                return cached;
            }
            ThingDef lob = DefDatabase<ThingDef>.GetNamedSilentFail(key + Props.lobProjectileSuffix);
            lobProjectileCache[key] = lob;
            return lob;
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (lobMode && stat == StatDefOf.RangedWeapon_Cooldown)
            {
                return Props.cooldownMultiplier;
            }
            return 1f;
        }

        public override string CompInspectStringExtra()
        {
            return (lobMode ? "LatiosArsenal_LobMode_On" : "LatiosArsenal_LobMode_Off").Translate();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            foreach (Gizmo gizmo in LobModeGizmos())
            {
                yield return gizmo;
            }
        }

        /// <summary>
        /// 模式切换按钮。装备状态下 RimWorld 不会调用武器组件的 CompGetGizmosExtra，
        /// 所以 LobGizmoPatch 会在 Pawn.GetGizmos 上把这里产出的按钮拼回去。
        /// </summary>
        public IEnumerable<Gizmo> LobModeGizmos()
        {
            yield return new Command_Action
            {
                defaultLabel = (lobMode ? "LatiosArsenal_LobMode_SwitchToDirect" : "LatiosArsenal_LobMode_SwitchToLob").Translate(),
                defaultDesc = "LatiosArsenal_LobMode_Desc".Translate(),
                icon = parent.def.uiIcon,
                action = Toggle
            };
        }

        /// <summary>供 Harmony 补丁使用：从装备上取高抛模式组件。</summary>
        public static CompLobMode GetFor(Thing equipment)
        {
            return equipment is ThingWithComps withComps ? withComps.GetComp<CompLobMode>() : null;
        }
    }

    /// <summary>高抛补丁的公共逻辑（仅依赖 Verse 类型，不直接引用 CombatExtended 程序集）。</summary>
    public static class LobModeUtility
    {
        public static bool IsLobMode(Thing equipment)
        {
            CompLobMode comp = CompLobMode.GetFor(equipment);
            return comp != null && comp.LobMode;
        }

        public static ThingDef LobProjectileFor(Thing equipment, ThingDef projectile)
        {
            CompLobMode comp = CompLobMode.GetFor(equipment);
            if (comp == null || projectile == null)
            {
                return projectile;
            }
            ThingDef lob = comp.ResolveLobProjectile(projectile);
            return lob ?? projectile;
        }
    }
}
