using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;
using System.Collections.Generic;

namespace PMDC.LevelGen
{
    [Serializable]
    public class SwitchDetourStep<T> : BaseDetourStep<T> where T : BaseMapGenContext
    {
        public int SealedTile;
        public int SwitchTile;
        public bool TimeLimit;
        public RandRange EntranceCount;

        public SwitchDetourStep()
        { }

        public SwitchDetourStep(int sealedTile, int switchTile, RandRange entranceCount, bool timeLimit) : this()
        {
            SealedTile = sealedTile;
            SwitchTile = switchTile;
            EntranceCount = entranceCount;
            TimeLimit = timeLimit;
        }

        public override void Apply(T map)
        {
            //first get all free tiles suitable for the switch
            List<Loc> freeSwitchTiles = ((IPlaceableGenContext<EffectTile>)map).GetAllFreeTiles();
            if (freeSwitchTiles.Count == 0)
                return;

            Grid.LocTest checkGround = (Loc testLoc) =>
            {
                if (!Collision.InBounds(map.Width, map.Height, testLoc))
                    return false;
                return (map.Tiles[testLoc.X][testLoc.Y].TileEquivalent(map.RoomTerrain) && !map.HasTileEffect(testLoc));
            };
            Grid.LocTest checkBlock = (Loc testLoc) =>
            {
                if (!Collision.InBounds(map.Width, map.Height, testLoc))
                    return false;
                return map.Tiles[testLoc.X][testLoc.Y].TileEquivalent(map.WallTerrain);
            };

            List<LocRay4> rays = Detection.DetectWalls(((IViewPlaceableGenContext<MapGenEntrance>)map).GetLoc(0), new Rect(0, 0, map.Width, map.Height), checkBlock, checkGround);

            EffectTile effect = new EffectTile(SealedTile, true);

            List<Loc> freeTiles = new List<Loc>();
            List<LocRay4> createdEntrances = new List<LocRay4>();

            int amount = EntranceCount.Pick(map.Rand);

            for (int ii = 0; ii < amount; ii++)
            {
                LocRay4? ray = PlaceRoom(map, rays, effect, freeTiles);

                if (ray != null)
                    createdEntrances.Add(ray.Value);
            }

            if (createdEntrances.Count > 0)
            {
                PlaceEntities(map, freeTiles);

                EffectTile switchTile = new EffectTile(SwitchTile, true);

                if (TimeLimit)
                    switchTile.Danger = true;

                TileListState state = new TileListState();
                for (int mm = 0; mm < createdEntrances.Count; mm++)
                    state.Tiles.Add(new Loc(createdEntrances[mm].Loc));
                switchTile.TileStates.Set(state);

                int randIndex = map.Rand.Next(freeSwitchTiles.Count);
                
                ((IPlaceableGenContext<EffectTile>)map).PlaceItem(freeSwitchTiles[randIndex], switchTile);
            }
        }

    }
}
