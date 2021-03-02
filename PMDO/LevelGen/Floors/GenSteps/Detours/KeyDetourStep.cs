using System;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDO.Dungeon;
using System.Collections.Generic;

namespace PMDO.LevelGen
{
    [Serializable]
    public class KeyDetourStep<T> : BaseDetourStep<T> where T : BaseMapGenContext
    {
        public int LockedTile;
        
        public KeyDetourStep()
        { }

        public KeyDetourStep(int sealedTile) : this()
        {
            LockedTile = sealedTile;
        }

        public override void Apply(T map)
        {

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

            EffectTile effect = new EffectTile(LockedTile, true);
            TileListState state = new TileListState();
            effect.TileStates.Set(state);

            List<Loc> freeTiles = new List<Loc>();
            LocRay4? ray = PlaceRoom(map, rays, effect, freeTiles);

            if (ray != null)
                PlaceEntities(map, freeTiles);

        }

    }
}
