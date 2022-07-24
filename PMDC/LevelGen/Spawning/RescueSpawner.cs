
using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using PMDC.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Spawns a rescue flag somewhere on the map, complete with a Monster House.
    /// This step should never be explicitly added to a map's gen steps.
    /// Instead, it needs to be dynamically added only when in rescue mode.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RescueSpawner<T> : GenStep<T> where T : BaseMapGenContext
    {
        public RescueSpawner() { }

        public override void Apply(T map)
        {
            //TODO: move magic numbers out of here
            EffectTile spawnedChest = new EffectTile("tile_rescue", true);

            List<Loc> freeTiles = ((IPlaceableGenContext<EffectTile>)map).GetAllFreeTiles();

            int randIndex = map.Rand.Next(freeTiles.Count);
            Loc chosenLoc = freeTiles[randIndex];
            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(chosenLoc, spawnedChest);

            //also put a monster house here
            RandRange amount = new RandRange(7, 13);
            int mobCount = amount.Pick(map.Rand);
            List<MobSpawn> chosenMobs = new List<MobSpawn>();

            if (map.TeamSpawns.Count > 0)
            {
                for (int ii = 0; ii < mobCount; ii++)
                {
                    List<MobSpawn> exampleList = map.TeamSpawns.Pick(map.Rand).ChooseSpawns(map.Rand);
                    if (exampleList.Count > 0)
                        chosenMobs.AddRange(exampleList);
                    if (chosenMobs.Count >= mobCount)
                        break;
                }
            }

            if (chosenMobs.Count > 0)
            {
                Rect bounds = new Rect(chosenLoc - new Loc(4), new Loc(9));
                bounds = Rect.Intersect(bounds, new Rect(0, 0, map.Width, map.Height));

                for (int xx = bounds.X; xx < bounds.End.X; xx++)
                {
                    for (int yy = bounds.Y; yy < bounds.End.Y; yy++)
                    {
                        if ((xx == bounds.X || xx == bounds.End.X - 1) && (yy == bounds.Y || yy == bounds.End.Y - 1))
                            continue;

                        if (map.WallTerrain.TileEquivalent(map.GetTile(new Loc(xx,yy))))
                            map.SetTile(new Loc(xx, yy), map.RoomTerrain.Copy());
                    }
                }

                //cover the room in a check that holds all of the monsters, and covers the room's bounds
                CheckIntrudeBoundsEvent check = new CheckIntrudeBoundsEvent();
                check.Bounds = bounds;
                {
                    MonsterHouseMapEvent house = new MonsterHouseMapEvent();
                    house.Bounds = check.Bounds;
                    foreach (MobSpawn mob in chosenMobs)
                        house.Mobs.Add(mob.Copy());
                    check.Effects.Add(house);
                }

                string intrudeStatus = "intrusion_check";
                MapStatus status = new MapStatus(intrudeStatus);
                status.LoadFromData();
                MapCheckState checkState = status.StatusStates.GetWithDefault<MapCheckState>();
                checkState.CheckEvents.Add(check);
                map.Map.Status.Add(intrudeStatus, status);
            }
        }

    }
}
