using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.LevelGen;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Generates a room containing a ring of water encircling treasure.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenWaterRing<T> : PermissiveRoomGen<T> where T : IPostProcGenContext, ITiledGenContext, IPlaceableGenContext<MapItem>
    {
        /// <summary>
        /// The extra width of the room added to the area occupied by the water ring.
        /// </summary>
        public RandRange PadWidth;

        /// <summary>
        /// The extra height of the room added to the area occupied by the water ring.
        /// </summary>
        public RandRange PadHeight;

        /// <summary>
        /// The amount of items to spawn.
        /// </summary>
        public int ItemAmount;

        /// <summary>
        /// Which items to spawn.
        /// </summary>
        public SpawnList<MapItem> Treasures;

        /// <summary>
        /// The terrain used for the water ring.
        /// </summary>
        public ITile WaterTerrain;

        public RoomGenWaterRing()
        {
            Treasures = new SpawnList<MapItem>();
        }
        protected RoomGenWaterRing(RoomGenWaterRing<T> other)
        {
            PadWidth = other.PadWidth;
            PadHeight = other.PadHeight;
            ItemAmount = other.ItemAmount;
            Treasures = new SpawnList<MapItem>();
            for (int ii = 0; ii < other.Treasures.Count; ii++)
                Treasures.Add(new MapItem(other.Treasures.GetSpawn(ii)), other.Treasures.GetSpawnRate(ii));
            WaterTerrain = other.WaterTerrain.Copy();
        }
        public override RoomGen<T> Copy() { return new RoomGenWaterRing<T>(this); }

        public RoomGenWaterRing(ITile waterTerrain, RandRange padWidth, RandRange padHeight, int itemAmount)
        {
            WaterTerrain = waterTerrain;
            PadWidth = padWidth;
            PadHeight = padHeight;
            ItemAmount = itemAmount;
            Treasures = new SpawnList<MapItem>();
        }

        public override Loc ProposeSize(IRandom rand)
        {
            Loc isleSize = new Loc(1);
            while (isleSize.X * isleSize.Y < ItemAmount)
            {
                if (isleSize.X > isleSize.Y)
                    isleSize.Y++;
                else
                    isleSize.X++;
            }
            Loc ringSize = isleSize + new Loc(2);
            Loc pad = new Loc(Math.Max(PadWidth.Pick(rand), 2), Math.Max(PadHeight.Pick(rand), 2));

            return new Loc(ringSize.X + pad.X, ringSize.Y + pad.Y);
        }

        public override void DrawOnMap(T map)
        {
            Loc isleSize = new Loc(1);
            while (isleSize.X * isleSize.Y < ItemAmount)
            {
                if (isleSize.X > isleSize.Y)
                    isleSize.Y++;
                else
                    isleSize.X++;
            }

            //require at least a rectangle that can contain a ring of land around the ring of water
            if (isleSize.X + 4 > Draw.Size.X || isleSize.Y + 4 > Draw.Size.Y)
            {
                DrawMapDefault(map);
                return;
            }

            Loc ringSize = isleSize + new Loc(2);
            //size of room should be between size of cave + 2 and max
            for (int x = 0; x < Draw.Size.X; x++)
            {
                for (int y = 0; y < Draw.Size.Y; y++)
                    map.SetTile(new Loc(Draw.X + x, Draw.Y + y), map.RoomTerrain.Copy());
            }

            List<Loc> freeTiles = new List<Loc>();
            Loc blockStart = new Loc(Draw.X + 1 + map.Rand.Next(Draw.Size.X - ringSize.X - 1), Draw.Y + 1 + map.Rand.Next(Draw.Size.Y - ringSize.Y - 1));
            for (int x = 0; x < ringSize.X; x++)
            {
                for (int y = 0; y < ringSize.Y; y++)
                {
                    Loc targetLoc = new Loc(blockStart.X + x, blockStart.Y + y);
                    if (x == 0 || x == ringSize.X - 1 || y == 0 || y == ringSize.Y - 1)
                        map.SetTile(targetLoc, WaterTerrain.Copy());
                    else
                    {
                        freeTiles.Add(targetLoc);
                        map.GetPostProc(targetLoc).Status |= PostProcType.Panel;
                        map.GetPostProc(targetLoc).Status |= PostProcType.Item;
                    }
                }
            }
            if (Treasures.Count > 0)
            {
                for (int ii = 0; ii < ItemAmount; ii++)
                {
                    MapItem item = new MapItem(Treasures.Pick(map.Rand));
                    int randIndex = map.Rand.Next(freeTiles.Count);
                    map.PlaceItem(freeTiles[randIndex], item);
                    freeTiles.RemoveAt(randIndex);
                }
            }

            //hall restrictions
            SetRoomBorders(map);
        }
    }
}
