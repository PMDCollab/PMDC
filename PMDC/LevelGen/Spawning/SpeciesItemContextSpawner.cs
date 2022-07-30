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

        public override IEnumerable<string> GetPossibleSpecies(TGenContext map)
        {
            foreach (TeamSpawner teamSpawn in map.TeamSpawns.EnumerateOutcomes())
            {
                SpawnList<MobSpawn> mobsAtFloor = teamSpawn.GetPossibleSpawns();
                foreach (MobSpawn mobSpawn in mobsAtFloor.EnumerateOutcomes())
                    yield return mobSpawn.BaseForm.Species;
            }
        }
    }
}
