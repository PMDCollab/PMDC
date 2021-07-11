using System;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using RogueElements;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Data;
using System.Collections.Generic;
using RogueEssence.Dev;
using RogueEssence.Script;
using NLua;
using System.Linq;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Spawns the mob with a 35% fullness and 50% PP.
    /// </summary>
    [Serializable]
    public class MobSpawnWeak : MobSpawnExtra
    {
        public override MobSpawnExtra Copy() { return new MobSpawnWeak(); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            //set the newChar's uses to 50% (ceiling), hunger to 35%
            newChar.Fullness = 35;
            for (int ii = 0; ii < newChar.Skills.Count; ii++)
            {
                if (newChar.Skills[ii].Element.SkillNum > -1)
                {
                    SkillData data = DataManager.Instance.GetSkill(newChar.Skills[ii].Element.SkillNum);
                    newChar.SetSkillCharges(ii, (data.BaseCharges - 1) / 2 + 1);
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }

    /// <summary>
    /// Spawns the mob with a custom shiny chance.
    /// </summary>
    [Serializable]
    public class MobSpawnAltColor : MobSpawnExtra
    {
        public int Odds;

        public MobSpawnAltColor() { }
        public MobSpawnAltColor(int odds)
        {
            Odds = odds;
        }
        public MobSpawnAltColor(MobSpawnAltColor other)
        {
            Odds = other.Odds;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnAltColor(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (map.Rand.Next(Odds) == 0)
                newChar.BaseForm.Skin = 1;
        }

        public override string ToString()
        {
            return string.Format("{0}: 1/{1}", this.GetType().Name, Odds);
        }
    }

    /// <summary>
    /// Spawns the mob with moves turned off.
    /// </summary>
    [Serializable]
    public class MobSpawnMovesOff : MobSpawnExtra
    {
        public int StartAt;
        public bool Remove;

        public MobSpawnMovesOff() { }
        public MobSpawnMovesOff(int startAt)
        {
            StartAt = startAt;
        }
        public MobSpawnMovesOff(MobSpawnMovesOff other)
        {
            StartAt = other.StartAt;
            Remove = other.Remove;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnMovesOff(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (Remove)
            {
                for (int ii = StartAt; ii < Character.MAX_SKILL_SLOTS; ii++)
                    newChar.DeleteSkill(StartAt);
            }
            else
            {
                for (int ii = StartAt; ii < Character.MAX_SKILL_SLOTS; ii++)
                    newChar.Skills[ii].Element.Enabled = false;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}+", this.GetType().Name, StartAt);
        }
    }

    /// <summary>
    /// Spawn the mob with stat boosts (vitamin boosts)
    /// </summary>
    [Serializable]
    public class MobSpawnBoost : MobSpawnExtra
    {
        public int MaxHPBonus;
        public int AtkBonus;
        public int DefBonus;
        public int SpAtkBonus;
        public int SpDefBonus;
        public int SpeedBonus;

        public MobSpawnBoost() { }
        public MobSpawnBoost(MobSpawnBoost other)
        {
            MaxHPBonus = other.MaxHPBonus;
            AtkBonus = other.AtkBonus;
            DefBonus = other.DefBonus;
            SpAtkBonus = other.SpAtkBonus;
            SpDefBonus = other.SpDefBonus;
            SpeedBonus = other.SpeedBonus;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnBoost(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            newChar.MaxHPBonus = Math.Min(MaxHPBonus, MonsterFormData.MAX_STAT_BOOST);
            newChar.AtkBonus = Math.Min(AtkBonus, MonsterFormData.MAX_STAT_BOOST);
            newChar.DefBonus = Math.Min(DefBonus, MonsterFormData.MAX_STAT_BOOST);
            newChar.MAtkBonus = Math.Min(SpAtkBonus, MonsterFormData.MAX_STAT_BOOST);
            newChar.MDefBonus = Math.Min(SpDefBonus, MonsterFormData.MAX_STAT_BOOST);
            newChar.SpeedBonus = Math.Min(SpeedBonus, MonsterFormData.MAX_STAT_BOOST);
            newChar.HP = newChar.MaxHP;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }

    /// <summary>
    /// Spawn the mob with stat boosts (vitamin boosts) that scale based on level
    /// </summary>
    [Serializable]
    public class MobSpawnScaledBoost : MobSpawnExtra
    {
        public IntRange LevelRange;
        public IntRange MaxHPBonus;
        public IntRange AtkBonus;
        public IntRange DefBonus;
        public IntRange SpAtkBonus;
        public IntRange SpDefBonus;
        public IntRange SpeedBonus;

        public MobSpawnScaledBoost() { }
        public MobSpawnScaledBoost(IntRange range)
        {
            LevelRange = range;
        }
        public MobSpawnScaledBoost(MobSpawnScaledBoost other)
        {
            LevelRange = other.LevelRange;
            MaxHPBonus = other.MaxHPBonus;
            AtkBonus = other.AtkBonus;
            DefBonus = other.DefBonus;
            SpAtkBonus = other.SpAtkBonus;
            SpDefBonus = other.SpDefBonus;
            SpeedBonus = other.SpeedBonus;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnScaledBoost(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            int clampedLevel = Math.Clamp(newChar.Level, LevelRange.Min, LevelRange.Max);
            newChar.MaxHPBonus = Math.Min(MaxHPBonus.Min + MaxHPBonus.Length * (clampedLevel - LevelRange.Min) / LevelRange.Length, MonsterFormData.MAX_STAT_BOOST);
            newChar.AtkBonus = Math.Min(AtkBonus.Min + AtkBonus.Length * (clampedLevel - LevelRange.Min) / LevelRange.Length, MonsterFormData.MAX_STAT_BOOST);
            newChar.DefBonus = Math.Min(DefBonus.Min + DefBonus.Length * (clampedLevel - LevelRange.Min) / LevelRange.Length, MonsterFormData.MAX_STAT_BOOST);
            newChar.MAtkBonus = Math.Min(SpAtkBonus.Min + SpAtkBonus.Length * (clampedLevel - LevelRange.Min) / LevelRange.Length, MonsterFormData.MAX_STAT_BOOST);
            newChar.MDefBonus = Math.Min(SpDefBonus.Min + SpDefBonus.Length * (clampedLevel - LevelRange.Min) / LevelRange.Length, MonsterFormData.MAX_STAT_BOOST);
            newChar.SpeedBonus = Math.Min(SpeedBonus.Min + SpeedBonus.Length * (clampedLevel - LevelRange.Min) / LevelRange.Length, MonsterFormData.MAX_STAT_BOOST);
            newChar.HP = newChar.MaxHP;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }

    /// <summary>
    /// Spawn the mob an item
    /// </summary>
    [Serializable]
    public class MobSpawnItem : MobSpawnExtra
    {
        public SpawnList<InvItem> Items;
        public bool MapStartOnly;

        public MobSpawnItem()
        {
            Items = new SpawnList<InvItem>();
        }
        public MobSpawnItem(bool startOnly, params int[] itemNum) : this()
        {
            MapStartOnly = startOnly;
            for(int ii = 0; ii < itemNum.Length; ii++)
                Items.Add(new InvItem(itemNum[ii]), 100);
        }

        public MobSpawnItem(MobSpawnItem other) : this()
        {
            MapStartOnly = other.MapStartOnly;
            for (int ii = 0; ii < other.Items.Count; ii++)
                Items.Add(new InvItem(other.Items.GetSpawn(ii)), other.Items.GetSpawnRate(ii));
        }
        public override MobSpawnExtra Copy() { return new MobSpawnItem(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (MapStartOnly && map.Begun)
                return;

            newChar.EquippedItem = Items.Pick(map.Rand);
        }

        public override string ToString()
        {
            if (Items.Count != 1)
                return string.Format("{0}[{1}]", this.GetType().Name, Items.Count.ToString());
            else
            {
                EntrySummary summary = DataManager.Instance.DataIndices[DataManager.DataType.Item].Entries[Items.GetSpawn(0).ID];
                return string.Format("{0}: {1}", this.GetType().Name, summary.Name.ToLocal());
            }
        }
    }


    /// <summary>
    /// Spawn the mob with a level that scales based on the current floor
    /// </summary>
    [Serializable]
    public class MobSpawnLevelScale : MobSpawnExtra
    {
        public int StartFromID;
        public int AddNumerator;
        public int AddDenominator;

        public MobSpawnLevelScale()
        {

        }
        public MobSpawnLevelScale(int numerator, int denominator) : this()
        {
            AddNumerator = numerator;
            AddDenominator = denominator;
        }

        public MobSpawnLevelScale(MobSpawnLevelScale other) : this()
        {
            StartFromID = other.StartFromID;
            AddNumerator = other.AddNumerator;
            AddDenominator = other.AddDenominator;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnLevelScale(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            newChar.Level += (map.ID-StartFromID) * AddNumerator / AddDenominator;
            newChar.HP = newChar.MaxHP;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }

    /// <summary>
    /// Spawn the mob with a specific location and direction
    /// </summary>
    [Serializable]
    public class MobSpawnLoc : MobSpawnExtra
    {
        public Loc Loc;
        public Dir8 Dir;

        public MobSpawnLoc() { }
        public MobSpawnLoc(Loc loc) { Loc = loc; }
        public MobSpawnLoc(Loc loc, Dir8 dir) { Loc = loc; Dir = dir; }
        public MobSpawnLoc(MobSpawnLoc other)
        {
            Loc = other.Loc;
            Dir = other.Dir;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnLoc(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            newChar.CharLoc = Loc;
            newChar.CharDir = Dir;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }


    /// <summary>
    /// Spawn the mob with recruitment turned off
    /// </summary>
    [Serializable]
    public class MobSpawnUnrecruitable : MobSpawnExtra
    {
        public override MobSpawnExtra Copy() { return new MobSpawnUnrecruitable(); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (newChar.MemberTeam is MonsterTeam)
                ((MonsterTeam)newChar.MemberTeam).Unrecruitable = true;
            newChar.BaseForm.Skin = 0;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }


    /// <summary>
    /// Spawn the mob with an effect on interaction (shows up for allies only)
    /// </summary>
    [Serializable]
    public class MobSpawnInteractable : MobSpawnExtra
    {
        public List<BattleEvent> CheckEvents;

        public MobSpawnInteractable()
        {
            CheckEvents = new List<BattleEvent>();
        }
        public MobSpawnInteractable(params BattleEvent[] checkEvents)
        {
            CheckEvents = new List<BattleEvent>();
            foreach (BattleEvent effect in checkEvents)
                CheckEvents.Add(effect);
        }

        public MobSpawnInteractable(MobSpawnInteractable other) : this()
        {
            foreach (BattleEvent effect in other.CheckEvents)
                CheckEvents.Add((BattleEvent)effect.Clone());
        }
        public override MobSpawnExtra Copy() { return new MobSpawnInteractable(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            foreach (BattleEvent effect in CheckEvents)
                newChar.ActionEvents.Add((BattleEvent)effect.Clone());
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }


    /// <summary>
    /// Spawn the mob with a lua data table
    /// </summary>
    [Serializable]
    public class MobSpawnLuaTable : MobSpawnExtra
    {
        [Multiline(0)]
        public string LuaTable;

        public MobSpawnLuaTable() { LuaTable = "{}"; }
        public MobSpawnLuaTable(string luaTable) { LuaTable = luaTable; }
        protected MobSpawnLuaTable(MobSpawnLuaTable other)
        {
            LuaTable = other.LuaTable;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnLuaTable(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            newChar.LuaDataTable = LuaEngine.Instance.RunString("return " + LuaTable).First() as LuaTable;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().Name);
        }
    }
}
