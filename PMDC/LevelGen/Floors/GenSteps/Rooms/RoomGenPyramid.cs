using System;
using RogueElements;
using RogueEssence.Dungeon;
using System.Collections.Generic;
using RogueEssence;
using RogueEssence.LevelGen;
using RogueEssence.Data;
using RogueEssence.Dev;

namespace PMDC.LevelGen
{
    /// <summary>
    /// Generates an evolution room.  It's 7x6 in size and hardcoded to look a specific way.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class RoomGenPyramid<T> : RoomGen<T> where T : BaseMapGenContext
    {
        const int MAP_HEIGHT = 179;
        const int MAP_WIDTH = 179;

        public EffectTile Exit;

        [DataType(0, DataManager.DataType.AutoTile, false)]
        public string AutoTileset;

        public RoomGenPyramid() { }
        public RoomGenPyramid(RoomGenPyramid<T> other)
        {
            Exit = new EffectTile(other.Exit);
            AutoTileset = other.AutoTileset;
        }
        public override RoomGen<T> Copy() { return new RoomGenPyramid<T>(this); }

        public override Loc ProposeSize(IRandom rand)
        {
            return new Loc(MAP_WIDTH, MAP_HEIGHT);
        }

        protected override void PrepareFulfillableBorders(IRandom rand)
        {
            FulfillableBorder[Dir4.Down][MAP_WIDTH / 2] = true;


            FulfillableBorder[Dir4.Up][0] = true;
            FulfillableBorder[Dir4.Up][MAP_WIDTH - 1] = true;
            FulfillableBorder[Dir4.Left][0] = true;
            FulfillableBorder[Dir4.Right][0] = true;
        }

        public override void DrawOnMap(T map)
        {
            if (MAP_WIDTH != Draw.Width || MAP_HEIGHT != Draw.Height)
            {
                DrawMapDefault(map);
                return;
            }

            //TODO: intelligently find a layer with the right draw layer or add one if it doesn't exist
            MapLayer targetLayer = map.Map.Layers[0];
            AutoTileData entry = DataManager.Instance.GetAutoTile(AutoTileset);

            for (int x = 0; x < Draw.Width; x++)
            {
                for (int y = 0; y < Draw.Height; y++)
                {
                    if (x == MAP_WIDTH / 2 && y >= MAP_HEIGHT - 5)
                    {
                        map.SetTile(new Loc(Draw.X + x, Draw.Y + y), map.RoomTerrain.Copy());
                        if (y == MAP_HEIGHT - 5)
                            ((IPlaceableGenContext<EffectTile>)map).PlaceItem(new Loc(Draw.X + x, Draw.Y + y), new EffectTile(Exit));
                    }
                    else
                        map.SetTile(new Loc(Draw.X + x, Draw.Y + y), map.UnbreakableTerrain.Copy());

                    int neighborCode = 0xFF;

                    if (x > MAP_WIDTH / 2 - 10 && x < MAP_WIDTH / 2 + 10 && y >= MAP_HEIGHT - 10)
                    {
                        if (x >= MAP_WIDTH / 2 - 1 && x <= MAP_WIDTH / 2 + 1 && y >= MAP_HEIGHT - 6)
                        {
                            if (y == xn && x < MAP_WIDTH / 2)
                            {
                            }
                            else if (y > xn && y < xn2 && x < MAP_WIDTH / 2)
                            {
                            }
                            else if (y == xn2 && x < MAP_WIDTH / 2)
                            {
                            }
                            else if (y > xn2 && x < MAP_WIDTH / 2)
                            {
                            }
                            else if (y == MAP_WIDTH - xn - 1 && x < MAP_WIDTH / 2)
                            {
                            }
                            else if (y > MAP_WIDTH - xn - 1 && y < MAP_WIDTH - xn2 - 1 && x > MAP_WIDTH / 2)
                            {

                            }
                            else if (y == MAP_WIDTH - xn2 - 1 && x > MAP_WIDTH / 2)
                            {

                            }
                            else if (y > MAP_WIDTH - xn2 - 1 && x > MAP_WIDTH / 2)
                            {

                            }
                            else
                            {

                            }
                        }
                        else
                        {
                            neighborCode = 0x00;
                        }
                    }
                    else if (x < 10 || x >= MAP_WIDTH - 10 || y < 10 || y >= MAP_HEIGHT - 10)
                    {
                        if (y > x)
                        {
                            if (y > MAP_WIDTH - x - 1)
                                neighborCode = 0x6E;
                            else if (y < MAP_WIDTH - x - 1)
                                neighborCode = 0xCD;
                            else
                                neighborCode = 0x4C;
                        }
                        else if (y < x)
                        {
                            if (y > MAP_WIDTH - x - 1)
                                neighborCode = 0x37;
                            else if (y < MAP_WIDTH - x - 1)
                                neighborCode = 0x9B;
                            else
                                neighborCode = 0x13;
                        }
                        else
                        {
                            if (y > MAP_WIDTH - x - 1)
                                neighborCode = 0x26;
                            else if (y < MAP_WIDTH - x - 1)
                                neighborCode = 0x89;
                            else
                                neighborCode = 0xFF;
                        }
                    }

                    if (neighborCode != 0xFF)
                    {
                        List<TileLayer> layers = entry.Tiles.GetLayers(neighborCode);
                        targetLayer.Tiles[Draw.X + x][Draw.Y + y] = new AutoTile(layers.ToArray());
                    }
                }
            }

            SetRoomBorders(map);
        }

        public override string ToString()
        {
            return string.Format("{0}", this.GetType().GetFormattedTypeName());
        }
    }

}
