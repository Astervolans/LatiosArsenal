using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LatiosArsenal
{
    public class CompProperties_BiofoamInjector : CompProperties
    {
        /// <summary>最大充能数。</summary>
        public int maxCharges = 4;

        /// <summary>每份医药恢复的充能数。</summary>
        public int chargesPerMedicine = 2;

        /// <summary>触发阈值：HediffSet.BleedRateTotal 高于该值即触发（1 = 流血速度 100%）。</summary>
        public float bleedThreshold = 1f;

        /// <summary>兜底检查间隔（tick）。伤害事件已经覆盖绝大多数情况，此处只兜底非伤害来源的流血。</summary>
        public int fallbackCheckInterval = 60;

        /// <summary>止血时给出的包扎质量，固定 75%。</summary>
        public float tendQuality = 0.75f;

        /// <summary>装填作业的基础耗时（tick）。随身携带医药时同样要花这些时间。</summary>
        public int reloadWorkTicks = 240;

        /// <summary>自动装填的检查间隔（tick）。0 = 关闭自动装填检查。</summary>
        public int autoReloadCheckInterval = 250;

        /// <summary>自动装填找不到医药/无法开始时的重试间隔（tick），避免每次检查都重复尝试。</summary>
        public int autoReloadRetryTicks = 1500;

        /// <summary>自动装填默认是否开启。</summary>
        public bool autoReloadByDefault = true;

        public CompProperties_BiofoamInjector()
        {
            compClass = typeof(CompBiofoamInjector);
        }
    }

    /// <summary>
    /// 生物泡沫注射器：腰带装备。
    /// 触发路径有两条：
    ///  1) 受伤事件（Harmony postfix: Pawn.PostApplyDamage）——即时，覆盖正常战斗中的失血；
    ///  2) CompTick 兜底——每 fallbackCheckInterval tick 读一次缓存的 BleedRateTotal。
    /// 装填不是瞬间操作：由 JobDriver_ReloadBiofoamInjector 走取药 + 等待后完成。
    /// </summary>
    public class CompBiofoamInjector : ThingComp
    {
        private int charges = -1;
        private int lastFallbackTick = -99999;
        private int lastAutoReloadTick = -99999;
        private int autoReloadRetryUntil;
        private bool autoReload = true;

        public CompProperties_BiofoamInjector Props => (CompProperties_BiofoamInjector)props;

        private Apparel Belt => parent as Apparel;

        public Pawn Wearer => Belt?.Wearer;

        public int Charges => Mathf.Clamp(charges, 0, Props.maxCharges);

        public bool IsFull => Charges >= Props.maxCharges;

        public override void PostPostMake()
        {
            base.PostPostMake();
            charges = Props.maxCharges;
            autoReload = Props.autoReloadByDefault;
        }

        public bool AutoReload => autoReload;

        public void ToggleAutoReload()
        {
            autoReload = !autoReload;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref charges, "biofoamCharges", -1);
            Scribe_Values.Look(ref autoReload, "biofoamAutoReload", Props.autoReloadByDefault);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && charges < 0)
            {
                charges = Props.maxCharges;
            }
        }

        public override void CompTick()
        {
            int ticks = Find.TickManager.TicksGame;

            // 1) 止血兜底：低频读缓存的流血值
            if (Props.fallbackCheckInterval > 0 && ticks - lastFallbackTick >= Props.fallbackCheckInterval)
            {
                lastFallbackTick = ticks;
                TryTrigger(announce: false);
            }

            // 2) 自动装填：充能不满且允许自动装填时，自行去取医药装填
            if (Props.autoReloadCheckInterval > 0
                && autoReload
                && !IsFull
                && ticks - lastAutoReloadTick >= Props.autoReloadCheckInterval)
            {
                lastAutoReloadTick = ticks;
                TryAutoReload(ticks);
            }
        }

        /// <summary>
        /// 自动装填：只在“空闲的玩家殖民地小人”身上触发，避免打断征召、精神崩溃或正在进行的作业。
        /// 检查频率由 autoReloadCheckInterval 控制；找不到医药或无法开始时按 autoReloadRetryTicks 退避。
        /// </summary>
        private void TryAutoReload(int ticks)
        {
            if (ticks < autoReloadRetryUntil)
            {
                return;
            }
            Pawn wearer = Wearer;
            if (wearer == null || !wearer.Spawned || wearer.Dead || wearer.Downed || wearer.Drafted
                || wearer.InMentalState || wearer.Faction != Faction.OfPlayer)
            {
                return;
            }
            JobDef reloadDef = LatiosJobDefs.ReloadBiofoamInjector;
            if (reloadDef == null || wearer.CurJobDef == reloadDef)
            {
                return;
            }
            Thing medicine = FindMedicine(wearer);
            if (medicine == null)
            {
                autoReloadRetryUntil = ticks + Props.autoReloadRetryTicks;
                return;
            }
            if (!TryStartReloadJob(wearer, medicine))
            {
                autoReloadRetryUntil = ticks + Props.autoReloadRetryTicks;
            }
        }

        /// <summary>
        /// 检查穿戴者失血并止血。返回是否真的消耗了一次充能。
        /// 开销：一次 wearer/health 判空 + 一次缓存 float 比较，未超阈值时立即返回。
        /// </summary>
        public bool TryTrigger(bool announce = true)
        {
            if (charges <= 0)
            {
                return false;
            }
            Pawn wearer = Wearer;
            if (wearer == null || wearer.Dead || !wearer.Spawned)
            {
                return false;
            }
            if (wearer.health?.hediffSet == null || !wearer.health.CanBleed)
            {
                return false;
            }
            if (wearer.health.hediffSet.BleedRateTotal <= Props.bleedThreshold)
            {
                return false;
            }

            Hediff_Injury worst = FindWorstBleedingInjury(wearer);
            if (worst == null)
            {
                return false;
            }

            // 与"包扎"同样的效果，质量固定为 Props.tendQuality（75%）：
            // 立刻停住这处流血，伤口本身保留、按正常速度愈合。
            worst.Tended(Props.tendQuality, Props.tendQuality);
            wearer.health.Notify_HediffChanged(worst);
            charges--;

            if (announce && wearer.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    "LatiosArsenal_Biofoam_Triggered".Translate(wearer.LabelShort, worst.LabelCap, Charges, Props.maxCharges),
                    wearer,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
            return true;
        }

        private static Hediff_Injury FindWorstBleedingInjury(Pawn pawn)
        {
            Hediff_Injury worst = null;
            float worstBleed = 0f;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury)
                {
                    float bleed = injury.BleedRate;
                    if (bleed <= worstBleed)
                    {
                        continue;
                    }
                    if (!injury.TendableNow())
                    {
                        continue;
                    }
                    worstBleed = bleed;
                    worst = injury;
                }
            }
            return worst;
        }

        public void AddCharges(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            charges = Mathf.Clamp(Charges + amount, 0, Props.maxCharges);
        }

        /// <summary>由 JobDriver 在作业完成时调用：消耗一份医药并补充充能。</summary>
        public void FinishReload(Thing medicine)
        {
            if (medicine == null || medicine.Destroyed || medicine.stackCount <= 0)
            {
                return;
            }
            if (medicine.stackCount > 1 && medicine.def.stackLimit > 1)
            {
                Thing split = medicine.SplitOff(1);
                split?.Destroy(DestroyMode.Vanish);
            }
            else
            {
                medicine.Destroy(DestroyMode.Vanish);
            }
            AddCharges(Props.chargesPerMedicine);
        }

        /// <summary>先看随身携带，再找地图上最近且可到达的医药（只在点击按钮时调用）。</summary>
        public static Thing FindMedicine(Pawn pawn)
        {
            if (pawn.inventory?.innerContainer != null)
            {
                foreach (Thing thing in pawn.inventory.innerContainer)
                {
                    if (thing.def.IsMedicine && thing.stackCount > 0)
                    {
                        return thing;
                    }
                }
            }
            Map map = pawn.MapHeld;
            if (map == null)
            {
                return null;
            }
            return GenClosest.ClosestThing_Global_Reachable(
                pawn.PositionHeld,
                map,
                map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                t => t.def.IsMedicine
                     && t.stackCount > 0
                     && !t.IsForbidden(pawn)
                     && pawn.CanReserve(t, 1, -1, null, false));
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetWornGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn wearer = Wearer;
            if (wearer == null || wearer.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            Command_Action command = new Command_Action
            {
                defaultLabel = "LatiosArsenal_Biofoam_Recharge".Translate(Charges, Props.maxCharges),
                defaultDesc = "LatiosArsenal_Biofoam_RechargeDesc".Translate(Props.chargesPerMedicine),
                icon = ContentFinder<Texture2D>.Get("UI/Icons/Latios/ReloadBiofoamInjector", false),
                action = StartReloadJob
            };

            if (IsFull)
            {
                command.Disable("LatiosArsenal_Biofoam_Full".Translate());
            }
            else if (FindMedicine(wearer) == null)
            {
                command.Disable("LatiosArsenal_Biofoam_NoMedicine".Translate());
            }

            yield return command;

            // 自动装填开关：绿勾 = 已开启，红叉 = 已关闭
            yield return new Command_Action
            {
                defaultLabel = (autoReload
                    ? "LatiosArsenal_Biofoam_AutoReloadOn".Translate()
                    : "LatiosArsenal_Biofoam_AutoReloadOff".Translate()),
                defaultDesc = "LatiosArsenal_Biofoam_AutoReloadDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get(autoReload ? "UI/Icons/Latios/AutoReloadOn" : "UI/Icons/Latios/AutoReloadOff", false),
                action = ToggleAutoReload
            };
        }

        public override string CompInspectStringExtra()
        {
            return "LatiosArsenal_Biofoam_Charges".Translate(Charges, Props.maxCharges);
        }

        /// <summary>手动装填按钮：找药并下达装填作业。</summary>
        private void StartReloadJob()
        {
            Pawn wearer = Wearer;
            if (wearer == null || IsFull)
            {
                return;
            }
            Thing medicine = FindMedicine(wearer);
            if (medicine == null)
            {
                return;
            }
            if (!TryStartReloadJob(wearer, medicine))
            {
                Messages.Message("LatiosArsenal_Biofoam_CannotStart".Translate(wearer.LabelShort), wearer, MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        /// <summary>下达装填作业（手动与自动装填共用）。</summary>
        public bool TryStartReloadJob(Pawn wearer, Thing medicine)
        {
            if (wearer == null || medicine == null || IsFull)
            {
                return false;
            }
            JobDef jobDef = LatiosJobDefs.ReloadBiofoamInjector;
            if (jobDef == null)
            {
                return false;
            }
            Job job = JobMaker.MakeJob(jobDef, medicine, Belt);
            job.count = 1;
            return wearer.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}
