using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace PrisonerRansom
{
    using System.Globalization;
    using JetBrains.Annotations;

    public class RansomSettings : ModSettings
    {
        public static RansomSettings settings;

        public int ransomFactor       = 2;
        public int adjustment         = 81;

        public int ransomRaidDelay    = GenDate.TicksPerDay * 2;
        public int ransomFailCooldown = GenDate.TicksPerDay * 3;


        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(value: ref this.ransomFactor, label: "ransomFactor", defaultValue: 2);
            Scribe_Values.Look(value: ref this.adjustment,   label: "adjustment",   defaultValue: 81);
            Scribe_Values.Look(value: ref this.ransomRaidDelay,     label: "ransomRaidDelay",     defaultValue: GenDate.TicksPerDay * 2);
            Scribe_Values.Look(value: ref this.ransomFailCooldown, label: "ransomRaidFailCooldown", defaultValue: GenDate.TicksPerDay * 3);
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

        public PrisonerRansom(ModContentPack content) : base(content: content)
        {
            this.settings = this.GetSettings<RansomSettings>();
            RansomSettings.settings = this.settings;
        }

        public override string SettingsCategory() => "Prisoner Ransom";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            int ransomRaidDelay = Mathf.RoundToInt(this.settings.ransomRaidDelay / (float) GenDate.TicksPerHour);
            int ransomFailCooldown = Mathf.RoundToInt(this.settings.ransomFailCooldown / (float)GenDate.TicksPerHour);

            Rect sliderSection = inRect.TopPart(0.7f);

            this.settings.ransomFactor = (int) Widgets.HorizontalSlider(rect: sliderSection.TopHalf().TopHalf().BottomHalf(), value: this.settings.ransomFactor, leftValue: 1f, rightValue: 5f, middleAlignment: true, label: "SettingsRansomFactor".Translate(this.settings.ransomFactor), leftAlignedLabel: "1", rightAlignedLabel: "5");
            ransomRaidDelay = (int) Widgets.HorizontalSlider(rect: sliderSection.TopHalf().BottomHalf().TopHalf(), value: ransomRaidDelay, leftValue: 1f, rightValue: 168f, middleAlignment: true, label: "SettingsRansomRaidDelay".Translate(this.settings.ransomRaidDelay.ToStringTicksToPeriod()), leftAlignedLabel: "1", rightAlignedLabel: "168");
            ransomFailCooldown = (int) Widgets.HorizontalSlider(rect: sliderSection.BottomHalf().TopHalf().TopHalf(),value: ransomFailCooldown, leftValue: ransomRaidDelay, rightValue: 336f, middleAlignment: true, label: "SettingsRansomFailCooldown".Translate(this.settings.ransomFailCooldown.ToStringTicksToPeriod()), leftAlignedLabel: ransomRaidDelay.ToString(), rightAlignedLabel: "336");
            this.settings.adjustment = (int) Widgets.HorizontalSlider(rect: sliderSection.BottomHalf().BottomHalf().TopHalf(), value: this.settings.adjustment, leftValue: 40f, rightValue: 95f, middleAlignment: true, label: "SettingsRansomAdjustment".Translate(this.settings.adjustment), leftAlignedLabel: "40", rightAlignedLabel: "95");

            this.settings.ransomRaidDelay = ransomRaidDelay * GenDate.TicksPerHour;
            this.settings.ransomFailCooldown = ransomFailCooldown * GenDate.TicksPerHour;

            SimpleCurve curve = new SimpleCurve();
            for (int i = -50; i <= 50; i++) 
                curve.Add(i, RansomSettings.RansomChanceRaw(-75, 10, i) * 100);

            SimpleCurveDrawInfo drawInfo = new SimpleCurveDrawInfo()
                                           {
                                               curve  = curve,
                                               color  = Color.cyan,
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
            Harmony harmony = new Harmony(id: "rimworld.erdelf.prisoner_ransom");
            harmony.Patch(original: typeof(FactionDialogMaker).GetMethod(name: "FactionDialogFor"), prefix: null, postfix: new HarmonyMethod(typeof(ReplacementCode), nameof(FactionDialogForPostFix)));
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
            DiaOption dia = new DiaOption(text: "DialogRansomDemand".Translate());
            if (!prisoners.Any())
                dia.Disable(newDisabledReason: "DialogRansomNoPrisoners".Translate());
            DiaNode diaNode = new DiaNode(text: "DialogRansomPrisonerList".Translate());
            foreach (Pawn p in prisoners)
            {
                int value = Mathf.RoundToInt(f: RansomSettings.MarketValue(p));

                DiaOption diaOption = new DiaOption(text: p.Name.ToStringFull + " (" + value + ")")
                                      {
                                          resolveTree = true,
                                          action = () => Find.WindowStack.Add(new Page_PrisonerRansom(p, negotiator))
                                      };
                
                diaNode.options.Add(item: diaOption);
            }

            diaNode.options.Add(new DiaOption("GoBack".Translate()) { link = original});
            dia.link = diaNode;
            return dia;
        }
    }

    public class Page_PrisonerRansom : Page
    {
        private readonly Pawn prisoner;
        private readonly Pawn handler;
        private float percentage;

        public Page_PrisonerRansom(Pawn prisoner, Pawn handler)
        {
            this.prisoner = prisoner;
            this.handler = handler;
        }

        public override Vector2 InitialSize => new Vector2(400, 250);

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

            listingStandard.Label(label: $"RansomDemandAmount".Translate(RansomSettings.MarketValuePercentage(this.prisoner, this.percentage).ToString(CultureInfo.CurrentCulture), ThingDefOf.Silver.LabelCap));
            
            this.percentage = listingStandard.Slider(this.percentage, -50f, 50f);
            listingStandard.Gap();
            listingStandard.Label(label: $"RansomDemandChance".Translate(Mathf.RoundToInt(RansomSettings.RansomChance(this.prisoner, this.handler, this.percentage) * 100).ToString(CultureInfo.CurrentCulture)));
            if (listingStandard.ButtonText("RansomSendOffer".Translate()))
            {
                Faction faction = this.prisoner.Faction;
                if (Rand.Value < RansomSettings.RansomChance(this.prisoner, this.handler, this.percentage))
                {
                    Messages.Message(text: "RansomFactionDeliveredMessage".Translate(), def: MessageTypeDefOf.PositiveEvent);
                    Thing silver = ThingMaker.MakeThing(def: ThingDefOf.Silver);
                    silver.stackCount = RansomSettings.MarketValuePercentage(this.prisoner, this.percentage);
                    TradeUtility.SpawnDropPod(dropSpot: DropCellFinder.TradeDropSpot(map: this.prisoner.Map), map: this.prisoner.Map, t: silver);

                    if (this.prisoner.Spawned)
                    {
                        GenGuest.PrisonerRelease(p: this.prisoner);
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

                    incidentParms.faction = faction;
                    incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;

                    Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + RansomSettings.settings.ransomRaidDelay, incidentParms, RansomSettings.settings.ransomRaidDelay);
                }
                this.Close();
            }

            if(listingStandard.ButtonText("Back".Translate()))
                this.Close();
        }
    }
}