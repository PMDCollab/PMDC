using RogueElements;
using System;
using System.IO;
using System.Collections.Generic;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;
using RogueEssence;
using RogueEssence.Data;
using System.Xml;
using Newtonsoft.Json;
using RogueEssence.Dev;
using PMDC.Data;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Gets specific items of a certain specified type
    /// </summary>
    /// <typeparam name="TGenContext"></typeparam>
    [Serializable]
    public class SpeciesItemElementSpawner<TGenContext> : SpeciesItemSpawner<TGenContext>
        where TGenContext : BaseMapGenContext
    {
        public SpeciesItemElementSpawner()
        {
        }

        public SpeciesItemElementSpawner(IntRange rarity, RandRange amount, string element, HashSet<string> exceptFor) : base(rarity, amount)
        {
            Element = element;
            ExceptFor = exceptFor;
        }

        [DataType(0, DataManager.DataType.Element, false)]
        public string Element { get; set; }

        [DataType(1, DataManager.DataType.Monster, false)]
        public HashSet<string> ExceptFor { get; set; }

        public override IEnumerable<string> GetPossibleSpecies(TGenContext map)
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

                if (Element == DataManager.Instance.DefaultElement || form.Element1 == Element || form.Element2 == Element)
                    yield return key;
            }
        }
    }
}
