using RimWorld;
using RimWorld.Utility;
using Verse;

namespace VanillaWeaponsExpanded_Bioferrite;

/// <summary>
/// This is almost a direct copy of the relevant methods within <c>Verb_LaunchProjectileStaticPsychic</c>.<br/><br/>
/// See <see cref="Verb_CastAbility_Shoot"/> for more information why.
/// </summary>
public class Verb_CastAbility_ShootStaticPsychic : Verb_CastAbility_Shoot
{
    public override void DrawHighlight(LocalTargetInfo target)
    {
        if (verbProps.range > 0f)
        {
            verbProps.DrawRadiusRing(caster.Position, this);
        }

        if (CanHitTarget(target) && IsApplicableTo(target))
        {
            if (target.IsValid)
            {
                GenDraw.DrawTargetHighlightWithLayer(target.CenterVector3,
                    AltitudeLayer.MetaOverlays);
                GenDraw.DrawRadiusRing(target.Cell,
                    verbProps.defaultProjectile.projectile.explosionRadius,
                    RadiusHighlightColor);
            }
            else
            {
                GenDraw.DrawTargetHighlightWithLayer(target.CenterVector3,
                    AltitudeLayer.MetaOverlays);
            }
        }

        if (target.IsValid)
        {
            ability.DrawEffectPreviews(target);
        }
    }

    public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
    {
        return base.ValidateTarget(target, showMessages) &&
               ReloadableUtility.CanUseConsideringQueuedJobs(CasterPawn, EquipmentSource);
    }

    public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
    {
        needLOSToCenter = true;
        var projectile = verbProps.defaultProjectile;
        if (projectile == null)
        {
            return 0f;
        }

        var num = projectile.projectile.explosionRadius +
                  projectile.projectile.explosionRadiusDisplayPadding;
        var forcedMissRadius = verbProps.ForcedMissRadius;
        if (forcedMissRadius > 0f && verbProps.burstShotCount > 1)
        {
            num += forcedMissRadius;
        }

        return num;
    }

    public override void OnGUI(LocalTargetInfo target)
    {
        base.OnGUI(target);

        var num = HighlightFieldRadiusAroundTarget(out _);
        if (caster.Spawned && target.IsValid && CanHitTarget(target))
        {
            foreach (var intVec in DamageDefOf.Bomb.Worker.ExplosionCellsToHit(
                         target.Cell, Find.CurrentMap, num))
            {
                if (!intVec.Fogged(Find.CurrentMap))
                {
                    var firstPawn = intVec.GetFirstPawn(Find.CurrentMap);
                    if (firstPawn != null &&
                        firstPawn.GetStatValue(StatDefOf.PsychicSensitivity) <
                        float.Epsilon && !firstPawn.IsHiddenFromPlayer())
                    {
                        Verb_CastPsycast.DrawIneffectiveWarningStatic(firstPawn);
                    }
                }
            }
        }
    }
}