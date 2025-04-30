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
using PMDC.Dungeon;
using RogueEssence.LevelGen;
using System.Runtime.Remoting;
using Avalonia.X11;

namespace RogueEssence.Dev
{
    public class MobSpawnWeakEditor : Editor<MobSpawnWeak>
    {
        public override string GetString(MobSpawnWeak obj, Type type, object[] attributes)
        {
            return "Half PP and 35% belly";
        }
        public override string GetTypeString()
        {
            return "Low PP and Belly";
        }
    }
    public class MobSpawnAltColorEditor : Editor<MobSpawnAltColor>
    {
        public override string GetString(MobSpawnAltColor obj, Type type, object[] attributes)
        {
            return String.Format("Shiny Chance: {0} in {1}", obj.Chance.Numerator, obj.Chance.Denominator);
        }
        public override string GetTypeString()
        {
            return "Shiny Chance";
        }
    }
    public class MobSpawnMovesOffEditor : Editor<MobSpawnMovesOff>
    {
        public override string GetString(MobSpawnMovesOff obj, Type type, object[] attributes)
        {
            return String.Format("Moves disabled after slot {0}", obj.StartAt);
        }
        public override string GetTypeString()
        {
            return "Disable Moves";
        }
    }
    public class MobSpawnBoostEditor : Editor<MobSpawnBoost>
    {
        public override string GetString(MobSpawnBoost obj, Type type, object[] attributes)
        {
            List<Tuple<String, int>> stats = new List<Tuple<String, int>>
            {
                new Tuple<String,int>("HP",obj.MaxHPBonus),
                new Tuple<String,int>("Atk",obj.AtkBonus),
                new Tuple<String,int>("Def",obj.DefBonus),
                new Tuple<String,int>("SpAtk",obj.SpAtkBonus),
                new Tuple<String,int>("SpDef",obj.SpDefBonus),
                new Tuple<String,int>("Speed",obj.SpeedBonus),
            };
            List<String> statBoosts = new List<String>();
            foreach ((String statName, int bonus) in stats)
            {
                if (bonus != 0)
                {
                    statBoosts.Add(String.Format("{0} {1}", bonus.ToString("+0;-#"), statName));
                }
            }
            return String.Format("Stat boosts: {0}", String.Join(", ", statBoosts));
        }
        public override string GetTypeString()
        {
            return "Stat Boosts";
        }
    }
    public class MobSpawnScaledBoostEditor : Editor<MobSpawnScaledBoost>
    {
        public override string GetString(MobSpawnScaledBoost obj, Type type, object[] attributes)
        {
            
            List<Tuple<String, IntRange>> stats = new List<Tuple<String, IntRange>>
            {
                new Tuple<String,IntRange>("Lvl",obj.LevelRange),
                new Tuple<String,IntRange>("HP",obj.MaxHPBonus),
                new Tuple<String,IntRange>("Atk",obj.AtkBonus),
                new Tuple<String,IntRange>("Def",obj.DefBonus),
                new Tuple<String,IntRange>("SpAtk",obj.SpAtkBonus),
                new Tuple<String,IntRange>("SpDef",obj.SpDefBonus),
                new Tuple<String,IntRange>("Speed",obj.SpeedBonus),
            };
            List<String> statBoosts = new List<String>();
            foreach ((String statName, IntRange bonusRange) in stats)
            {
                if ((bonusRange.Min != 0) | (bonusRange.Max != 0))
                {
                    statBoosts.Add(String.Format("{0}: [{1}, {2}]", statName, bonusRange.Min, bonusRange.Max));
                }
            }
            return String.Format("Level-scaled stat boosts: {0}", String.Join(", ", statBoosts));
        }
        public override string GetTypeString()
        {
            return "Level-Scaled Stat Boosts";
        }
    }
    public class MobSpawnItemEditor : Editor<MobSpawnItem>
    {
        public override string GetString(MobSpawnItem obj, Type type, object[] attributes)
        {
            String Item = "";
            if (obj.Items.Count == 1)
            {
                Item = obj.Items.GetSpawn(0).ToString();
            } 
            else
            {
                Item = obj.Items.ToString();
            }
            return String.Format("Item: {0}", Item);
        }
        public override string GetTypeString()
        {
            return "Held Item";
        }
    }
    public class MobSpawnInvEditor : Editor<MobSpawnInv>
    {
        public override string GetString(MobSpawnInv obj, Type type, object[] attributes)
        {
            List<String> inventory = new List<String>();
            foreach (InvItem item in obj.Items)
            {
                inventory.Add(item.ToString());
            }
            return String.Format("Inventory: {0}", String.Join(", ", inventory));
        }
        public override string GetTypeString()
        {
            return "Inventory";
        }
    }
    public class MobSpawnLevelScaleEditor : Editor<MobSpawnLevelScale>
    {
        public override string GetString(MobSpawnLevelScale obj, Type type, object[] attributes)
        {
            return String.Format("Scale level to floor starting at floor {0}", obj.StartFromID + 1);
        }
        public override string GetTypeString()
        {
            return "Floor-Scaled Level";
        }
    }
    public class MobSpawnLocEditor : Editor<MobSpawnLoc>
    {
        public override string GetString(MobSpawnLoc obj, Type type, object[] attributes)
        {
            return String.Format("Spawn at X:{0}, Y:{1}, facing {2}", obj.Loc.X, obj.Loc.Y, obj.Dir);
        }
        public override string GetTypeString()
        {
            return "Position and Orientation";
        }
    }
    public class MobSpawnUnrecruitableEditor : Editor<MobSpawnUnrecruitable>
    {
        public override string GetString(MobSpawnUnrecruitable obj, Type type, object[] attributes)
        {
            return "Unrecruitable";
        }
        public override string GetTypeString()
        {
            return "Unrecruitable";
        }
    }
    public class MobSpawnFoeConflictEditor : Editor<MobSpawnFoeConflict>
    {
        public override string GetString(MobSpawnFoeConflict obj, Type type, object[] attributes)
        {
            return "Attacks Enemies";
        }
        public override string GetTypeString()
        {
            return "Aggressive";
        }
    }
    public class MobSpawnInteractableEditor : Editor<MobSpawnInteractable>
    {
        public override string GetString(MobSpawnInteractable obj, Type type, object[] attributes)
        {
            List<String> interactionEventNames = new List<String>();
            foreach (BattleEvent battleEvent in obj.CheckEvents)
            {
                interactionEventNames.Add(battleEvent.ToString());
            }
            return String.Format("Interactions: {0}", String.Join(", ", interactionEventNames));
        }
        public override string GetTypeString()
        {
            return "Interactions";
        }
    }
    public class MobSpawnLuaTableEditor : Editor<MobSpawnLuaTable>
    {
        public override string GetString(MobSpawnLuaTable obj, Type type, object[] attributes)
        {
            return "Custom Lua Script";
        }
        public override string GetTypeString()
        {
            return "Lua Scripting";
        }
    }
    public class MobSpawnDiscriminatorEditor : Editor<MobSpawnDiscriminator>
    {
        public override string GetString(MobSpawnDiscriminator obj, Type type, object[] attributes)
        {
            return String.Format("Descriminator ID: {0}", obj.Discriminator);
        }
        public override string GetTypeString()
        {
            return "Descriminator";
        }
    }
    public class MobSpawnStatusEditor : Editor<MobSpawnStatus>
    {
        public override string GetString(MobSpawnStatus obj, Type type, object[] attributes)
        {
            if (obj.Statuses.Count != 1)
                return string.Format("Status: [{0}]", obj.Statuses.Count.ToString());
            else
            {
                EntrySummary summary = DataManager.Instance.DataIndices[DataManager.DataType.Status].Get(obj.Statuses.GetSpawn(0).ID);
                return string.Format("Status: {0}", summary.Name.ToLocal());
            }
        }
        public override string GetTypeString()
        {
            return "Status";
        }
    }
	public class Intrinsic3ChanceEditor : Editor<Intrinsic3Chance>
    {
        public override string GetString(Intrinsic3Chance obj, Type type, object[] attributes)
        {
            return "Roll for Hidden Ability";
        }
        public override string GetTypeString()
        {
            return "Roll for Hidden Ability";
        }
    }
}
