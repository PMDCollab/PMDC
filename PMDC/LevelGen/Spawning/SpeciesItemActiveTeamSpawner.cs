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
    /// Gets specific items belonging to a team member of the current save file
    /// </summary>
    /// <typeparam name="TGenContext"></typeparam>
    [Serializable]
    public class SpeciesItemActiveTeamSpawner<TGenContext> : SpeciesItemSpawner<TGenContext>
        where TGenContext : BaseMapGenContext
    {
        public SpeciesItemActiveTeamSpawner()
        {
        }

        public SpeciesItemActiveTeamSpawner(IntRange rarity, RandRange amount, HashSet<string> exceptFor, string exceptInstead) : base(rarity, amount)
        {
            ExceptFor = exceptFor;
            ExceptInstead = exceptInstead;
        }

        [DataType(1, DataManager.DataType.Monster, false)]
        public HashSet<string> ExceptFor { get; set; }

        [DataType(0, DataManager.DataType.Monster, false)]
        public string ExceptInstead { get; set; }

        public override IEnumerable<string> GetPossibleSpecies(TGenContext map)
        {
            GameProgress progress = DataManager.Instance.Save;
            if (progress != null && progress.ActiveTeam != null)
            {
                foreach (Character chara in progress.ActiveTeam.Players)
                {
                    if (ExceptFor.Contains(chara.BaseForm.Species))
                        yield return ExceptInstead;
                    else
                        yield return chara.BaseForm.Species;

                }
            }
        }
    }
}
