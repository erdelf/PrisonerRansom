using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Harmony;
using UnityEngine;

namespace PrisonerRansom
{
    using JetBrains.Annotations;

    public class RansomSettings : ModSettings
    {
        public int ransomFactor=2;
        public int ransomGoodwill=5;
        public int ransomGoodwillFail=-10;
        public int ransomFailChance=20;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(value: ref this.ransomFactor, label: "ransomFactor", defaultValue: 2);
            Scribe_Values.Look(value: ref this.ransomGoodwill, label: "ransomGoodwill", defaultValue: 5);
            Scribe_Values.Look(value: ref this.ransomGoodwillFail, label: "ransomGoodWillFail", defaultValue: -10);
            Scribe_Values.Look(value: ref this.ransomFailChance, label: "ransomFailChance", defaultValue: 20);
        }
    }

    [UsedImplicitly]
    internal class PrisonerRansom : Mod
    {
        private readonly RansomSettings settings;

        public PrisonerRansom(ModContentPack content) : base(content: content)
        {
            this.settings = this.GetSettings<RansomSettings>();
            ReplacementCode.settings = this.settings;
        }

        public override string SettingsCategory() => "Prisoner Ransom";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            this.settings.ransomFactor = (int) Widgets.HorizontalSlider(rect: inRect.TopHalf().TopHalf().TopPart(pct: 0.8f), value: this.settings.ransomFactor, leftValue: -5f, rightValue: 5f, middleAlignment: true, label: "Ransom amount factor: " + this.settings.ransomFactor + "\nDetermines the factor that the value of a prisoner is multiplied with", leftAlignedLabel: "5", rightAlignedLabel: "5");
            this.settings.ransomGoodwill = (int) Widgets.HorizontalSlider(rect: inRect.TopHalf().BottomHalf().TopPart(pct:0.8f), value: this.settings.ransomGoodwill, leftValue: -50f, rightValue: 50f, middleAlignment: true, label: "Goodwill effect on success: " + this.settings.ransomGoodwill + "\nDetermines the value the relationship get's affected with on success", leftAlignedLabel: "-50", rightAlignedLabel: "50");
            this.settings.ransomGoodwillFail = (int) Widgets.HorizontalSlider(rect: inRect.BottomHalf().TopHalf().TopPart(pct: 0.8f), value: this.settings.ransomGoodwillFail, leftValue: -50f, rightValue: 50f, middleAlignment: true, label: "Goodwill effect on failure: " + this.settings.ransomGoodwillFail + "\nDetermines the value the relationship get's affected with on failure", leftAlignedLabel: "-50", rightAlignedLabel: "50");
            this.settings.ransomFailChance = (int) Widgets.HorizontalSlider(rect: inRect.BottomHalf().BottomHalf().TopHalf(), value: this.settings.ransomFailChance, leftValue: 0f, rightValue: 100f, middleAlignment: true, label: "Chance of failure: " + this.settings.ransomFailChance + "\nDetermines the probability of a ransom failing", leftAlignedLabel: "0%", rightAlignedLabel: "100%");

            this.settings.Write();
        }
    }

    [StaticConstructorOnStartup]
    public static class ReplacementCode
    {
        public static RansomSettings settings;

        static ReplacementCode()
        {
            HarmonyInstance harmony = HarmonyInstance.Create(id: "rimworld.erdelf.prisoner_ransom");
            harmony.Patch(original: typeof(FactionDialogMaker).GetMethod(name: "FactionDialogFor"), prefix: null, postfix: new HarmonyMethod(type: typeof(ReplacementCode), name: nameof(FactionDialogForPostFix)));
        }
        
        public static void FactionDialogForPostFix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (faction.HostileTo(other: Faction.OfPlayer))
            {
                __result.options.Insert(index: 0, item: RansomPrisoner(faction: faction, negotiator: negotiator, map: negotiator.Map, original: __result));
            }
        }

        private static DiaOption RansomPrisoner(Faction faction, Pawn negotiator, Map map, DiaNode original)
        {
            IEnumerable<Pawn> prisoners = map.mapPawns.PrisonersOfColony.Where(predicate: p => p.Faction == faction).ToArray();
            DiaOption dia = new DiaOption(text: "Demand ransom for Prisoner");
            if (!prisoners.Any())
                dia.Disable(newDisabledReason: "No prisoners of this faction.");
            DiaNode diaNode = new DiaNode(text: "You have these Prisoners of this faction");
            foreach (Pawn p in prisoners)
            {
                int value = Mathf.RoundToInt(f: p.MarketValue * (faction.leader==p?4:settings.ransomFactor));
                DiaOption diaOption = new DiaOption(text: p.Name.ToStringFull + " (" + value + ")")
                {
                    action = delegate
                    {
                        if (Random.value + negotiator.skills.GetSkill(skillDef: SkillDefOf.Social).Level / 50f - 0.2 > (settings.ransomFailChance / 100f))
                        {
                            Messages.Message(text: "The faction delivered the ransom.", def: MessageTypeDefOf.PositiveEvent);
                            Thing silver = ThingMaker.MakeThing(def: ThingDefOf.Silver);
                            silver.stackCount = value;
                            TradeUtility.SpawnDropPod(dropSpot: DropCellFinder.TradeDropSpot(map: map), map: map, t: silver);

                            if (p.Spawned)
                            {
                                GenGuest.PrisonerRelease(p: p);
                                p.DeSpawn();
                            }
                            //TaleRecorder.RecordTale(TaleDefOf.SoldPrisoner);
                            faction.TryAffectGoodwillWith(other: Faction.OfPlayer, goodwillChange: faction.leader == p ? 50 : settings.ransomGoodwill);
                            Messages.Message(text: "You send " + (faction.leader == p ? "the leader of this Faction" : "You send your prisoner") + " back to his home (+" + (faction.leader == p ? 50 : settings.ransomGoodwill) + ")", def: MessageTypeDefOf.NeutralEvent);
                        }
                        else
                        {
                            Messages.Message(text: "The faction did not accept the ransom.", def: MessageTypeDefOf.NegativeEvent);
                            faction.TryAffectGoodwillWith(other: Faction.OfPlayer, goodwillChange: faction.leader == p ? -50 : settings.ransomGoodwillFail);
                            IncidentParms incidentParms = new IncidentParms()
                            {
                                faction = faction,
                                points = Rand.Range(min: value / 3, max: value / 2),
                                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                                target = map
                            };
                            IncidentDefOf.RaidEnemy.Worker.TryExecute(parms: incidentParms);
                        }
                    }
                };
                diaNode.options.Add(item: diaOption);
                diaOption.resolveTree = true;
            }

            diaNode.options.Add(new DiaOption("Go back") { link = original});
            dia.link = diaNode;
            return dia;
        }
    }
}