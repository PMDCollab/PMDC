using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;
using RogueEssence.Dev;
using RogueEssence.Data;
using Newtonsoft.Json;

namespace PMDC.LevelGen
{
    /// <summary>
    /// One part of several steps used to create a sealed key room, or several thereof.
    /// This step takes the target rooms and surrounds them with unbreakable walls, with several guards used to unlock them.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class GuardSealStep<T> : BaseSealStep<T> where T : ListMapGenContext
    {
        public IMultiRandPicker<MobSpawn> Guards;


        public GuardSealStep()
        {
        }

        public GuardSealStep(IMultiRandPicker<MobSpawn> guards) : base()
        {
            Guards = guards;
        }

        protected override void PlaceBorders(T map, Dictionary<Loc, SealType> sealList)
        {
            List<Loc> guardLocList = new List<Loc>();

            foreach (Loc loc in sealList.Keys)
            {
                switch (sealList[loc])
                {
                    //lay down the blocks
                    case SealType.Blocked:
                        map.SetTile(loc, map.UnbreakableTerrain.Copy());
                        break;
                    case SealType.Locked:
                        {
                            if (!Grid.IsChokePoint(loc - Loc.One, Loc.One * 3, loc,
                                map.TileBlocked, (Loc testLoc) => { return true; }))
                                map.SetTile(loc, map.UnbreakableTerrain.Copy());
                        }
                        break;
                    case SealType.Key:
                        guardLocList.Add(loc);
                        break;
                }
            }

            List<MobSpawn> spawns = Guards.Roll(map.Rand);

            foreach (MobSpawn spawn in spawns)
            {
                Loc baseLoc = guardLocList[map.Rand.Next(guardLocList.Count)];
                Loc? destLoc = map.Map.GetClosestTileForChar(null, baseLoc);
                if (destLoc.HasValue)
                {
                    MonsterTeam team = new MonsterTeam();
                    Character newChar = spawn.Spawn(team, map);
                    ((IGroupPlaceableGenContext<TeamSpawn>)map).PlaceItems(new TeamSpawn(team, false), new Loc[1] { destLoc.Value });
                }
            }
        }

    }
}
