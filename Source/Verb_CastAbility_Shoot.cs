using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaWeaponsExpanded_Bioferrite;

/// <summary>
/// A verb that fires projectiles like a gun, but from an ability.<br/>
/// <br/>
/// This is almost a direct copy of the relevant methods within <c>Verb_LaunchProjectile</c>.<br/>
/// This was done due to <c>CompEquippableAbility</c>'s current inability
/// to support non-<c>Verb_CastAbility</c> verbs and the desire to emulate
/// <c>Verb_LaunchProjectile</c> behaviours.
/// </summary>
public class Verb_CastAbility_Shoot : Verb_CastAbility
{
    protected override int ShotsPerBurst => verbProps.burstShotCount;

    public virtual ThingDef Projectile
    {
        get
        {
            var compChangeableProjectile = EquipmentSource?.GetComp<CompChangeableProjectile>();
            return compChangeableProjectile is { Loaded: true }
                ? compChangeableProjectile.Projectile
                : verbProps.defaultProjectile;
        }
    }

    public override void WarmupComplete()
    {
        base.WarmupComplete();
        //normal ability flow activated here
        ability.Activate(currentTarget, currentDestination);
        Find.BattleLog.Add(new BattleLogEntry_RangedFire(caster,
            currentTarget.HasThing ? currentTarget.Thing : null,
            EquipmentSource?.def, Projectile, ShotsPerBurst > 1));
    }

    protected IntVec3 GetForcedMissTarget(float forcedMissRadius)
    {
        if (verbProps.forcedMissEvenDispersal)
        {
            if (forcedMissTargetEvenDispersalCache.Count <= 0)
            {
                forcedMissTargetEvenDispersalCache.AddRange(
                    GenerateEvenDispersalForcedMissTargets(
                        currentTarget.Cell, forcedMissRadius, burstShotsLeft));
                forcedMissTargetEvenDispersalCache.SortByDescending(p =>
                    p.DistanceToSquared(Caster.Position));
            }

            if (forcedMissTargetEvenDispersalCache.Count > 0)
            {
                return forcedMissTargetEvenDispersalCache.Pop();
            }
        }

        var maxExclusive = GenRadial.NumCellsInRadius(forcedMissRadius);
        var num = Rand.Range(0, maxExclusive);
        return currentTarget.Cell + GenRadial.RadialPattern[num];
    }

    private static IEnumerable<IntVec3> GenerateEvenDispersalForcedMissTargets(
        IntVec3 root, float radius, int count)
    {
        var randomRotationOffset = Rand.Range(0f, 360f);
        var goldenRatio = (1f + Mathf.Pow(5f, 0.5f)) / 2f;
        for (var i = 0; i < count; i++)
        {
            var f = (float)Math.PI * 2f * i / goldenRatio;
            var f2 = Mathf.Acos(1f - 2f * (i + 0.5f) / count);
            var num = (int)(Mathf.Cos(f) * Mathf.Sin(f2) * radius);
            var num2 = (int)(Mathf.Cos(f2) * radius);
            var vect = new Vector3(num, 0f, num2).RotatedBy(randomRotationOffset);
            yield return root + vect.ToIntVec3();
        }
    }

    protected override bool TryCastShot()
    {
        if (currentTarget.HasThing &&
            currentTarget.Thing.Map != caster.Map)
        {
            return false;
        }

        if (Projectile == null)
        {
            return false;
        }

        var flag = TryFindShootLineFromTo(caster.Position, currentTarget, out var resultingLine);
        if (verbProps.stopBurstWithoutLos && !flag)
        {
            return false;
        }

        if (EquipmentSource != null)
        {
            EquipmentSource.GetComp<CompChangeableProjectile>()
                ?.Notify_ProjectileLaunched();
            EquipmentSource.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
        }

        lastShotTick = Find.TickManager.TicksGame;
        var manningPawn = caster;
        Thing equipmentSource = EquipmentSource;
        var compMannable = caster.TryGetComp<CompMannable>();
        if (compMannable?.ManningPawn != null)
        {
            manningPawn = compMannable.ManningPawn;
            equipmentSource = caster;
        }

        var drawPos = caster.DrawPos;
        var projectile2 =
            (Projectile)GenSpawn.Spawn(Projectile, resultingLine.Source, caster.Map);
        if (verbProps.ForcedMissRadius > 0.5f)
        {
            var num = verbProps.ForcedMissRadius;
            if (manningPawn is Pawn pawn)
            {
                num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
            }

            var num2 = VerbUtility.CalculateAdjustedForcedMiss(num,
                currentTarget.Cell - caster.Position);
            if (num2 > 0.5f)
            {
                var forcedMissTarget = GetForcedMissTarget(num2);
                if (forcedMissTarget != currentTarget.Cell)
                {
                    ThrowDebugText("ToRadius");
                    ThrowDebugText("Rad\nDest", forcedMissTarget);
                    var projectileHitFlags =
                        ProjectileHitFlags.NonTargetWorld;
                    if (Rand.Chance(0.5f))
                    {
                        projectileHitFlags = ProjectileHitFlags.All;
                    }

                    if (!canHitNonTargetPawnsNow)
                    {
                        projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                    }

                    projectile2.Launch(manningPawn, drawPos, forcedMissTarget,
                        currentTarget, projectileHitFlags, preventFriendlyFire,
                        equipmentSource);
                    return true;
                }
            }
        }

        var shotReport =
            ShotReport.HitReportFor(caster, this, currentTarget);
        var randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
        var targetCoverDef = randomCoverToMissInto?.def;
        if (verbProps.canGoWild &&
            !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
        {
            var flyOverhead = projectile2?.def?.projectile != null &&
                              projectile2.def.projectile.flyOverhead;
            resultingLine.ChangeDestToMissWild(
                shotReport.AimOnTargetChance_StandardTarget, flyOverhead,
                caster.Map);
            ThrowDebugText(
                "ToWild" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
            ThrowDebugText("Wild\nDest", resultingLine.Dest);
            var projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
            if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
            {
                projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
            }

            projectile2.Launch(manningPawn, drawPos, resultingLine.Dest,
                currentTarget, projectileHitFlags2, preventFriendlyFire,
                equipmentSource, targetCoverDef);
            return true;
        }

        if (currentTarget.Thing != null &&
            currentTarget.Thing.def.CanBenefitFromCover &&
            !Rand.Chance(shotReport.PassCoverChance))
        {
            ThrowDebugText("ToCover" +
                                (canHitNonTargetPawnsNow ? "\nchntp" : ""));
            ThrowDebugText("Cover\nDest", randomCoverToMissInto.Position);
            var projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
            if (canHitNonTargetPawnsNow)
            {
                projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
            }

            projectile2.Launch(manningPawn, drawPos, randomCoverToMissInto,
                currentTarget, projectileHitFlags3, preventFriendlyFire,
                equipmentSource, targetCoverDef);
            return true;
        }

        var projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
        if (canHitNonTargetPawnsNow)
        {
            projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
        }

        if (!currentTarget.HasThing ||
            currentTarget.Thing.def.Fillage == FillCategory.Full)
        {
            projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
        }

        ThrowDebugText("ToHit" + (canHitNonTargetPawnsNow ? "\nchntp" : ""));
        if (currentTarget.Thing != null)
        {
            projectile2.Launch(manningPawn, drawPos, currentTarget,
                currentTarget, projectileHitFlags4, preventFriendlyFire,
                equipmentSource, targetCoverDef);
            ThrowDebugText("Hit\nDest", currentTarget.Cell);
        }
        else
        {
            projectile2.Launch(manningPawn, drawPos, resultingLine.Dest,
                currentTarget, projectileHitFlags4, preventFriendlyFire,
                equipmentSource, targetCoverDef);
            ThrowDebugText("Hit\nDest", resultingLine.Dest);
        }

        return true;
    }

    private void ThrowDebugText(string text)
    {
        if (DebugViewSettings.drawShooting)
        {
            MoteMaker.ThrowText(caster.DrawPos, caster.Map, text);
        }
    }

    private void ThrowDebugText(string text, IntVec3 c)
    {
        if (DebugViewSettings.drawShooting)
        {
            MoteMaker.ThrowText(c.ToVector3Shifted(), caster.Map, text);
        }
    }

    public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
    {
        needLOSToCenter = true;
        var projectile = Projectile;
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

    public override bool Available()
    {
        if (!base.Available())
        {
            return false;
        }

        if (CasterIsPawn)
        {
            var casterPawn = CasterPawn;
            if (casterPawn.Faction != Faction.OfPlayer &&
                !verbProps.ai_ProjectileLaunchingIgnoresMeleeThreats &&
                casterPawn.mindState.MeleeThreatStillThreat &&
                casterPawn.mindState.meleeThreat.Position.AdjacentTo8WayOrInside(
                    casterPawn.Position))
            {
                return false;
            }
        }

        return Projectile != null;
    }

    public override void Reset()
    {
        base.Reset();
        forcedMissTargetEvenDispersalCache.Clear();
    }

    private List<IntVec3> forcedMissTargetEvenDispersalCache = [];
}