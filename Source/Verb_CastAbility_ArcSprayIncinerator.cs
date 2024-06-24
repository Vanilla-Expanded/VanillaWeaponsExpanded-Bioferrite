using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaWeaponsExpanded_Bioferrite;

/// <summary>
/// This is almost a direct copy of the relevant methods within <c>Verb_ArcSprayIncinerator</c>.<br/><br/>
/// See <see cref="Verb_CastAbility_Shoot"/> for more information why.
/// </summary>
public class Verb_CastAbility_ArcSprayIncinerator : Verb_CastAbility
{
    public new ThingWithComps EquipmentSource => (caster as Pawn)?.equipment.Primary;
    protected override int ShotsPerBurst => verbProps.burstShotCount;

    public float ShotProgress =>
        ticksToNextPathStep / (float)verbProps.ticksBetweenBurstShots;

    public Vector3 InterpolatedPosition
    {
        get
        {
            var vector = CurrentTarget.CenterVector3 - initialTargetPosition;
            return Vector3.Lerp(path[burstShotsLeft],
                       path[Mathf.Min(burstShotsLeft + 1, path.Count - 1)],
                       ShotProgress) +
                   vector;
        }
    }

    public override float? AimAngleOverride
    {
        get
        {
            if (state != VerbState.Bursting)
            {
                return null;
            }

            return (InterpolatedPosition - caster.DrawPos).AngleFlat();
        }
    }

    public override void DrawHighlight(LocalTargetInfo target)
    {
        base.DrawHighlight(target);
        CalculatePath(target.CenterVector3, tmpPath, tmpPathCells, false);
        foreach (var intVec in tmpPathCells)
        {
            ShootLine shootLine;
            var flag =
                TryFindShootLineFromTo(caster.Position, target, out shootLine);
            IntVec3 intVec2;
            if ((!verbProps.stopBurstWithoutLos || flag) &&
                TryGetHitCell(shootLine.Source, intVec, out intVec2))
            {
                tmpHighlightCells.Add(intVec2);
                if (verbProps.beamHitsNeighborCells)
                {
                    foreach (var intVec3 in GetBeamHitNeighbourCells(shootLine.Source,
                                 intVec2))
                    {
                        if (!tmpHighlightCells.Contains(intVec3))
                        {
                            tmpSecondaryHighlightCells.Add(intVec3);
                        }
                    }
                }
            }
        }

        tmpSecondaryHighlightCells.RemoveWhere(x =>
            tmpHighlightCells.Contains(x));
        if (tmpHighlightCells.Any())
        {
            GenDraw.DrawFieldEdges(tmpHighlightCells.ToList(),
                verbProps.highlightColor ?? Color.white);
        }

        if (tmpSecondaryHighlightCells.Any())
        {
            GenDraw.DrawFieldEdges(tmpSecondaryHighlightCells.ToList(),
                verbProps.secondaryHighlightColor ?? Color.white);
        }

        tmpHighlightCells.Clear();
        tmpSecondaryHighlightCells.Clear();
    }

    protected override bool TryCastShot()
    {
        var flag = BeamTryCastShot();
        var vector = InterpolatedPosition.Yto0();
        var intVec = vector.ToIntVec3();
        var vector2 = caster.DrawPos;
        var normalized = (vector - vector2).normalized;
        vector2 += normalized * BarrelOffset;
        var position = caster.Position;
        var moteDualAttached = MoteMaker.MakeInteractionOverlay(
            ThingDefOf.Mote_IncineratorBurst, new TargetInfo(position, caster.Map),
            new TargetInfo(intVec, caster.Map));
        var num = Vector3.Distance(vector, vector2);
        var num2 = num < BarrelOffset ? 0.5f : 1f;
        var incineratorSpray = sprayer;
        if (incineratorSpray == null)
        {
            return flag;
        }

        incineratorSpray.Add(new IncineratorProjectileMotion
        {
            mote = moteDualAttached,
            targetDest = intVec,
            worldSource = vector2,
            worldTarget = vector,
            moveVector = (vector - vector2).normalized,
            startScale = 1f * num2,
            endScale = (1f + Rand.Range(0.1f, 0.4f)) * num2,
            lifespanTicks = Mathf.FloorToInt(num * DistanceToLifetimeScalar)
        });
        return flag;
    }

    private bool BeamTryCastShot()
    {
        if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
        {
            return false;
        }

        ShootLine shootLine;
        var flag =
            TryFindShootLineFromTo(caster.Position, currentTarget, out shootLine);
        if (verbProps.stopBurstWithoutLos && !flag)
        {
            return false;
        }

        if (EquipmentSource != null)
        {
            var comp = EquipmentSource.GetComp<CompChangeableProjectile>();
            comp?.Notify_ProjectileLaunched();

            var comp2 = EquipmentSource.GetComp<CompApparelReloadable>();
            comp2?.UsedOnce();
        }

        lastShotTick = Find.TickManager.TicksGame;
        ticksToNextPathStep = verbProps.ticksBetweenBurstShots;
        var intVec = InterpolatedPosition.Yto0().ToIntVec3();
        if (!TryGetHitCell(shootLine.Source, intVec, out var intVec2))
        {
            return true;
        }

        HitCell(intVec2, shootLine.Source);
        if (verbProps.beamHitsNeighborCells)
        {
            hitCells.Add(intVec2);
            foreach (var intVec3 in GetBeamHitNeighbourCells(shootLine.Source,
                         intVec2))
            {
                if (!hitCells.Contains(intVec3))
                {
                    var num = pathCells.Contains(intVec3) ? 1f : 0.5f;
                    HitCell(intVec3, shootLine.Source, num);
                    hitCells.Add(intVec3);
                }
            }
        }

        return true;
    }

    protected bool TryGetHitCell(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell)
    {
        var intVec = GenSight.LastPointOnLineOfSight(source, targetCell,
            c => c.InBounds(caster.Map) && c.CanBeSeenOverFast(caster.Map),
            true);
        if (verbProps.beamCantHitWithinMinRange &&
            intVec.DistanceTo(source) < verbProps.minRange)
        {
            hitCell = default;
            return false;
        }

        hitCell = intVec.IsValid ? intVec : targetCell;
        return intVec.IsValid;
    }

    protected IEnumerable<IntVec3> GetBeamHitNeighbourCells(IntVec3 source, IntVec3 pos)
    {
        if (!verbProps.beamHitsNeighborCells)
        {
            yield break;
        }

        int num;
        for (var i = 0; i < 4; i = num + 1)
        {
            var intVec = pos + GenAdj.CardinalDirections[i];
            if (intVec.InBounds(Caster.Map) &&
                (!verbProps.beamHitsNeighborCellsRequiresLOS ||
                 GenSight.LineOfSight(source, intVec, caster.Map)))
            {
                yield return intVec;
            }

            num = i;
        }
    }

    public override bool TryStartCastOn(LocalTargetInfo castTarg,
        LocalTargetInfo destTarg, bool surpriseAttack = false,
        bool canHitNonTargetPawns = true, bool preventFriendlyFire = false,
        bool nonInterruptingSelfCast = false)
    {
        return base.TryStartCastOn(verbProps.beamTargetsGround ? castTarg.Cell : castTarg,
            destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire,
            nonInterruptingSelfCast);
    }

    public override void BurstingTick()
    {
        ticksToNextPathStep--;
        var vector = InterpolatedPosition;
        var intVec = vector.ToIntVec3();
        var vector2 = InterpolatedPosition - caster.Position.ToVector3Shifted();
        var num = vector2.MagnitudeHorizontal();
        var normalized = vector2.Yto0().normalized;
        var intVec2 = GenSight.LastPointOnLineOfSight(caster.Position, intVec,
            c => c.CanBeSeenOverFast(caster.Map), true);
        if (intVec2.IsValid)
        {
            num -= (intVec - intVec2).LengthHorizontal;
            vector = caster.Position.ToVector3Shifted() + normalized * num;
            intVec = vector.ToIntVec3();
        }

        var vector3 = normalized * verbProps.beamStartOffset;
        var vector4 = vector - intVec.ToVector3Shifted();
        if (mote != null)
        {
            mote.UpdateTargets(new TargetInfo(caster.Position, caster.Map),
                new TargetInfo(intVec, caster.Map), vector3, vector4);
            mote.Maintain();
        }

        if (verbProps.beamGroundFleckDef != null &&
            Rand.Chance(verbProps.beamFleckChancePerTick))
        {
            FleckMaker.Static(vector, caster.Map, verbProps.beamGroundFleckDef);
        }

        if (endEffecter == null && verbProps.beamEndEffecterDef != null)
        {
            endEffecter =
                verbProps.beamEndEffecterDef.Spawn(intVec, caster.Map, vector4);
        }

        if (endEffecter != null)
        {
            endEffecter.offset = vector4;
            endEffecter.EffectTick(new TargetInfo(intVec, caster.Map),
                TargetInfo.Invalid);
            endEffecter.ticksLeft--;
        }

        if (verbProps.beamLineFleckDef != null)
        {
            var num2 = 1f * num;
            var num3 = 0;
            while (num3 < num2)
            {
                if (Rand.Chance(
                        verbProps.beamLineFleckChanceCurve.Evaluate(num3 / num2)))
                {
                    var vector5 = num3 * normalized - normalized * Rand.Value +
                                  normalized / 2f;
                    FleckMaker.Static(caster.Position.ToVector3Shifted() + vector5,
                        caster.Map, verbProps.beamLineFleckDef);
                }

                num3++;
            }
        }

        var sustainer = this.sustainer;

        sustainer?.Maintain();
    }

    public override void WarmupComplete()
    {
        sprayer = GenSpawn.Spawn(ThingDefOf.IncineratorSpray, this.caster.Position,
            this.caster.Map) as IncineratorSpray;
        InternalWarmupComplete();
        var battleLog = Find.BattleLog;
        var caster = this.caster;
        var thing = currentTarget.HasThing ? currentTarget.Thing : null;

        Log.Message(EquipmentSource);
        var equipmentSource = EquipmentSource;
        battleLog.Add(new BattleLogEntry_RangedFire(caster, thing,
            equipmentSource?.def, null, false));
        
        //normal ability flow activated here
        ability.Activate(currentTarget, currentDestination);
    }

    private void InternalWarmupComplete()
    {
        burstShotsLeft = ShotsPerBurst;
        state = VerbState.Bursting;
        initialTargetPosition = currentTarget.CenterVector3;
        CalculatePath(currentTarget.CenterVector3, path, pathCells);
        hitCells.Clear();
        if (verbProps.beamMoteDef != null)
        {
            mote = MoteMaker.MakeInteractionOverlay(verbProps.beamMoteDef, caster,
                new TargetInfo(path[0].ToIntVec3(), caster.Map));
        }

        TryCastNextBurstShot();
        ticksToNextPathStep = verbProps.ticksBetweenBurstShots;
        var effecter = endEffecter;
        if (effecter != null)
        {
            effecter.Cleanup();
        }

        if (verbProps.soundCastBeam != null)
        {
            sustainer =
                verbProps.soundCastBeam.TrySpawnSustainer(
                    SoundInfo.InMap(caster, MaintenanceType.PerTick));
        }
    }

    private void CalculatePath(Vector3 target, List<Vector3> pathList,
        HashSet<IntVec3> pathCellsList, bool addRandomOffset = true)
    {
        pathList.Clear();
        var vector = (target - caster.Position.ToVector3Shifted()).Yto0();
        var magnitude = vector.magnitude;
        var normalized = vector.normalized;
        var vector2 = normalized.RotatedBy(-90f);
        var num = verbProps.beamFullWidthRange > 0f
            ? Mathf.Min(magnitude / verbProps.beamFullWidthRange, 1f)
            : 1f;
        var num2 = (verbProps.beamWidth + 1f) * num / ShotsPerBurst;
        var vector3 = target.Yto0() - vector2 * verbProps.beamWidth / 2f * num;

        pathList.Add(vector3);
        for (var i = 0; i < ShotsPerBurst; i++)
        {
            var vector4 = normalized * (Rand.Value * verbProps.beamMaxDeviation) -
                          normalized / 2f;
            var vector5 =
                Mathf.Sin((i / (float)ShotsPerBurst + 0.5f) * (float)Math.PI *
                          57.29578f) * verbProps.beamCurvature * -normalized -
                normalized * verbProps.beamMaxDeviation / 2f;
            if (addRandomOffset)
            {
                pathList.Add(vector3 + (vector4 + vector5) * num);
            }
            else
            {
                pathList.Add(vector3 + vector5 * num);
            }

            vector3 += vector2 * num2;
        }

        pathCellsList.Clear();
        foreach (var path in pathList)
        {
            pathCellsList.Add(path.ToIntVec3());
        }
    }

    private bool CanHit(Thing thing)
    {
        return thing.Spawned && !CoverUtility.ThingCovered(thing, caster.Map);
    }

    private void HitCell(IntVec3 cell, IntVec3 sourceCell, float damageFactor = 1f)
    {
        if (!cell.InBounds(caster.Map))
        {
            return;
        }

        ApplyDamage(
            VerbUtility.ThingsToHit(cell, caster.Map, CanHit)
                .RandomElementWithFallback(), sourceCell, damageFactor);
        if (verbProps.beamSetsGroundOnFire &&
            Rand.Chance(verbProps.beamChanceToStartFire))
        {
            FireUtility.TryStartFireIn(cell, caster.Map, 1f, caster);
        }
    }

    private void ApplyDamage(Thing thing, IntVec3 sourceCell, float damageFactor = 1f)
    {
        var intVec = InterpolatedPosition.Yto0().ToIntVec3();
        var intVec2 = GenSight.LastPointOnLineOfSight(sourceCell, intVec,
            c => c.InBounds(caster.Map) && c.CanBeSeenOverFast(caster.Map),
            true);
        if (intVec2.IsValid)
        {
            intVec = intVec2;
        }

        var map = caster.Map;
        if (thing != null && verbProps.beamDamageDef != null)
        {
            var angleFlat = (currentTarget.Cell - caster.Position).AngleFlat;
            var battleLogEntry_RangedImpact =
                new BattleLogEntry_RangedImpact(caster, thing, currentTarget.Thing,
                    EquipmentSource.def, null, null);
            DamageInfo damageInfo;
            if (verbProps.beamTotalDamage > 0f)
            {
                var num = verbProps.beamTotalDamage / pathCells.Count;
                num *= damageFactor;
                damageInfo = new DamageInfo(verbProps.beamDamageDef, num,
                    verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, caster,
                    null, EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown,
                    currentTarget.Thing);
            }
            else
            {
                var num2 = verbProps.beamDamageDef.defaultDamage * damageFactor;
                damageInfo = new DamageInfo(verbProps.beamDamageDef, num2,
                    verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, caster,
                    null, EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown,
                    currentTarget.Thing);
            }

            thing.TakeDamage(damageInfo).AssociateWithLog(battleLogEntry_RangedImpact);
            if (thing.CanEverAttachFire())
            {
                float num3;
                if (verbProps.flammabilityAttachFireChanceCurve != null)
                {
                    num3 = verbProps.flammabilityAttachFireChanceCurve.Evaluate(
                        thing.GetStatValue(StatDefOf.Flammability));
                }
                else
                {
                    num3 = verbProps.beamChanceToAttachFire;
                }

                if (Rand.Chance(num3))
                {
                    thing.TryAttachFire(verbProps.beamFireSizeRange.RandomInRange,
                        caster);
                }
            }
            else if (Rand.Chance(verbProps.beamChanceToStartFire))
            {
                FireUtility.TryStartFireIn(intVec, map,
                    verbProps.beamFireSizeRange.RandomInRange, caster,
                    verbProps.flammabilityAttachFireChanceCurve);
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref path, "path", LookMode.Value, []);
        Scribe_Values.Look(ref ticksToNextPathStep, "ticksToNextPathStep");
        Scribe_Values.Look(ref initialTargetPosition, "initialTargetPosition");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && path == null)
        {
            path = [];
        }
    }

    private List<Vector3> path = [];

    private List<Vector3> tmpPath = [];

    private int ticksToNextPathStep;

    private Vector3 initialTargetPosition;

    private MoteDualAttached mote;

    private Effecter endEffecter;

    private Sustainer sustainer;

    private HashSet<IntVec3> pathCells = [];

    private HashSet<IntVec3> tmpPathCells = [];

    private HashSet<IntVec3> tmpHighlightCells = [];

    private HashSet<IntVec3> tmpSecondaryHighlightCells = [];

    private HashSet<IntVec3> hitCells = [];

    [TweakValue("Incinerator", 0f, 10f)]
    public static float DistanceToLifetimeScalar = 5f;

    [TweakValue("Incinerator", -2f, 7f)] public static float BarrelOffset = 5f;

    private IncineratorSpray sprayer;
}