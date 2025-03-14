using System;
using System.Collections.Generic;
using RogueElements;
using RogueEssence.Dungeon;
using RogueEssence.Data;
using Newtonsoft.Json;
using RogueEssence.Dev;

namespace PMDC.Dungeon
{
    /// <summary>
    /// Stays in designated territory, based on terrain block.
    /// Will never get within 1 tile of non-traversible terrain.
    /// Always knows where the player team is, and will move towards it in naive pathing.
    /// </summary>
    [Serializable]
    public class StalkerPlan : AIPlan
    {
        [JsonConverter(typeof(StatusConverter))]
        [DataType(0, DataManager.DataType.Status, false)]
        public string StatusIndex;

        /// <summary>
        /// How many tiles away this mob can see when stalking.
        /// Does not respect vision limitations.
        /// </summary>
        public int DarkRange;

        public StalkerPlan(AIFlags iq, string status, int prescience) : base(iq)
        {
            StatusIndex = status;
            DarkRange = prescience;
        }
        protected StalkerPlan(StalkerPlan other) : base(other)
        {
            StatusIndex = other.StatusIndex;
            DarkRange = other.DarkRange;
        }
        public override BasePlan CreateNew() { return new StalkerPlan(this); }


        public override GameAction Think(Character controlledChar, bool preThink, IRandom rand)
        {
            if (controlledChar.CantWalk)
                return null;

            //check for being hit already.  if hit, will forever abandon this plan
            if (controlledChar.GetStatusEffect(StatusIndex) != null)
                return null;

            //check for being already in the light.  if in the light, skip this plan
            for (int xx = -1; xx <= 1; xx++)
            {
                for (int yy = -1; yy <= 1; yy++)
                {
                    // must stay in the dark
                    if (!TileBlocksLight(controlledChar.CharLoc + new Loc(xx, yy)))
                        return null;
                }
            }

            Character targetChar = null;
            int minRange = DarkRange + 1;
            foreach (Character testChar in ZoneManager.Instance.CurrentMap.ActiveTeam.IterateByRank())
            {
                int testDist = ZoneManager.Instance.CurrentMap.GetClosestDist8(testChar.CharLoc, controlledChar.CharLoc);
                if (testDist < minRange)
                {
                    targetChar = testChar;
                    minRange = testDist;
                }
            }

            //gravitate to the CLOSEST target.
            //iterate in increasing character indices
            GameAction result = null;
            if (targetChar != null)
            {
                //get the direction to that character
                Dir8 dirToChar = ZoneManager.Instance.CurrentMap.GetClosestDir8(controlledChar.CharLoc, targetChar.CharLoc);

                //is it possible to move in that direction?
                //if so, use it
                result = tryDir(controlledChar, targetChar, dirToChar, !preThink);
                if (result != null)
                    return result;
                if (dirToChar.IsDiagonal())
                {
                    Loc diff = controlledChar.CharLoc - targetChar.CharLoc;
                    DirH horiz;
                    DirV vert;
                    dirToChar.Separate(out horiz, out vert);
                    //start with the one that covers the most distance
                    if (Math.Abs(diff.X) < Math.Abs(diff.Y))
                    {
                        result = tryDir(controlledChar, targetChar, vert.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                        result = tryDir(controlledChar, targetChar, horiz.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                    }
                    else
                    {
                        result = tryDir(controlledChar, targetChar, horiz.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                        result = tryDir(controlledChar, targetChar, vert.ToDir8(), !preThink);
                        if (result != null)
                            return result;
                    }
                }
                else
                {
                    result = tryDir(controlledChar, targetChar, DirExt.AddAngles(dirToChar, Dir8.DownLeft), !preThink);
                    if (result != null)
                        return result;
                    result = tryDir(controlledChar, targetChar, DirExt.AddAngles(dirToChar, Dir8.DownRight), !preThink);
                    if (result != null)
                        return result;
                }
            }

            //if a path can't be found to anyone, just wait and stalk
            return new GameAction(GameAction.ActionType.Wait, Dir8.None);
        }

        private bool TileBlocksLight(Loc testLoc)
        {
            Tile tile = ZoneManager.Instance.CurrentMap.GetTile(testLoc);
            if (tile == null)
                return true;

            TerrainData terrain = (TerrainData)tile.Data.GetData();
            if (terrain.BlockLight)
                return true;

            return false;
        }

        private GameAction tryDir(Character controlledChar, Character targetChar, Dir8 testDir, bool respectPeers)
        {
            Loc endLoc = controlledChar.CharLoc + testDir.GetLoc();

            //do not go even one tile near light-exposed tiles
            for (int xx = -1; xx <= 1; xx++)
            {
                for (int yy = -1; yy <= 1; yy++)
                {
                    // must stay in the dark
                    if (!TileBlocksLight(endLoc + new Loc(xx, yy)))
                        return null;
                }
            }

            //check to see if it's possible to move in this direction
            bool blocked = Grid.IsDirBlocked(controlledChar.CharLoc, testDir,
                (Loc testLoc) =>
                {
                    if (IsPathBlocked(controlledChar, testLoc))
                        return true;

                    if (ZoneManager.Instance.CurrentMap.WrapLoc(testLoc) != targetChar.CharLoc && respectPeers)
                    {
                        Character destChar = ZoneManager.Instance.CurrentMap.GetCharAtLoc(testLoc);
                        if (!canPassChar(controlledChar, destChar, true))
                            return true;
                    }

                    return false;
                },
                (Loc testLoc) =>
                {
                    return (ZoneManager.Instance.CurrentMap.TileBlocked(testLoc, controlledChar.Mobility, true));
                },
                1);

            //if that direction is good, send the command to move in that direction
            if (blocked)
                return null;

            //check to see if moving in this direction will get to the target char
            if (ZoneManager.Instance.CurrentMap.WrapLoc(endLoc) == targetChar.CharLoc)
                return new GameAction(GameAction.ActionType.Wait, Dir8.None);

            return TrySelectWalk(controlledChar, testDir);
        }
    }
}
