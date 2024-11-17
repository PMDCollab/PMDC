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
    public class FloorNameDropZoneStepEditor : Editor<FloorNameDropZoneStep>
    {
        public override string GetString(FloorNameDropZoneStep obj, Type type, object[] attributes)
        {
            return String.Format("{0}: '{1}'", "Show Floor Name", obj.Name);
        }
        public override string GetTypeString()
        {
            return "Show Floor Name";
        }
    }
}
