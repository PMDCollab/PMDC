using RogueElements;
using System;
using System.IO;
using System.Collections.Generic;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;
using RogueEssence;
using RogueEssence.Data;
using System.Xml;
using PMDC.Data;

namespace PMDC.LevelGen
{

    [Serializable]
    public abstract class SpeciesItemSpawner<TGenContext> : IStepSpawner<TGenContext, MapItem>
        where TGenContext : BaseMapGenContext
    {
        public SpeciesItemSpawner()
        {
        }

        public SpeciesItemSpawner(IntRange rarity, RandRange amount)
        {
            this.Rarity = rarity;
            this.Amount = amount;
        }

        public IntRange Rarity { get; set; }

        public RandRange Amount { get; set; }

        public abstract IEnumerable<int> GetPossibleSpecies(TGenContext map);

        public List<MapItem> GetSpawns(TGenContext map)
        {
            int chosenAmount = Amount.Pick(map.Rand);

            RarityData rarity = DataManager.Instance.UniversalData.Get<RarityData>();
            List<int> possibleItems = new List<int>();
            foreach (int baseSpecies in GetPossibleSpecies(map))
            {
                for (int ii = Rarity.Min; ii < Rarity.Max; ii++)
                {
                    Dictionary<int, List<int>> rarityTable;
                    if (rarity.RarityMap.TryGetValue(baseSpecies, out rarityTable))
                    {
                        if (rarityTable.ContainsKey(ii))
                            possibleItems.AddRange(rarityTable[ii]);
                    }
                }
            }

            List<MapItem> results = new List<MapItem>();
            if (possibleItems.Count > 0)
            {
                for (int ii = 0; ii < chosenAmount; ii++)
                {
                    int chosenItem = possibleItems[map.Rand.Next(possibleItems.Count)];
                    results.Add(new MapItem(chosenItem));
                }
            }

            return results;
        }

        public override string ToString()
        {
            return string.Format("{0}: Rarity:{1} Amt:{2}", this.GetType().Name, this.Rarity.ToString(), this.Amount.ToString());
        }
    }
}
