using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using HugsLib.GuiInject;
using HugsLib.Source.Detour;
using UnityEngine;

namespace PrisonerRansom
{
    internal static class ReplacementCode
    {
        
        [DetourMethod(typeof(FactionDialogMaker), "FactionDialogFor")]
        internal static DiaNode _FactionDialogFor(Pawn negotiator, Faction faction)
        {
            Map map = negotiator.Map;
            SetStaticField(typeof(FactionDialogMaker), "negotiator", negotiator);
            SetStaticField(typeof(FactionDialogMaker), "faction", faction);
            string text = (faction.leader != null) ? faction.leader.Name.ToStringFull : faction.Name;
            if (faction.PlayerGoodwill < -70f)
            {
                SetStaticField(typeof(FactionDialogMaker), "root", new DiaNode("FactionGreetingHostile".Translate(new object[]
                {
                    text
                })));
            }
            else if (faction.PlayerGoodwill < 40f)
            {
                string text2 = "FactionGreetingWary".Translate(new object[]
                {
                    text,
                    negotiator.LabelShort
                });
                text2 = text2.AdjustedFor(negotiator);
                SetStaticField(typeof(FactionDialogMaker), "root", new DiaNode(text2));
                if(!FactionBaseUtility.IsPlayerAttackingAnyFactionBaseOf(faction))
                    ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add(((DiaOption)InvokeMethod(typeof(FactionDialogMaker), "OfferGiftOption", null, map)));
                if(!faction.HostileTo(Faction.OfPlayer) && negotiator.Spawned && map.IsPlayerHome)
                    ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add(((DiaOption)InvokeMethod(typeof(FactionDialogMaker), "RequestTraderOption", null, null, new object[] { map, 600 })));
            }
            else
            {
                SetStaticField(typeof(FactionDialogMaker), "root", new DiaNode("FactionGreetingWarm".Translate(new object[]
                {
                    text,
                    negotiator.LabelShort
                })));
                if(!FactionBaseUtility.IsPlayerAttackingAnyFactionBaseOf(faction))
                    ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add((DiaOption)InvokeMethod(typeof(FactionDialogMaker), "OfferGiftOption", null, map));
                if (!faction.HostileTo(Faction.OfPlayer) && negotiator.Spawned && map.IsPlayerHome)
                {
                    ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add((DiaOption)InvokeMethod(typeof(FactionDialogMaker), "RequestTraderOption", null, null, new object[] {map, 300}));
                    ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add((DiaOption)InvokeMethod(typeof(FactionDialogMaker), "RequestMilitaryAidOption", null, map));
                }
            }
            //Log.Message("test: " + faction.HostileTo(Faction.OfPlayer));
            if(faction.HostileTo(Faction.OfPlayer))
                ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add(RansomPrisoner(faction, negotiator, map));
            if (Prefs.DevMode)
            {
                foreach (DiaOption current in (IEnumerable<DiaOption>)InvokeMethod(typeof(FactionDialogMaker), "DebugOptions"))
                {
                    ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add(current);
                }
            }
            DiaOption diaOption = new DiaOption("(" + "Disconnect".Translate() + ")");
            diaOption.resolveTree = true;
            ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root")).options.Add(diaOption);
            return ((DiaNode)GetStaticField(typeof(FactionDialogMaker), "root"));
        }

        internal static object GetStaticField(Type type, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(null);
        }

        internal static void SetStaticField(Type type, string fieldName, object value)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            field.SetValue(null, value);
        }

        internal static object InvokeMethod(Type type, string fieldName, object obj = null, object value = null, object[] values = null)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            MethodInfo method = type.GetMethod(fieldName, bindFlags);
            return method.Invoke(obj, values != null ? values : value != null ? new object[] { value } : null);
        }
        
        
        /*
        [WindowInjection(typeof(Dialog_Negotiation), Mode = WindowInjectionManager.InjectMode.AfterContents)]
        private static void RansomButton(Window window, Rect inRect)
        {
            Dialog_Negotiation dialog = window as Dialog_Negotiation;
            Faction faction = ((Faction)typeof(FactionDialogMaker).GetField("faction", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
            DiaNode root = ((DiaNode)typeof(FactionDialogMaker).GetField("root", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
            Pawn negotiator = ((Pawn)typeof(FactionDialogMaker).GetField("negotiator", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));

            if (dialog != null)
                if (((DiaNode)typeof(Dialog_NodeTree).GetField("curNode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dialog)) == root)
                    if (faction.HostileTo(Faction.OfPlayer))
                        if (!root.options.Any((DiaOption dia) => ((string)typeof(DiaOption).GetField("text", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dia)).Contains("ransom")))
                        {
                            root.options.Reverse();
                            root.options.Add(RansomPrisoner(faction, negotiator, negotiator.Map));
                            root.options.Reverse();
                        }
        }
        */
        private static DiaOption RansomPrisoner(Faction faction, Pawn negotiator, Map map)
        {
            IEnumerable<Pawn> prisoners = (from p in map.mapPawns.PrisonersOfColony where p.Faction == faction select p);
            DiaOption dia = new DiaOption("Demand ransom for Prisoner");
            if (prisoners.Count() <= 0)
                dia.Disable("No prisoners of this faction.");
            DiaNode diaNode = new DiaNode("You have these Prisoners of this faction");
            foreach (Pawn p in prisoners)
            {
                int value = UnityEngine.Mathf.RoundToInt(p.MarketValue * (faction.leader==p?4:RansomSettings.ransomFactor.Value));
                DiaOption diaOption = new DiaOption(p.Name.ToStringFull + " (" + value + ")");
                diaOption.action = delegate
                {
                    if (UnityEngine.Random.value + negotiator.skills.GetSkill(SkillDefOf.Social).Level/50 - 0.2  > (RansomSettings.ransomFailChance.Value/100f))
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
                        faction.AffectGoodwillWith(Faction.OfPlayer, faction.leader == p ? 50 : RansomSettings.ransomGoodwill.Value);
                        Messages.Message("You send " + (faction.leader == p ? "the leader of this Faction" : "You send your prisoner") + " back to his home (+" + (faction.leader == p ? 50 : RansomSettings.ransomGoodwill.Value) + ")", MessageSound.Standard);
                    }
                    else
                    {
                        Messages.Message("The faction did not accept the ransom.", MessageSound.Negative);
                        faction.AffectGoodwillWith(Faction.OfPlayer, faction.leader == p ? -50 : RansomSettings.ransomGoodwillFail.Value);
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
