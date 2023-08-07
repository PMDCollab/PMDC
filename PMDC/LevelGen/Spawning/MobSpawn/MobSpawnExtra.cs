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
using PMDC.Dungeon;
using System.Runtime.Serialization;

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
                if (!String.IsNullOrEmpty(newChar.Skills[ii].Element.SkillNum))
                {
                    SkillData data = DataManager.Instance.GetSkill(newChar.Skills[ii].Element.SkillNum);
                    newChar.SetSkillCharges(ii, MathUtils.DivUp(data.BaseCharges, 2));
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }

    /// <summary>
    /// Spawns the mob with a custom shiny chance.
    /// </summary>
    [Serializable]
    public class MobSpawnAltColor : MobSpawnExtra
    {
        /// <summary>
        /// Fractional chance of occurrence.
        /// </summary>
        [FractionLimit(0, 0, 0)]
        public Multiplier Chance;
        
        /// <summary>
        /// OBSOLETE
        /// </summary>
        [NonEdited]
        public int Odds;

        public MobSpawnAltColor() { }
        public MobSpawnAltColor(int odds)
        {
            Chance = new Multiplier(1, odds);
        }
        public MobSpawnAltColor(MobSpawnAltColor other)
        {
            Chance = other.Chance;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnAltColor(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (Chance.Denominator > 0 && map.Rand.Next(Chance.Denominator) < Chance.Numerator)
            {
                SkinTableState table = DataManager.Instance.UniversalEvent.UniversalStates.GetWithDefault<SkinTableState>();
                newChar.BaseForm.Skin = table.AltColor;
            }
            else
                newChar.BaseForm.Skin = DataManager.Instance.DefaultSkin;
            newChar.CurrentForm = newChar.BaseForm;
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}", this.GetType().GetFormattedTypeName(), Chance);
        }


        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: Remove in v1.1
            if (Serializer.OldVersion < new Version(0, 7, 15))
            {
                Chance = new Multiplier(1, Odds);
            }
        }
    }

    /// <summary>
    /// Spawns the mob with moves turned off.
    /// </summary>
    [Serializable]
    public class MobSpawnMovesOff : MobSpawnExtra
    {
        /// <summary>
        /// The move index to start turning moves off.
        /// </summary>
        public int StartAt;

        /// <summary>
        /// Remove the moves entirely.
        /// </summary>
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
            return string.Format("{0}: {1}+", this.GetType().GetFormattedTypeName(), StartAt);
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
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }

    /// <summary>
    /// Spawn the mob with stat boosts (vitamin boosts) that scale based on its level
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
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }

    /// <summary>
    /// Spawn the mob with an item.
    /// </summary>
    [Serializable]
    public class MobSpawnItem : MobSpawnExtra
    {
        /// <summary>
        /// The possible items.  Picks one.
        /// </summary>
        public SpawnList<InvItem> Items;

        /// <summary>
        /// Only give it the item on map generation.
        /// Respawns that occur after the map is generated do not get the item.
        /// </summary>
        public bool MapStartOnly;

        public MobSpawnItem()
        {
            Items = new SpawnList<InvItem>();
        }
        public MobSpawnItem(bool startOnly, params string[] itemNum) : this()
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
                return string.Format("{0}[{1}]", this.GetType().GetFormattedTypeName(), Items.Count.ToString());
            else
            {
                EntrySummary summary = DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(Items.GetSpawn(0).ID);
                return string.Format("{0}: {1}", this.GetType().GetFormattedTypeName(), summary.Name.ToLocal());
            }
        }
    }


    /// <summary>
    /// Spawn the mob with its inventory filled with the specified items.
    /// Inventory items are not dropped when the mob is defeated.
    /// </summary>
    [Serializable]
    public class MobSpawnInv : MobSpawnExtra
    {
        /// <summary>
        /// Items to give.  All of them will be placed in the mob's inventory.
        /// </summary>
        public List<InvItem> Items;

        /// <summary>
        /// Only give it the item on map generation.
        /// Respawns that occur after the map is generated do not get the item.
        /// </summary>
        public bool MapStartOnly;

        public MobSpawnInv()
        {
            Items = new List<InvItem>();
        }
        public MobSpawnInv(bool startOnly, params string[] itemNum) : this()
        {
            MapStartOnly = startOnly;
            for (int ii = 0; ii < itemNum.Length; ii++)
                Items.Add(new InvItem(itemNum[ii]));
        }

        public MobSpawnInv(MobSpawnInv other) : this()
        {
            MapStartOnly = other.MapStartOnly;
            for (int ii = 0; ii < other.Items.Count; ii++)
                Items.Add(new InvItem(other.Items[ii]));
        }
        public override MobSpawnExtra Copy() { return new MobSpawnInv(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (MapStartOnly && map.Begun)
                return;

            for (int ii = 0; ii < Items.Count; ii++)
                newChar.MemberTeam.AddToInv(Items[ii], true);
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }

    /// <summary>
    /// Spawn the mob with a level that scales based on the current floor
    /// </summary>
    [Serializable]
    public class MobSpawnLevelScale : MobSpawnExtra
    {
        /// <summary>
        /// The floor to start scaling level at.
        /// </summary>
        public int StartFromID;

        /// <summary>
        /// The numerator for the fractional level to add per floor.
        /// </summary>
        public int AddNumerator;

        /// <summary>
        /// The denominator for the fractional level to add per floor.
        /// </summary>
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
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }

    /// <summary>
    /// Spawn the mob with a specific location and direction
    /// </summary>
    [Serializable]
    public class MobSpawnLoc : MobSpawnExtra
    {
        /// <summary>
        /// The location.
        /// </summary>
        public Loc Loc;

        /// <summary>
        /// The direction.
        /// </summary>
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
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }


    /// <summary>
    /// Spawn the mob with recruitment turned off.
    /// </summary>
    [Serializable]
    public class MobSpawnUnrecruitable : MobSpawnExtra
    {
        public override MobSpawnExtra Copy() { return new MobSpawnUnrecruitable(); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (newChar.MemberTeam is MonsterTeam)
                newChar.Unrecruitable = true;
            newChar.BaseForm.Skin = DataManager.Instance.DefaultSkin;
            newChar.CurrentForm = newChar.BaseForm;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }


    /// <summary>
    /// Spawns the mob with aggression towards enemy mobs.  Only applies to neutral mobs.
    /// </summary>
    [Serializable]
    public class MobSpawnFoeConflict : MobSpawnExtra
    {
        public override MobSpawnExtra Copy() { return new MobSpawnFoeConflict(); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (newChar.MemberTeam is MonsterTeam)
                newChar.MemberTeam.FoeConflict = true;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }


    /// <summary>
    /// Spawn the mob with an effect on interaction.  Only applies to allies or neutral mobs.
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
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }


    /// <summary>
    /// Spawn the mob with a lua data table.
    /// </summary>
    [Serializable]
    public class MobSpawnLuaTable : MobSpawnExtra
    {
        /// <summary>
        /// The lua table.
        /// </summary>
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
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }




    /// <summary>
    /// Spawn the mob with a discriminator.  This is used for personality calculations.
    /// </summary>
    [Serializable]
    public class MobSpawnDiscriminator : MobSpawnExtra
    {
        public int Discriminator;

        public MobSpawnDiscriminator() { }
        public MobSpawnDiscriminator(int discriminator) { Discriminator = discriminator; }
        protected MobSpawnDiscriminator(MobSpawnDiscriminator other)
        {
            Discriminator = other.Discriminator;
        }
        public override MobSpawnExtra Copy() { return new MobSpawnDiscriminator(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            newChar.Discriminator = Discriminator;
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }
}
