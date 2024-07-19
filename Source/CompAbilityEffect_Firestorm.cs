using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaWeaponsExpanded_Bioferrite;

public class CompProperties_AbilityFirestorm : CompProperties_AbilityBurner
{
	public CompProperties_AbilityFirestorm()
	{
		compClass = typeof(CompAbilityEffect_Firestorm);
	}
}

/// <summary>
/// A copy of CompAbilityEffect_Burner, made due to the mote being hardcoded.
/// </summary>
public class CompAbilityEffect_Firestorm : CompAbilityEffect_Burner
{
    public new CompProperties_AbilityBurner Props => (CompProperties_AbilityBurner)base.props;

	public override IEnumerable<PreCastAction> GetPreCastActions()
	{
		yield return new PreCastAction
		{
			action = delegate(LocalTargetInfo a, LocalTargetInfo _)
			{
				Vector3 drawPos = base.parent.pawn.DrawPos;
				IntVec3 intVec = drawPos.Yto0().ToIntVec3();
				Map map = base.parent.pawn.Map;
				IncineratorSpray incineratorSpray = GenSpawn.Spawn(ThingDefOf.IncineratorSpray, intVec, map) as IncineratorSpray;
				int numStreams = this.Props.numStreams;
				Vector3 normalized = (a.CenterVector3 - drawPos).normalized;
				for (int i = 0; i < numStreams; i++)
				{
					float angle = Rand.Range(0f - this.Props.coneSizeDegrees, this.Props.coneSizeDegrees);
					Vector3 vector = normalized.RotatedBy(angle);
					Vector3 vect = drawPos + vector * (this.Props.range);
					IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(intVec, vect.ToIntVec3(), (IntVec3 c) => c.CanBeSeenOverFast(map), skipFirstCell: true);
					if (!intVec2.IsValid)
					{
						intVec2 = vect.ToIntVec3();
					}
					float num = Vector3.Distance(intVec2.ToVector3(), drawPos);
					float num2 = Mathf.Clamp01(num / this.Props.sizeReductionDistanceThreshold);
					if (!(Vector3.Dot((intVec2.ToVector3() - drawPos).normalized, vector) <= 0.5f))
					{
						MoteDualAttached mote = MoteMaker.MakeInteractionOverlay(Props.moteDef, new TargetInfo(intVec, map), new TargetInfo(intVec2, map));
						incineratorSpray.Add(new IncineratorProjectileMotion
						{
							mote = mote,
							targetDest = a.Cell,
							worldSource = drawPos + vector * this.Props.barrelOffsetDistance,
							worldTarget = intVec2.ToVector3(),
							moveVector = vector,
							startScale = Rand.Range(0.8f, 1f) * num2,
							endScale = (1f) * num2,
							lifespanTicks = Mathf.FloorToInt(num * 5f) + Rand.Range(-this.Props.lifespanNoise, this.Props.lifespanNoise)
						});
						map.effecterMaintainer.AddEffecterToMaintain(this.Props.effecterDef.Spawn(intVec2, map), intVec2, 100);
					}
				}
			},
			ticksAwayFromCast = 5
		};
	}
}