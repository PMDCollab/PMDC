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

namespace PMDC.LevelGen
{

    [Serializable]
    public class SpeciesItemListSpawner<TGenContext> : SpeciesItemSpawner<TGenContext>
        where TGenContext : BaseMapGenContext
    {
        public SpeciesItemListSpawner()
        {
        }

        public SpeciesItemListSpawner(IntRange rarity, RandRange amount, params string[] species) : base(rarity, amount)
        {
            this.Species.AddRange(species);
        }

        [JsonConverter(typeof(MonsterListConverter))]
        [DataType(1, DataManager.DataType.Monster, false)]
        public List<string> Species { get; set; }

        public override IEnumerable<string> GetPossibleSpecies(TGenContext map)
        {
            foreach (string baseSpecies in Species)
                yield return baseSpecies;
        }
    }
}
