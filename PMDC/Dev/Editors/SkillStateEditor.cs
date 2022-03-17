using System;
using System.Collections.Generic;
using System.Text;
using RogueEssence.Content;
using RogueEssence.Dungeon;
using PMDC.Dungeon;
using RogueEssence.Data;
using System.Drawing;
using RogueElements;
using Avalonia.Controls;
using RogueEssence.Dev.Views;
using System.Collections;
using Avalonia;
using System.Reactive.Subjects;

namespace RogueEssence.Dev
{
    public class BasePowerStateEditor : Editor<BasePowerState>
    {
        public override string GetString(BasePowerState obj, Type type, object[] attributes)
        {
            return String.Format("Power: {0}", obj.Power);
        }
    }
    public class AdditionalEffectStateEditor : Editor<AdditionalEffectState>
    {
        public override string GetString(AdditionalEffectState obj, Type type, object[] attributes)
        {
            return String.Format("Effect Chance: {0}%", obj.EffectChance);
        }
    }
}
