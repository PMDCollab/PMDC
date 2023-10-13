using System;
using System.Collections.Generic;
using System.Text;
using RogueEssence.Content;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using System.Drawing;
using RogueElements;
using Avalonia.Controls;
using RogueEssence.Dev.Views;
using System.Collections;
using Avalonia;
using System.Reactive.Subjects;
using PMDC.LevelGen;

namespace RogueEssence.Dev
{
    public class SaveVarsZoneStepEditor : Editor<SaveVarsZoneStep>
    {
        public override string GetString(SaveVarsZoneStep obj, Type type, object[] attributes)
        {
            return "Handle Rescues";
        }

        public override string GetTypeString()
        {
            return "Handle Rescues";
        }
    }
}
