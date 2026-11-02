using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LatiosArsenal
{
    /// <summary>
    /// JobDef 属于“医疗 - 生物泡沫”内容包，关闭该包时 Def 不存在，
    /// 因此不用 [DefOf]（缺 Def 会在启动时报错），改为惰性解析 + 空值保护。
    /// </summary>
    public static class LatiosJobDefs
    {
        private static JobDef reloadBiofoamInjector;

        public static JobDef ReloadBiofoamInjector =>
            reloadBiofoamInjector ??=
                DefDatabase<JobDef>.GetNamedSilentFail("Latios_ReloadBiofoamInjector");
    }

    /// <summary>
    /// 装填生物泡沫注射器：走到医药处（随身携带则原地）→ 花时间装填 → 消耗 1 份医药、+2 充能。
    /// TargetA = 医药，TargetB = 注射器本体（腰带装备）。
    /// </summary>
    public class JobDriver_ReloadBiofoamInjector : JobDriver
    {
        private const TargetIndex MedicineInd = TargetIndex.A;
        private const TargetIndex InjectorInd = TargetIndex.B;

        private Thing Medicine => job.GetTarget(MedicineInd).Thing;

        private Apparel InjectorApparel => job.GetTarget(InjectorInd).Thing as Apparel;

        private CompBiofoamInjector Injector => InjectorApparel?.TryGetComp<CompBiofoamInjector>();

        private bool MedicineCarried => Medicine != null && Medicine.ParentHolder is Pawn_InventoryTracker;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Thing medicine = Medicine;
            if (medicine == null || Injector == null)
            {
                return false;
            }
            if (MedicineCarried)
            {
                return true;
            }
            return pawn.Reserve(medicine, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFailCondition(() => Injector == null || Injector.IsFull);

            if (!MedicineCarried)
            {
                this.FailOnDestroyedOrNull(MedicineInd);
                this.FailOnForbidden(MedicineInd);
                yield return Toils_Goto.GotoThing(MedicineInd, PathEndMode.ClosestTouch);
            }
            else
            {
                // 随身携带的医药：不用走路，但仍然要花时间装填
                this.AddFailCondition(() => Medicine == null || Medicine.Destroyed || Medicine.stackCount <= 0);
            }

            int workTicks = Injector != null ? Injector.Props.reloadWorkTicks : 240;
            Toil wait = Toils_General.Wait(Mathf.Max(30, workTicks));
            wait.WithProgressBarToilDelay(MedicineInd, true);
            yield return wait;

            yield return Toils_General.Do(() =>
            {
                CompBiofoamInjector injector = Injector;
                if (injector != null)
                {
                    injector.FinishReload(Medicine);
                }
            });
        }
    }
}
