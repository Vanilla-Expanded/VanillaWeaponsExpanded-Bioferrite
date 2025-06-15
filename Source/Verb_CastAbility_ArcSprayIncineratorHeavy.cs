using VEF.Weapons;
using RimWorld;
using Verse;

namespace VanillaWeaponsExpanded_Bioferrite;

public class Verb_CastAbility_ArcSprayIncineratorHeavy : Verb_CastAbility_ArcSprayIncinerator
{
    protected override ThingDef IncineratorBurstMote =>
        BioferriteDefOf.VWEB_Mote_Blue_IncineratorBurst;

    protected override bool TryCastShot()
    {
        bool num = base.TryCastShot();
        if (num && CasterIsPawn)
        {
            CasterPawn.records.Increment(RecordDefOf.ShotsFired);
        }

        if (num && this.EquipmentSource.def.HasModExtension<HeavyWeapon>())
        {
            var options = this.EquipmentSource.def.GetModExtension<HeavyWeapon>();
            if (options.weaponHitPointsDeductionOnShot > 0)
            {
                this.EquipmentSource.HitPoints -= options.weaponHitPointsDeductionOnShot;
                if (this.EquipmentSource.HitPoints <= 0)
                {
                    this.EquipmentSource.Destroy();
                    if (CasterIsPawn)
                    {
                        CasterPawn.jobs.StopAll();
                    }
                }
            }
        }

        return num;
    }
}