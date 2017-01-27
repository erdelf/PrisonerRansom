using HugsLib;
using HugsLib.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrisonerRansom
{
    class RansomSettings : ModBase
    {
        public override string ModIdentifier
        {
            get
            {
                return "PrisonerRansom";
            }
        }

        public static SettingHandle<float> ransomFactor;
        public static SettingHandle<float> ransomGoodwill;
        public static SettingHandle<float> ransomGoodwillFail;
        public static SettingHandle<float> ransomFailChance;

        public override void DefsLoaded()
        {
            ransomFactor = Settings.GetHandle<float>("ransomFactor", "Ransom amount factor", "Determines the factor that the value of a prisoner is multiplied with", 2f);
            ransomGoodwill = Settings.GetHandle<float>("ransomGoodwill", "Goodwill effect on success", "Determines the value the relationship get's affected with on success", 5f);
            ransomGoodwillFail = Settings.GetHandle<float>("ransomGoodwillFail", "Goodwill effect on failure", "Determines the value the relationship get's affected with on failure", -10f);
            ransomFailChance = Settings.GetHandle<float>("ransomFailureChance", "Chance of failure", "Determines the probability of a ransom failing", 20f);
        }
    }
}
