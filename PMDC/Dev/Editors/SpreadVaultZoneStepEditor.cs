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
    public class SpreadVaultZoneStepEditor : Editor<SpreadVaultZoneStep>
    {
        public override string GetString(SpreadVaultZoneStep obj, Type type, object[] attributes)
        {
            string housePrefix = "";

            if (obj.Mobs.Count > 0)
            {
                housePrefix = "Monster";
            }
            else if (obj.Items.Count > 0)
            {
                housePrefix = "Item";
            }

            return String.Format("Spread {0} Vaults", housePrefix);
        }
        public override string GetTypeString()
        {
            return "Spread Vaults";
        }
    }
}
