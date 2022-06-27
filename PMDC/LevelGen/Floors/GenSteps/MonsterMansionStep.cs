using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence;
using RogueEssence.LevelGen;
using PMDC.Dungeon;

namespace PMDC.LevelGen
{
    /// <summary>
    /// A monster house that consists of the entire floor.
    /// When it activates, you can see all enemies on the map, and all enemies can see you.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class MonsterMansionStep<T> : MonsterHouseBaseStep<T> where T : ListMapGenContext
    {
        public MonsterMansionStep() : base() { }
        public MonsterMansionStep(MonsterMansionStep<T> other) : base(other) { }
        public override MonsterHouseBaseStep<T> CreateNew() { return new MonsterMansionStep<T>(this); }

        public override void Apply(T map)
        {
            if (!ItemThemes.CanPick)
                return;

            if (!MobThemes.CanPick)
                return;

            Rect bounds = new Rect(0, 0, map.Width, map.Height);

            //determine the number of free tiles to put items on; trim the maximum item spawn accordingly (maximum <= 1/2 of free tiles)
            //determine the number of free tiles to put mobs on; trim the maximum mob spawn accordingly (maximum <= 1/2 of free tiles)
            List<Loc> itemTiles = new List<Loc>();
            int mobSpace = 0;
            for (int x = bounds.X; x < bounds.X + bounds.Size.X; x++)
            {
                for (int y = bounds.Y; y < bounds.Y + bounds.Size.Y; y++)
                {
                    Loc testLoc = new Loc(x, y);
                    if (!map.TileBlocked(testLoc))
                    {
                        if (!map.HasTileEffect(new Loc(x, y)) && (map.GetPostProc(testLoc).Status & (PostProcType.Panel | PostProcType.Item)) == PostProcType.None)
                        {
                            bool hasItem = false;
                            foreach (MapItem item in map.Items)
                            {
                                if (item.TileLoc == testLoc)
                                {
                                    hasItem = true;
                                    break;
                                }
                            }
                            if (!hasItem)
                                itemTiles.Add(testLoc);
                        }
                        bool hasMob = false;
                        foreach (Team team in map.AllyTeams)
                        {
                            foreach (Character testChar in team.EnumerateChars())
                            {
                                if (testChar.CharLoc == testLoc)
                                {
                                    hasMob = true;
                                    break;
                                }
                            }
                        }
                        foreach (Team team in map.MapTeams)
                        {
                            foreach (Character testChar in team.EnumerateChars())
                            {
                                if (testChar.CharLoc == testLoc)
                                {
                                    hasMob = true;
                                    break;
                                }
                            }
                        }
                        if (!hasMob)
                            mobSpace++;
                    }
                }
            }

            //choose which item theme to work with
            ItemTheme chosenItemTheme = ItemThemes.Pick(map.Rand);

            //the item spawn list in this class dictates the items available for spawning
            //it will be queried for items that match the theme selected
            List<MapItem> chosenItems = chosenItemTheme.GenerateItems(map, Items);
            
            //place the items
            for (int ii = 0; ii < chosenItems.Count; ii++)
            {
                if (itemTiles.Count > 0)
                {
                    MapItem item = new MapItem(chosenItems[ii]);
                    int randIndex = map.Rand.Next(itemTiles.Count);
                    ((IPlaceableGenContext<MapItem>)map).PlaceItem(itemTiles[randIndex], item);
                    itemTiles.RemoveAt(randIndex);
                }
            }



            //the mob theme will be selected randomly
            MobTheme chosenMobTheme = MobThemes.Pick(map.Rand);

            //the mobs in this class are the ones that would be available when the game wants to spawn things outside of the floor's spawn list
            //it will be queried for monsters that match the theme provided
            List<MobSpawn> chosenMobs = chosenMobTheme.GenerateMobs(map, Mobs);

            //cover the room in a check that holds all of the monsters, and covers the room's bounds
            CheckIntrudeBoundsEvent check = new CheckIntrudeBoundsEvent();
            check.Bounds = bounds;
            {
                RevealAllEvent reveal = new RevealAllEvent();
                check.Effects.Add(reveal);

                GiveMapStatusSingleEvent statusEvent = new GiveMapStatusSingleEvent(30, 0);
                check.Effects.Add(statusEvent);

                MonsterHouseMapEvent house = new MonsterHouseMapEvent();
                house.Bounds = bounds;

                foreach (MobSpawn mob in chosenMobs)
                {
                    MobSpawn copyMob = mob.Copy();
                    if (map.Rand.Next(ALT_COLOR_ODDS) == 0)
                        copyMob.BaseForm.Skin = 1;
                    house.Mobs.Add(copyMob);
                }
                check.Effects.Add(house);
            }

            AddIntrudeStep(map, check);
        }
    }

}
