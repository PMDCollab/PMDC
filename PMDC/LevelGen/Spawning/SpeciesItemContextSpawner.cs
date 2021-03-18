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
    public class SpeciesItemContextSpawner<TGenContext> : SpeciesItemSpawner<TGenContext>
        where TGenContext : BaseMapGenContext
    {
        public SpeciesItemContextSpawner()
        {
        }

        public SpeciesItemContextSpawner(IntRange rarity, RandRange amount) : base(rarity, amount)
        {

        }

        public override IEnumerable<int> GetPossibleSpecies(TGenContext map)
        {
            foreach (TeamSpawner teamSpawn in map.TeamSpawns)
            {
                foreach (MobSpawn mobSpawn in teamSpawn.GetPossibleSpawns())
                    yield return mobSpawn.BaseForm.Species;
            }
        }
    }
}
