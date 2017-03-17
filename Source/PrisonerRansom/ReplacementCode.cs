using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using HugsLib;
using HugsLib.Settings;
using Harmony;

namespace PrisonerRansom
{

    [StaticConstructorOnStartup]
    public static class ReplacementCode
    {
        static ReplacementCode()
        {
            // Thank god Zhentar
            LongEventHandler.QueueLongEvent(() =>
            {
                ransomFactor = () => 2f;
                ransomGoodwill = () => 5f;
                ransomGoodwillFail = () => -10f;
                ransomFailChance = () => 20f;

                HarmonyInstance harmony = HarmonyInstance.Create("rimworld.erdelf.prisoner_ransom");
                harmony.Patch(typeof(FactionDialogMaker).GetMethod("FactionDialogFor"), null, new HarmonyMethod(typeof(ReplacementCode), nameof(FactionDialogForPostFix)));

                try
                {   //Need a wrapper method/lambda to be able to catch the TypeLoadException when HugsLib isn't present
                    ((Action)(() =>
                    {

                        ModSettingsPack settings = HugsLibController.Instance.Settings.GetModSettings("PrisonerRansom");
                        //handle can't be saved as a SettingHandle<> type; otherwise the compiler generated closure class will throw a typeloadexception

                        settings.EntryName = "PrisonerRansom";

                        object factor = settings.GetHandle<float>("ransomFactor", "Ransom amount factor", "Determines the factor that the value of a prisoner is multiplied with", 2f);
                        object goodwill = settings.GetHandle<float>("ransomGoodwill", "Goodwill effect on success", "Determines the value the relationship get's affected with on success", 5f);
                        object goodwillFail = settings.GetHandle<float>("ransomGoodwillFail", "Goodwill effect on failure", "Determines the value the relationship get's affected with on failure", -10f);
                        object failChance = settings.GetHandle<float>("ransomFailureChance", "Chance of failure", "Determines the probability of a ransom failing", 20f);

                        ransomFactor = () => (SettingHandle<float>)factor;
                        ransomGoodwill = () => (SettingHandle<float>)goodwill;
                        ransomGoodwillFail = () => (SettingHandle<float>)goodwillFail;
                        ransomFailChance = () => (SettingHandle<float>)failChance;
                        return;
                    }))();
                } 
                catch (TypeLoadException)
                { }
            }, "queueHugsLibPrisonerRansom", false, null);
        }

        public static Func<float> ransomFactor;
        public static Func<float> ransomGoodwill;
        public static Func<float> ransomGoodwillFail;
        public static Func<float> ransomFailChance;
        
        public static void FactionDialogForPostFix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (faction.HostileTo(Faction.OfPlayer))
            {
                __result.options.Insert(0, RansomPrisoner(faction, negotiator, negotiator.Map));
            }
        }

        private static DiaOption RansomPrisoner(Faction faction, Pawn negotiator, Map map)
        {
            IEnumerable<Pawn> prisoners = (from p in map.mapPawns.PrisonersOfColony where p.Faction == faction select p);
            DiaOption dia = new DiaOption("Demand ransom for Prisoner");
            if (prisoners.Count() <= 0)
                dia.Disable("No prisoners of this faction.");
            DiaNode diaNode = new DiaNode("You have these Prisoners of this faction");
            foreach (Pawn p in prisoners)
            {
                int value = UnityEngine.Mathf.RoundToInt(p.MarketValue * (faction.leader==p?4:ransomFactor()));
                DiaOption diaOption = new DiaOption(p.Name.ToStringFull + " (" + value + ")");
                diaOption.action = delegate
                {
                    if (UnityEngine.Random.value + negotiator.skills.GetSkill(SkillDefOf.Social).Level/50 - 0.2  > (ransomFailChance()/100f))
                    {
                        Messages.Message("The faction delivered the ransom.", MessageSound.Benefit);
                        Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                        silver.stackCount = value;
                        TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map), map, silver);

                        if (p.Spawned)
                        {
                            GenGuest.PrisonerRelease(p);
                            p.DeSpawn();
                        }
                        //TaleRecorder.RecordTale(TaleDefOf.SoldPrisoner);
                        faction.AffectGoodwillWith(Faction.OfPlayer, faction.leader == p ? 50 : ransomGoodwill());
                        Messages.Message("You send " + (faction.leader == p ? "the leader of this Faction" : "You send your prisoner") + " back to his home (+" + (faction.leader == p ? 50 : ransomGoodwill()) + ")", MessageSound.Standard);
                    }
                    else
                    {
                        Messages.Message("The faction did not accept the ransom.", MessageSound.Negative);
                        faction.AffectGoodwillWith(Faction.OfPlayer, faction.leader == p ? -50 : ransomGoodwillFail());
                        IncidentParms incidentParms = new IncidentParms();
                        incidentParms.faction = faction;
                        incidentParms.points = (float)Rand.Range(value/3, value/2);
                        incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                        incidentParms.target = map;
                        IncidentDefOf.RaidEnemy.Worker.TryExecute(incidentParms);
                    }
                };
                diaNode.options.Add(diaOption);
                diaOption.resolveTree = true;
            }
            dia.link = diaNode;
            return dia;
        }
    }
}