using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.Dev;
using RogueEssence;
using RogueEssence.LevelGen;
using RogueEssence.Data;

namespace PMDC.LevelGen
{

    //specify the chance of each theme, plus the purity of each theme
    //to specify no items from the special spawnlist, choose "no theme", and choose purity of 0
    //don't pull in existing items to compensate the generosity... just see if you can lower the spawn rate in the by-floor post proc...
    //or maybe it doesn't need compensation


    [Serializable]
    public abstract class ItemTheme
    {
        //TODO: use randpickers for these
        public RandRange Amount;
        public ItemTheme() { }
        public ItemTheme(RandRange amount) { Amount = amount; }
        protected ItemTheme(ItemTheme other) { Amount = other.Amount; }
        public abstract ItemTheme Copy();
        public abstract List<MapItem> GenerateItems(BaseMapGenContext map, SpawnList<MapItem> specialItems);
    }

    [Serializable]
    public class ItemThemeMultiple : ItemTheme
    {
        public List<ItemTheme> Themes;

        public ItemThemeMultiple() { Themes = new List<ItemTheme>(); }
        public ItemThemeMultiple(params ItemTheme[] themes)
        {
            Themes = new List<ItemTheme>();
            Themes.AddRange(themes);
        }
        protected ItemThemeMultiple(ItemThemeMultiple other) : base(other)
        {
            Themes = other.Themes;
        }
        public override ItemTheme Copy() { return new ItemThemeMultiple(this); }

        public override List<MapItem> GenerateItems(BaseMapGenContext map, SpawnList<MapItem> specialItems)
        {
            List<MapItem> spawners = new List<MapItem>();

            foreach (ItemTheme theme in Themes)
            {
                List<MapItem> items = theme.GenerateItems(map, specialItems);
                spawners.AddRange(items);
            }

            return spawners;
        }
    }

    [Serializable]
    public class ItemThemeNone : ItemTheme
    {
        public int SpecialRatio;

        public ItemThemeNone() { }
        public ItemThemeNone(int specialRatio, RandRange amount) : base(amount)
        {
            SpecialRatio = specialRatio;
        }
        protected ItemThemeNone(ItemThemeNone other) : base(other)
        {
            SpecialRatio = other.SpecialRatio;
        }
        public override ItemTheme Copy() { return new ItemThemeNone(this); }

        public override List<MapItem> GenerateItems(BaseMapGenContext map, SpawnList<MapItem> specialItems)
        {
            int itemCount = Amount.Pick(map.Rand);
            List<MapItem> spawners = new List<MapItem>();

            for (int ii = 0; ii < itemCount; ii++)
            {
                if (specialItems.Count > 0 && map.Rand.Next(100) < SpecialRatio)
                    spawners.Add(specialItems.Pick(map.Rand));
                else
                {
                    if (map.ItemSpawns.CanPick)
                        spawners.Add(new MapItem(map.ItemSpawns.Pick(map.Rand)));
                }
            }

            return spawners;
        }
    }

    [Serializable]
    public class ItemThemeType : ItemTheme
    {
        public ItemData.UseType UseType;

        public ItemThemeType() { }
        public ItemThemeType(ItemData.UseType useType, RandRange amount) : base(amount)
        {
            UseType = useType;
        }
        protected ItemThemeType(ItemThemeType other) : base(other)
        {
            UseType = other.UseType;
        }
        public override ItemTheme Copy() { return new ItemThemeType(this); }

        public override List<MapItem> GenerateItems(BaseMapGenContext map, SpawnList<MapItem> specialItems)
        {
            int itemCount = Amount.Pick(map.Rand);
            List<MapItem> spawners = new List<MapItem>();

            SpawnList<MapItem> subList = new SpawnList<MapItem>();
            for (int ii = 0; ii < specialItems.Count; ii++)
            {
                MapItem spawn = specialItems.GetSpawn(ii);
                if (!spawn.IsMoney)
                {
                    //TODO: retrieving items from disk is an expensive operation
                    //especially when hundreds of items may be in the spawn pool
                    //we should make an index in the DataManager that keeps track of item types
                    //(you can also do this for item states)
                    //when the game begins, those indices will be loaded and this code can just reference those indices
                    ItemData data = DataManager.Instance.GetItem(spawn.Value);
                    if (data.UsageType == UseType)
                        subList.Add(spawn, specialItems.GetSpawnRate(ii));
                }
            }

            foreach(string key in map.ItemSpawns.Spawns.GetKeys())
            {
                SpawnList<InvItem> spawns = map.ItemSpawns.Spawns.GetSpawn(key);
                for (int ii = 0; ii < spawns.Count; ii++)
                {
                    //TODO: spawn rate is somewhat distorted here
                    InvItem spawn = spawns.GetSpawn(ii);
                    ItemData data = DataManager.Instance.GetItem(spawn.ID);
                    if (data.UsageType == UseType)
                        subList.Add(new MapItem(spawn), spawns.GetSpawnRate(ii));
                }
            }

            if (subList.Count == 0)
                return spawners;

            for (int ii = 0; ii < itemCount; ii++)
                spawners.Add(subList.Pick(map.Rand));

            return spawners;
        }
    }

    [Serializable]
    public class ItemStateType : ItemTheme
    {
        [StringTypeConstraint(0, typeof(ItemState))]
        public FlagType UseType;

        public ItemStateType() { }
        public ItemStateType(FlagType useType, RandRange amount) : base(amount)
        {
            UseType = useType;
        }
        protected ItemStateType(ItemStateType other) : base(other)
        {
            UseType = other.UseType;
        }
        public override ItemTheme Copy() { return new ItemStateType(this); }

        public override List<MapItem> GenerateItems(BaseMapGenContext map, SpawnList<MapItem> specialItems)
        {
            int itemCount = Amount.Pick(map.Rand);
            List<MapItem> spawners = new List<MapItem>();

            SpawnList<MapItem> subList = new SpawnList<MapItem>();
            for (int ii = 0; ii < specialItems.Count; ii++)
            {
                MapItem spawn = specialItems.GetSpawn(ii);
                if (!spawn.IsMoney)
                {
                    ItemData data = DataManager.Instance.GetItem(spawn.Value);
                    if (data.ItemStates.Contains(UseType.FullType))
                        subList.Add(spawn, specialItems.GetSpawnRate(ii));
                }
            }

            foreach (string key in map.ItemSpawns.Spawns.GetKeys())
            {
                SpawnList<InvItem> spawns = map.ItemSpawns.Spawns.GetSpawn(key);
                for (int ii = 0; ii < spawns.Count; ii++)
                {
                    //TODO: spawn rate is somewhat distorted here
                    InvItem spawn = spawns.GetSpawn(ii);
                    ItemData data = DataManager.Instance.GetItem(spawn.ID);
                    if (data.ItemStates.Contains(UseType.FullType))
                        subList.Add(new MapItem(spawn), spawns.GetSpawnRate(ii));
                }
            }

            if (subList.Count == 0)
                return spawners;

            for (int ii = 0; ii < itemCount; ii++)
                spawners.Add(subList.Pick(map.Rand));

            return spawners;
        }
    }


    [Serializable]
    public abstract class MobTheme
    {
        public RandRange Amount;
        public MobTheme() { }
        public MobTheme(RandRange amount) { Amount = amount; }
        protected MobTheme(MobTheme other) { Amount = other.Amount; }
        public abstract MobTheme Copy();
        public abstract List<MobSpawn> GenerateMobs(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs);

        protected MobSpawn GetSeedChar(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            //the contents of that theme can be selected randomly,
            MobSpawn seedSpawn = null;
            //or, to add some sensibility, make it seeded from a random spawn that can already be found in the map
            if (map.TeamSpawns.CanPick)
            {
                TeamSpawner spawn = map.TeamSpawns.Pick(map.Rand);
                if (spawn != null)
                {
                    List<MobSpawn> exampleList = spawn.ChooseSpawns(map.Rand);
                    if (exampleList.Count > 0)
                        seedSpawn = exampleList[map.Rand.Next(exampleList.Count)];
                }
            }
            //choose the spawn, then seed the theme with it
            //the theme will take the aspects of the seedspawn and then be ready to spit out a list
            if (seedSpawn == null && specialMobs.CanPick)
            {
                seedSpawn = specialMobs.Pick(map.Rand);
            }
            return seedSpawn;
        }
    }



    [Serializable]
    public class MobThemeNone : MobTheme
    {
        public int SpecialRatio;

        public MobThemeNone() { }
        public MobThemeNone(int specialRatio, RandRange amount) : base(amount)
        {
            SpecialRatio = specialRatio;
        }
        protected MobThemeNone(MobThemeNone other) : base(other)
        {
            SpecialRatio = other.SpecialRatio;
        }
        public override MobTheme Copy() { return new MobThemeNone(this); }

        public override List<MobSpawn> GenerateMobs(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            int mobCount = Amount.Pick(map.Rand);
            List<MobSpawn> spawners = new List<MobSpawn>();

            for (int ii = 0; ii < mobCount; ii++)
            {
                if (specialMobs.Count > 0 && map.Rand.Next(100) < SpecialRatio)
                    spawners.Add(specialMobs.Pick(map.Rand));
                else if (map.TeamSpawns.Count > 0)
                {
                    List<MobSpawn> exampleList = map.TeamSpawns.Pick(map.Rand).ChooseSpawns(map.Rand);
                    if (exampleList.Count > 0)
                        spawners.Add(exampleList[map.Rand.Next(exampleList.Count)]);
                }
            }

            return spawners;
        }
    }



    [Serializable]
    public class ItemThemeRange : ItemTheme
    {
        public IntRange Range;

        public ItemThemeRange() { }
        public ItemThemeRange(IntRange range, RandRange amount) : base(amount)
        {
            Range = range;
        }
        protected ItemThemeRange(ItemThemeRange other) : base(other)
        {
            Range = other.Range;
        }
        public override ItemTheme Copy() { return new ItemThemeRange(this); }

        public override List<MapItem> GenerateItems(BaseMapGenContext map, SpawnList<MapItem> specialItems)
        {
            int itemCount = Amount.Pick(map.Rand);
            List<MapItem> spawners = new List<MapItem>();

            SpawnList<MapItem> subList = new SpawnList<MapItem>();
            for (int ii = 0; ii < specialItems.Count; ii++)
            {
                MapItem spawn = specialItems.GetSpawn(ii);
                if (!spawn.IsMoney)
                {
                    if (Range.Min <= spawn.Value && spawn.Value < Range.Max)
                        subList.Add(spawn, specialItems.GetSpawnRate(ii));
                }
            }

            foreach (string key in map.ItemSpawns.Spawns.GetKeys())
            {
                SpawnList<InvItem> spawns = map.ItemSpawns.Spawns.GetSpawn(key);
                for (int ii = 0; ii < spawns.Count; ii++)
                {
                    //TODO: spawn rate is somewhat distorted here
                    InvItem spawn = spawns.GetSpawn(ii);
                    ItemData data = DataManager.Instance.GetItem(spawn.ID);
                    if (Range.Min <= spawn.ID && spawn.ID < Range.Max)
                        subList.Add(new MapItem(spawn), spawns.GetSpawnRate(ii));
                }
            }

            if (subList.Count == 0)
                return spawners;

            for (int ii = 0; ii < itemCount; ii++)
                spawners.Add(subList.Pick(map.Rand));

            return spawners;
        }
    }

    [Serializable]
    public class ItemThemeMoney : ItemTheme
    {
        public int Multiplier;

        public ItemThemeMoney() { }
        public ItemThemeMoney(int mult, RandRange amount) : base(amount)
        {
            Multiplier = mult;
        }
        protected ItemThemeMoney(ItemThemeMoney other) : base(other)
        {
            Multiplier = other.Multiplier;
        }
        public override ItemTheme Copy() { return new ItemThemeMoney(this); }

        public override List<MapItem> GenerateItems(BaseMapGenContext map, SpawnList<MapItem> specialItems)
        {
            int itemCount = Amount.Pick(map.Rand);
            List<MapItem> spawners = new List<MapItem>();

            for (int ii = 0; ii < itemCount; ii++)
                spawners.Add(new MapItem(true, Math.Max(1, map.MoneyAmount.Pick(map.Rand).Amount * Multiplier / 100)));

            return spawners;
        }
    }


    [Serializable]
    public class MobThemeFamilySeeded : MobThemeFamily
    {
        public MobThemeFamilySeeded() : base() { }
        public MobThemeFamilySeeded(RandRange amount) : base(amount) { }
        protected MobThemeFamilySeeded(MobThemeFamilySeeded other)  : base(other)
        { }
        public override MobTheme Copy() { return new MobThemeFamilySeeded(this); }

        protected override IEnumerable<int> GetSpecies(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            MobSpawn baseMob = GetSeedChar(map, specialMobs);
            if (baseMob != null)
            {
                int earliestBaseStage = baseMob.BaseForm.Species;

                MonsterData baseData = DataManager.Instance.GetMonster(earliestBaseStage);
                while (baseData.PromoteFrom > -1)
                {
                    earliestBaseStage = baseData.PromoteFrom;
                    baseData = DataManager.Instance.GetMonster(earliestBaseStage);
                }
                yield return earliestBaseStage;
            }
        }
    }

    [Serializable]
    public class MobThemeFamilyChosen : MobThemeFamily
    {
        public int[] Species;

        public MobThemeFamilyChosen() : base() { }
        public MobThemeFamilyChosen(RandRange amount, params int[] species) : base(amount)
        {
            Species = species;
        }
        protected MobThemeFamilyChosen(MobThemeFamilyChosen other)  : base(other)
        {
            Species = new int[other.Species.Length];
            other.Species.CopyTo(Species, 0);
        }
        public override MobTheme Copy() { return new MobThemeFamilyChosen(this); }

        protected override IEnumerable<int> GetSpecies(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            foreach (int specie in Species)
                yield return specie;
        }
    }

    [Serializable]
    public abstract class MobThemeFamily : MobTheme
    {
        protected MobThemeFamily() { }
        public MobThemeFamily(RandRange amount) : base(amount) { }
        protected MobThemeFamily(MobThemeFamily other) : base(other) { }
        public override List<MobSpawn> GenerateMobs(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            int mobCount = Amount.Pick(map.Rand);
            List<MobSpawn> spawners = new List<MobSpawn>();
            IEnumerable<int> species = GetSpecies(map, specialMobs);
            SpawnList<MobSpawn> subList = new SpawnList<MobSpawn>();
            for (int ii = 0; ii < specialMobs.Count; ii++)
            {
                MobSpawn spawn = specialMobs.GetSpawn(ii);
                if (CheckIfAllowed(map, spawn, species))
                    subList.Add(spawn, specialMobs.GetSpawnRate(ii));
            }
            for (int ii = 0; ii < map.TeamSpawns.Count; ii++)
            {
                SpawnList<MobSpawn> memberSpawns = map.TeamSpawns.GetSpawn(ii).GetPossibleSpawns();
                for (int jj = 0; ii < memberSpawns.Count; jj++)
                {
                    MobSpawn spawn = memberSpawns.GetSpawn(jj);
                    if (CheckIfAllowed(map, spawn, species))
                        subList.Add(spawn, memberSpawns.GetSpawnRate(jj));
                }
            }

            if (subList.Count > 0)
            {
                for (int ii = 0; ii < mobCount; ii++)
                    spawners.Add(subList.Pick(map.Rand));
            }

            return spawners;
        }

        protected abstract IEnumerable<int> GetSpecies(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs);

        protected bool CheckIfAllowed(BaseMapGenContext map, MobSpawn spawn, IEnumerable<int> species)
        {
            int earliestStage = spawn.BaseForm.Species;

            MonsterData data = DataManager.Instance.GetMonster(earliestStage);
            while (data.PromoteFrom > -1)
            {
                earliestStage = data.PromoteFrom;
                data = DataManager.Instance.GetMonster(earliestStage);
            }

            foreach (int baseStage in species)
            {
                if (baseStage == earliestStage)
                    return true;
            }

            return false;
        }
    }

    [Serializable]
    public class MobThemeTypingChosen : MobThemeTyping
    {
        [DataType(0, DataManager.DataType.Element, false)]
        public int[] Types;

        public MobThemeTypingChosen() : base() { }
        public MobThemeTypingChosen(EvoFlag allowance, RandRange amount, params int[] types) : base(allowance, amount)
        {
            Types = types;
        }
        protected MobThemeTypingChosen(MobThemeTypingChosen other)  : base(other)
        {
            Types = new int[other.Types.Length];
            other.Types.CopyTo(Types, 0);
        }
        public override MobTheme Copy() { return new MobThemeTypingChosen(this); }

        protected override List<int> GetTypes(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            List<int> result = new List<int>();
            foreach (int type in Types)
                result.Add(type);
            return result;
        }
    }

    [Serializable]
    public class MobThemeTypingSeeded : MobThemeTyping
    {
        const int THRESHOLD = 3;
        public MobThemeTypingSeeded() : base() { }
        public MobThemeTypingSeeded(EvoFlag allowance, RandRange amount) : base(allowance, amount) { }
        protected MobThemeTypingSeeded(MobThemeTypingSeeded other)  : base(other) { }
        public override MobTheme Copy() { return new MobThemeTypingSeeded(this); }

        protected override List<int> GetTypes(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            Dictionary<int, int> elementFrequency = new Dictionary<int, int>();

            for (int ii = 0; ii < map.TeamSpawns.Count; ii++)
            {
                SpawnList<MobSpawn> mobSpawns = map.TeamSpawns.GetSpawn(ii).GetPossibleSpawns();
                foreach (MobSpawn spawn in mobSpawns)
                {
                    MonsterData data = DataManager.Instance.GetMonster(spawn.BaseForm.Species);
                    BaseMonsterForm form = data.Forms[Math.Max(0, spawn.BaseForm.Form)];
                    if (form.Element1 != 00)
                        MathUtils.AddToDictionary(elementFrequency, form.Element1, 1);
                    if (form.Element2 != 00)
                        MathUtils.AddToDictionary(elementFrequency, form.Element2, 1);
                }
            }

            if (elementFrequency.Count == 0)
            {
                for (int ii = 0; ii < specialMobs.Count; ii++)
                {
                    MobSpawn spawn = specialMobs.GetSpawn(ii);
                    MonsterData data = DataManager.Instance.GetMonster(spawn.BaseForm.Species);
                    BaseMonsterForm form = data.Forms[Math.Max(0, spawn.BaseForm.Form)];
                    if (form.Element1 != 00)
                        MathUtils.AddToDictionary(elementFrequency, form.Element1, 1);
                    if (form.Element2 != 00)
                        MathUtils.AddToDictionary(elementFrequency, form.Element2, 1);
                }
            }

            List<int> result = new List<int>();

            if (elementFrequency.Count > 0)
            {
                //choose randomly from the top 3 types
                List<(int, int)> elements = new List<(int, int)>();
                foreach (int key in elementFrequency.Keys)
                    elements.Add((key, elementFrequency[key]));
                elements.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                int max = elements[0].Item2;
                int limit = elements[Math.Min(THRESHOLD - 1, elements.Count - 1)].Item2 - 1;
                if (limit == 0 && max > 1)
                    limit = 1;
                for (int ii = 0; ii < elements.Count; ii++)
                {
                    if (elements[ii].Item2 > limit)
                        result.Add(elements[ii].Item1);
                }
            }
            return result;
        }
    }

    [Serializable]
    public abstract class MobThemeTyping : MobThemeEvoRestricted
    {
        public MobThemeTyping() { }
        public MobThemeTyping(EvoFlag allowance, RandRange amount) : base(allowance, amount) { }
        protected MobThemeTyping(MobThemeTyping other) : base(other) { }

        public override List<MobSpawn> GenerateMobs(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            int mobCount = Amount.Pick(map.Rand);
            List<MobSpawn> spawners = new List<MobSpawn>();
            List<int> types = GetTypes(map, specialMobs);
            SpawnList<MobSpawn> subList = new SpawnList<MobSpawn>();
            for (int ii = 0; ii < specialMobs.Count; ii++)
            {
                MobSpawn spawn = specialMobs.GetSpawn(ii);
                if (CheckIfAllowed(map, spawn, types))
                    subList.Add(spawn, specialMobs.GetSpawnRate(ii));
            }
            for (int ii = 0; ii < map.TeamSpawns.Count; ii++)
            {
                SpawnList<MobSpawn> memberSpawns = map.TeamSpawns.GetSpawn(ii).GetPossibleSpawns();
                for (int jj = 0; jj < memberSpawns.Count; jj++)
                {
                    MobSpawn spawn = memberSpawns.GetSpawn(jj);
                    if (CheckIfAllowed(map, spawn, types))
                        subList.Add(spawn, memberSpawns.GetSpawnRate(jj));
                }
            }

            if (subList.Count > 0)
            {
                for (int ii = 0; ii < mobCount; ii++)
                    spawners.Add(subList.Pick(map.Rand));
            }

            return spawners;
        }

        protected abstract List<int> GetTypes(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs);

        protected bool CheckIfAllowed(BaseMapGenContext map, MobSpawn spawn, List<int> types)
        {
            MonsterData data = DataManager.Instance.GetMonster(spawn.BaseForm.Species);
            BaseMonsterForm form = data.Forms[Math.Max(0, spawn.BaseForm.Form)];
            bool matchesType = false;
            foreach (int type in types)
            {
                if (form.Element1 == type || form.Element2 == type)
                {
                    matchesType = true;
                    break;
                }
            }

            if (matchesType)
            {
                if (CheckIfAllowed(data))
                    return true;
            }
            return false;
        }
    }

    //TODO: seedable stat themes?
    [Serializable]
    public class MobThemeStat : MobThemeEvoRestricted
    {
        public Stat ChosenStat;
        public bool Weakness;

        public MobThemeStat() { }
        public MobThemeStat(Stat stat, bool weakness, EvoFlag allowance, RandRange amount) : base(allowance, amount)
        {
            ChosenStat = stat;
            Weakness = weakness;
        }
        protected MobThemeStat(MobThemeStat other) : base(other)
        {
            ChosenStat = other.ChosenStat;
            Weakness = other.Weakness;
        }
        public override MobTheme Copy() { return new MobThemeStat(this); }

        public override List<MobSpawn> GenerateMobs(BaseMapGenContext map, SpawnList<MobSpawn> specialMobs)
        {
            int mobCount = Amount.Pick(map.Rand);
            List<MobSpawn> spawners = new List<MobSpawn>();

            SpawnList<MobSpawn> subList = new SpawnList<MobSpawn>();
            for (int ii = 0; ii < specialMobs.Count; ii++)
            {
                MobSpawn spawn = specialMobs.GetSpawn(ii);
                if (CheckIfAllowed(spawn))
                    subList.Add(spawn, specialMobs.GetSpawnRate(ii));
            }
            for (int ii = 0; ii < map.TeamSpawns.Count; ii++)
            {
                SpawnList<MobSpawn> memberSpawns = map.TeamSpawns.GetSpawn(ii).GetPossibleSpawns();
                for (int jj = 0; jj < memberSpawns.Count; jj++)
                {
                    if (CheckIfAllowed(memberSpawns.GetSpawn(jj)))
                        subList.Add(memberSpawns.GetSpawn(jj), memberSpawns.GetSpawnRate(jj));
                }
            }

            if (subList.Count > 0)
            {
                for (int ii = 0; ii < mobCount; ii++)
                    spawners.Add(subList.Pick(map.Rand));
            }

            return spawners;
        }

        protected bool CheckIfAllowed(MobSpawn spawn)
        {
            MonsterData data = DataManager.Instance.GetMonster(spawn.BaseForm.Species);
            BaseMonsterForm form = data.Forms[Math.Max(0, spawn.BaseForm.Form)];

            bool passesStat = true;
            int chosenStat = form.GetBaseStat(ChosenStat);
            for (int ii = 0; ii < (int)Stat.HitRate; ii++)
            {
                if ((form.GetBaseStat((Stat)ii) - chosenStat) * (Weakness ? -1 : 1) > 0)
                {
                    passesStat = false;
                    break;
                }
            }
            if (passesStat)
            {
                if (CheckIfAllowed(data))
                    return true;
            }
            return false;
        }
    }


    [Serializable]
    public abstract class MobThemeEvoRestricted : MobTheme
    {
        [Flags]
        public enum EvoFlag
        {
            None = 0,
            NoEvo = 1,//^0
            FirstEvo = 2,//^1
            FinalEvo = 4,//^2
            MidEvo = 8,//^3
            All = 15
        }
        public EvoFlag EvoAllowance;

        public MobThemeEvoRestricted() { }
        public MobThemeEvoRestricted(EvoFlag allowance, RandRange amount) : base(amount) { EvoAllowance = allowance; }
        protected MobThemeEvoRestricted(MobThemeEvoRestricted other) : base(other)
        {
            EvoAllowance = other.EvoAllowance;
        }

        protected virtual bool CheckIfAllowed(MonsterData data)
        {
            int prevo = (data.PromoteFrom > -1) ? 2 : 0;
            int evo = (data.Promotions.Count > 0) ? 1 : 0;

            if (((int)Math.Pow(2, prevo + evo) & (int)EvoAllowance) > 0)
                return true;

            return false;
        }
    }

}
