using RogueElements;
using System;
using System.IO;
using System.Collections.Generic;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;
using RogueEssence;
using RogueEssence.Data;
using System.Xml;

namespace PMDC.LevelGen
{

    [Serializable]
    public class SpeciesItemListSpawner<TGenContext> : SpeciesItemSpawner<TGenContext>
        where TGenContext : BaseMapGenContext
    {
        public SpeciesItemListSpawner()
        {
        }

        public SpeciesItemListSpawner(IntRange rarity, RandRange amount, params int[] species) : base(rarity, amount)
        {
            this.Species.AddRange(species);
        }

        public List<int> Species { get; set; }

        public override IEnumerable<int> GetPossibleSpecies(TGenContext map)
        {
            foreach (int baseSpecies in Species)
                yield return baseSpecies;
        }
    }
}
