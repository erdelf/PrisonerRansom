using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace PrisonerRansom
{
    using System;
    using System.Globalization;
    using JetBrains.Annotations;

    public class RansomSettings : ModSettings
    {
        public static RansomSettings settings;

        public int ransomFactor = 2;
        public int adjustment   = 81;

        public int ransomRaidDelay    = GenDate.TicksPerDay * 2;
        public int ransomFailCooldown = GenDate.TicksPerDay * 3;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.ransomFactor,       "ransomFactor",           2);
            Scribe_Values.Look(ref this.adjustment,         "adjustment",             81);
            Scribe_Values.Look(ref this.ransomRaidDelay,    "ransomRaidDelay",        GenDate.TicksPerDay * 2);
            Scribe_Values.Look(ref this.ransomFailCooldown, "ransomRaidFailCooldown", GenDate.TicksPerDay * 3);
        }

        public static float MarketValue(Pawn p) => p.MarketValue * settings.ransomFactor;

        public static int MarketValuePercentage(Pawn p, float percentage) => Mathf.RoundToInt(MarketValue(p) * (percentage / 100f + 1));

        public static float RansomChance(Pawn p, Pawn h, float percentage) =>
            RansomChanceRaw(p.Faction.PlayerGoodwill, h.skills.GetSkill(SkillDefOf.Social).Level, percentage);

        public static float RansomChanceRaw(int factionGoodwill, int skillLevel, float percentage) =>
            Mathf.Clamp01(0.01f * Mathf.Pow(1.1f, -percentage * (0.3f + factionGoodwill / 1000f) + settings.adjustment + skillLevel) / 100f);
    }

    [UsedImplicitly]
    internal class PrisonerRansom : Mod
    {
        private readonly RansomSettings settings;

        public PrisonerRansom(ModContentPack content) : base(content)
        {
            this.settings           = this.GetSettings<RansomSettings>();
            RansomSettings.settings = this.settings;
        }

        public override string SettingsCategory() => "Prisoner Ransom";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            int ransomRaidDelay    = Mathf.RoundToInt(this.settings.ransomRaidDelay    / (float)GenDate.TicksPerHour);
            int ransomFailCooldown = Mathf.RoundToInt(this.settings.ransomFailCooldown / (float)GenDate.TicksPerHour);

            Rect sliderSection = inRect.TopPart(0.7f);

            this.settings.ransomFactor = (int)Widgets.HorizontalSlider(sliderSection.TopHalf().TopHalf().BottomHalf(), this.settings.ransomFactor, 1f, 5f, true,
                                                                       "SettingsRansomFactor".Translate(this.settings.ransomFactor), "1", "5");
            ransomRaidDelay = (int)Widgets.HorizontalSlider(sliderSection.TopHalf().BottomHalf().TopHalf(), ransomRaidDelay, 1f, 168f, true,
                                                            "SettingsRansomRaidDelay".Translate(this.settings.ransomRaidDelay.ToStringTicksToPeriod()), "1", "168");
            ransomFailCooldown = (int)Widgets.HorizontalSlider(sliderSection.BottomHalf().TopHalf().TopHalf(), ransomFailCooldown, ransomRaidDelay, 336f, true,
                                                               "SettingsRansomFailCooldown".Translate(this.settings.ransomFailCooldown.ToStringTicksToPeriod()), ransomRaidDelay.ToString(), "336");
            this.settings.adjustment = (int)Widgets.HorizontalSlider(sliderSection.BottomHalf().BottomHalf().TopHalf(), this.settings.adjustment, 40f, 95f, true,
                                                                     "SettingsRansomAdjustment".Translate(this.settings.adjustment), "40", "95");

            this.settings.ransomRaidDelay    = ransomRaidDelay    * GenDate.TicksPerHour;
            this.settings.ransomFailCooldown = ransomFailCooldown * GenDate.TicksPerHour;

            SimpleCurve curve = new SimpleCurve();
            for (int i = -50; i <= 50; i++)
                curve.Add(i, RansomSettings.RansomChanceRaw(-75, 10, i) * 100);

            SimpleCurveDrawInfo drawInfo = new SimpleCurveDrawInfo()
                                           {
                                               curve       = curve,
                                               color       = Color.cyan,
                                               valueFormat = "{0}%"
                                           };

            SimpleCurveDrawer.DrawCurve(inRect.BottomPart(0.3f), drawInfo, new SimpleCurveDrawerStyle
                                                                           {
                                                                               DrawBackground           = false,
                                                                               DrawBackgroundLines      = false,
                                                                               DrawCurveMousePoint      = true,
                                                                               DrawLegend               = true,
                                                                               DrawMeasures             = true,
                                                                               DrawPoints               = false,
                                                                               FixedScale               = new Vector2(0, 100),
                                                                               FixedSection             = new FloatRange(-50, 50),
                                                                               LabelX                   = "Adj",
                                                                               MeasureLabelsXCount      = 10,
                                                                               MeasureLabelsYCount      = 10,
                                                                               OnlyPositiveValues       = false,
                                                                               PointsRemoveOptimization = false,
                                                                               UseAntiAliasedLines      = true,
                                                                               UseFixedScale            = true,
                                                                               UseFixedSection          = true,
                                                                               XIntegersOnly            = true,
                                                                               YIntegersOnly            = true
                                                                           });
        }
    }

    [StaticConstructorOnStartup]
    public static class ReplacementCode
    {

        static ReplacementCode()
        {
            Harmony harmony = new Harmony("rimworld.erdelf.prisoner_ransom");
            harmony.Patch(typeof(FactionDialogMaker).GetMethod(nameof(FactionDialogMaker.FactionDialogFor)), postfix: new HarmonyMethod(typeof(ReplacementCode), nameof(FactionDialogForPostFix)));
        }

        public static void FactionDialogForPostFix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (faction.HostileTo(Faction.OfPlayer))
            {
                DiaOption ransomPrisoner = RansomPrisoner(faction, negotiator, negotiator.Map, __result);
                if (Page_PrisonerRansom.linkingBack)
                {
                    Page_PrisonerRansom.linkingBack = false;
                    __result                        = ransomPrisoner.link;
                }
                else
                {
                    __result.options.Insert(0, ransomPrisoner);
                }
            }
        }

        private static DiaOption RansomPrisoner(Faction faction, Pawn negotiator, Map map, DiaNode original)
        {
            IEnumerable<Pawn> prisoners = map.mapPawns.PrisonersOfColony.Where(p => p.Faction == faction && p.CarriedBy == null).ToArray();
            DiaOption         dia       = new DiaOption("DialogRansomDemand".Translate());
            if (!prisoners.Any())
                dia.Disable("DialogRansomNoPrisoners".Translate());
            DiaNode diaNode = new DiaNode("DialogRansomPrisonerList".Translate());
            foreach (Pawn p in prisoners)
            {
                int value = Mathf.RoundToInt(RansomSettings.MarketValue(p));

                DiaOption diaOption = new DiaOption(p.Name.ToStringFull + " (" + value + ")")
                                      {
                                          resolveTree = true,
                                          action      = () => Find.WindowStack.Add(new Page_PrisonerRansom(p, negotiator))
                                      };

                diaNode.options.Add(diaOption);
            }

            diaNode.options.Add(new DiaOption("GoBack".Translate()) { link = original });
            dia.link = diaNode;
            return dia;
        }
    }

    public class Page_PrisonerRansom : Page
    {
        internal static bool linkingBack;

        private readonly Pawn  prisoner;
        private readonly Pawn  handler;
        private          float percentage;

        public Page_PrisonerRansom(Pawn prisoner, Pawn handler)
        {
            this.prisoner = prisoner;
            this.handler  = handler;
        }

        public override Vector2 InitialSize => new Vector2(400, 300);

        public override void PostOpen()
        {
            base.PostOpen();
            Find.TickManager.Pause();
        }

        public override void PostClose()
        {
            base.PostClose();
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.Label("RansomBargain".Translate(this.prisoner.Name.ToStringFull, this.prisoner.Faction.Name));
            listingStandard.Gap();
            listingStandard.Gap();
            listingStandard.Gap();

            listingStandard.Label($"RansomDemandAmount".Translate(RansomSettings.MarketValuePercentage(this.prisoner, this.percentage).ToString(CultureInfo.CurrentCulture),
                                                                  ThingDefOf.Silver.LabelCap));

            this.percentage = listingStandard.Slider(this.percentage, -50f, 50f);
            listingStandard.Gap();

            float ransomChance = RansomSettings.RansomChance(this.prisoner, this.handler, this.percentage);

            listingStandard.Label($"RansomDemandChance".Translate(Mathf.RoundToInt(ransomChance * 100)
                                                                       .ToString(CultureInfo.CurrentCulture)));

            listingStandard.Gap();
            if (listingStandard.ButtonText("RansomSendOffer".Translate()))
            {
                linkingBack = true;
                this.prisoner.Faction.TryOpenComms(this.handler);

                Faction faction = this.prisoner.Faction;
                if (Rand.Value < ransomChance)
                {
                    Messages.Message("RansomFactionDeliveredMessage".Translate(), MessageTypeDefOf.PositiveEvent);
                    Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = RansomSettings.MarketValuePercentage(this.prisoner, this.percentage);
                    TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(this.prisoner.Map), this.prisoner.Map, silver);

                    if (this.prisoner.Spawned)
                    {
                        GenGuest.PrisonerRelease(this.prisoner);
                        this.prisoner.DeSpawn();
                    }

                    //TaleRecorder.RecordTale(TaleDefOf.SoldPrisoner, this.handler, this.prisoner);
                    //faction.TryAffectGoodwillWith(other: Faction.OfPlayer, RansomSettings.settings.ransomGoodwill);

                    Find.WindowStack.Add(new Dialog_MessageBox("RansomPrisonerSend".Translate()));
                }
                else
                {
                    Find.WindowStack.Add(new Dialog_MessageBox("RansomNotAccepted".Translate()));
                    //faction.TryAffectGoodwillWith(other: Faction.OfPlayer, goodwillChange: RansomSettings.settings.ransomGoodwillFail);

                    IncidentParms incidentParms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, this.prisoner.Map);

                    incidentParms.faction      = faction;
                    incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

                    Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + RansomSettings.settings.ransomRaidDelay, incidentParms,
                                                       RansomSettings.settings.ransomRaidDelay);
                }

                this.Close();
            }

            if (listingStandard.ButtonText("Back".Translate()))
            {
                linkingBack = true;
                this.prisoner.Faction.TryOpenComms(this.handler);
                this.Close();
            }

            listingStandard.End();
        }
    }
}