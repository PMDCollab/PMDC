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
                    newChar.DeleteSkill(StartAt, false);
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

        /// <summary>
        /// Chance of item spawn
        /// </summary>
        public Multiplier Chance;

        public MobSpawnItem()
        {
            Items = new SpawnList<InvItem>();
        }
        public MobSpawnItem(bool startOnly, params string[] itemNum) : this()
        {
            Chance = new Multiplier(1, 1);
            MapStartOnly = startOnly;
            for(int ii = 0; ii < itemNum.Length; ii++)
                Items.Add(new InvItem(itemNum[ii]), 100);
        }

        public MobSpawnItem(MobSpawnItem other) : this()
        {
            MapStartOnly = other.MapStartOnly;
            Chance = other.Chance;
            for (int ii = 0; ii < other.Items.Count; ii++)
                Items.Add(new InvItem(other.Items.GetSpawn(ii)), other.Items.GetSpawnRate(ii));
        }
        public override MobSpawnExtra Copy() { return new MobSpawnItem(this); }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (MapStartOnly && map.Begun)
                return;

            if (Chance.Denominator > 0 && map.Rand.Next(Chance.Denominator) < Chance.Numerator)
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

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: Remove in v1.1
            if (Serializer.OldVersion < new Version(0, 7, 20))
            {
                Chance = new Multiplier(1, 1);
            }
        }
    }


    /// <summary>
    /// Spawn the mob with a box containing an exclusive item.
    /// </summary>
    [Serializable]
    public abstract class MobSpawnExclBase : MobSpawnExtra
    {
        public IntRange Rarity;

        /// <summary>
        /// Type of box
        /// </summary>
        [DataType(0, DataManager.DataType.Item, false)]
        public string Box;

        /// <summary>
        /// Only give it the item on map generation.
        /// Respawns that occur after the map is generated do not get the item.
        /// </summary>
        public bool MapStartOnly;

        /// <summary>
        /// Chance of item spawn.
        /// </summary>
        public Multiplier Chance;

        public MobSpawnExclBase()
        {
            Box = "";
        }
        public MobSpawnExclBase(string box, IntRange rarity, bool startOnly) : this()
        {
            Chance = new Multiplier(1, 1);
            Box = box;
            MapStartOnly = startOnly;
            Rarity = rarity;
        }

        public MobSpawnExclBase(MobSpawnExclBase other) : this()
        {
            MapStartOnly = other.MapStartOnly;
            Rarity = other.Rarity;
            Box = other.Box;
            Chance = other.Chance;
        }

        public override void ApplyFeature(IMobSpawnMap map, Character newChar)
        {
            if (MapStartOnly && map.Begun)
                return;

            if (Chance.Denominator > 0 && map.Rand.Next(Chance.Denominator) < Chance.Numerator)
            {
                RarityData rarity = DataManager.Instance.UniversalData.Get<RarityData>();
                List<string> possibleItems = new List<string>();
                foreach (string baseSpecies in GetPossibleSpecies(map, newChar))
                {
                    for (int ii = Rarity.Min; ii < Rarity.Max; ii++)
                    {
                        Dictionary<int, List<string>> rarityTable;
                        if (rarity.RarityMap.TryGetValue(baseSpecies, out rarityTable))
                        {
                            if (rarityTable.ContainsKey(ii))
                            {
                                foreach (string item in rarityTable[ii])
                                {
                                    EntrySummary summary = DataManager.Instance.DataIndices[DataManager.DataType.Item].Get(item);
                                    if (summary.Released)
                                        possibleItems.Add(item);
                                }
                            }
                        }
                    }
                }

                InvItem equip = new InvItem(Box);
                equip.HiddenValue = possibleItems[map.Rand.Next(possibleItems.Count)];
                newChar.EquippedItem = equip;
            }
        }

        protected abstract IEnumerable<string> GetPossibleSpecies(IMobSpawnMap map, Character newChar);

        public override string ToString()
        {
            return string.Format("{0}: {1}*", this.GetType().GetFormattedTypeName(), Rarity.ToString());
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            //TODO: Remove in v1.1
            if (Serializer.OldVersion < new Version(0, 7, 20))
            {
                Chance = new Multiplier(1, 1);
            }
        }
    }


    [Serializable]
    public class MobSpawnExclFamily : MobSpawnExclBase
    {
        public MobSpawnExclFamily()
        { }
        public MobSpawnExclFamily(string box, IntRange rarity, bool startOnly) : base(box, rarity, startOnly)
        { }

        public MobSpawnExclFamily(MobSpawnExclFamily other) : base(other)
        { }

        public override MobSpawnExtra Copy() { return new MobSpawnExclFamily(this); }

        protected override IEnumerable<string> GetPossibleSpecies(IMobSpawnMap map, Character newChar)
        {
            //check prevos
            string prevo = newChar.BaseForm.Species;
            while (!String.IsNullOrEmpty(prevo))
            {
                yield return prevo;
                MonsterData data = DataManager.Instance.GetMonster(prevo);
                prevo = data.PromoteFrom;
            }

            string baseStage = newChar.BaseForm.Species;
            foreach (string evo in recurseEvos(baseStage))
                yield return evo;
        }

        private IEnumerable<string> recurseEvos(string baseStage)
        {
            MonsterData data = DataManager.Instance.GetMonster(baseStage);
            foreach (PromoteBranch branch in data.Promotions)
            {
                yield return branch.Result;
                foreach (string evo in recurseEvos(branch.Result))
                    yield return evo;
            }
        }
    }


    [Serializable]
    public class MobSpawnExclAny : MobSpawnExclBase
    {
        [DataType(1, DataManager.DataType.Monster, false)]
        public HashSet<string> ExceptFor { get; set; }


        public MobSpawnExclAny()
        {
            ExceptFor = new HashSet<string>();
        }
        public MobSpawnExclAny(string box, HashSet<string> exceptFor, IntRange rarity, bool startOnly) : base(box, rarity, startOnly)
        {
            ExceptFor = exceptFor;
        }

        public MobSpawnExclAny(MobSpawnExclAny other) : base(other)
        {
            ExceptFor = new HashSet<string>();
            foreach (string except in other.ExceptFor)
                ExceptFor.Add(except);
        }

        public override MobSpawnExtra Copy() { return new MobSpawnExclAny(this); }

        protected override IEnumerable<string> GetPossibleSpecies(IMobSpawnMap map, Character newChar)
        {
            MonsterFeatureData feature = DataManager.Instance.UniversalData.Get<MonsterFeatureData>();
            //iterate all species that have that element, except for
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Monster].GetOrderedKeys(true))
            {
                if (ExceptFor.Contains(key))
                    continue;
                EntrySummary summary = DataManager.Instance.DataIndices[DataManager.DataType.Monster].Get(key);
                if (!summary.Released)
                    continue;

                Dictionary<int, FormFeatureSummary> species;
                if (!feature.FeatureData.TryGetValue(key, out species))
                    continue;
                FormFeatureSummary form;
                if (!species.TryGetValue(0, out form))
                    continue;

                yield return key;
            }
        }
    }


    [Serializable]
    public class MobSpawnExclElement : MobSpawnExclBase
    {
        [DataType(1, DataManager.DataType.Monster, false)]
        public HashSet<string> ExceptFor { get; set; }


        public MobSpawnExclElement()
        {
            ExceptFor = new HashSet<string>();
        }
        public MobSpawnExclElement(string box, HashSet<string> exceptFor, IntRange rarity, bool startOnly) : base(box, rarity, startOnly)
        {
            ExceptFor = exceptFor;
        }

        public MobSpawnExclElement(MobSpawnExclElement other) : base(other)
        {
            ExceptFor = new HashSet<string>();
            foreach (string except in other.ExceptFor)
                ExceptFor.Add(except);
        }

        public override MobSpawnExtra Copy() { return new MobSpawnExclElement(this); }

        protected override IEnumerable<string> GetPossibleSpecies(IMobSpawnMap map, Character newChar)
        {
            MonsterFeatureData feature = DataManager.Instance.UniversalData.Get<MonsterFeatureData>();
            //iterate all species that have that element, except for
            foreach (string key in DataManager.Instance.DataIndices[DataManager.DataType.Monster].GetOrderedKeys(true))
            {
                if (ExceptFor.Contains(key))
                    continue;
                EntrySummary summary = DataManager.Instance.DataIndices[DataManager.DataType.Monster].Get(key);
                if (!summary.Released)
                    continue;

                Dictionary<int, FormFeatureSummary> species;
                if (!feature.FeatureData.TryGetValue(key, out species))
                    continue;
                FormFeatureSummary form;
                if (!species.TryGetValue(0, out form))
                    continue;

                if (form.Element1 != DataManager.Instance.DefaultElement)
                {
                    if (newChar.HasElement(form.Element1))
                        yield return key;
                }
                if (form.Element2 != DataManager.Instance.DefaultElement)
                {
                    if (newChar.HasElement(form.Element2))
                        yield return key;
                }
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
        [IntRange(0, true)]
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
